﻿namespace FizzCode.EtLast.AdoNet
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Transactions;
    using FizzCode.DbTools.Configuration;
    using FizzCode.EtLast;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public class MsSqlWriteToTableWithMicroTransactionsMutator : AbstractMutator, IRowWriter
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        public ConnectionStringWithProvider ConnectionString { get; set; }
        public int CommandTimeout { get; set; } = 30;

        public DbTableDefinition TableDefinition { get; set; }

        /// <summary>
        /// Default value is true <see cref="SqlBulkCopyOptions.KeepIdentity"/>.
        /// </summary>
        public bool BulkCopyKeepIdentity { get; set; } = true;

        /// <summary>
        /// Default value is false <see cref="SqlBulkCopyOptions.CheckConstraints"/>.
        /// </summary>
        public bool BulkCopyCheckConstraints { get; set; }

        /// <summary>
        /// Default value is 10000
        /// </summary>
        public int BatchSize { get; set; } = 10000;

        /// <summary>
        /// Default value is 5
        /// </summary>
        public int MaxRetryCount { get; set; } = 5;

        /// <summary>
        /// Default value is 5000
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 5000;

        private int _rowsWritten;
        private Stopwatch _timer;
        private RowShadowReader _reader;
        private int? _storeUid;

        public MsSqlWriteToTableWithMicroTransactionsMutator(ITopic topic, string name)
            : base(topic, name)
        {
        }

        protected override void StartMutator()
        {
            _rowsWritten = 0;
            _timer = new Stopwatch();

            var columnIndexes = new Dictionary<string, int>();
            for (var i = 0; i < TableDefinition.Columns.Length; i++)
            {
                columnIndexes[TableDefinition.Columns[i].RowColumn] = i;
            }

            _reader = new RowShadowReader(BatchSize, TableDefinition.Columns.Select(x => x.DbColumn).ToArray(), columnIndexes);
        }

        protected override void CloseMutator()
        {
            if (_reader.RowCount > 0)
            {
                WriteToSql();
            }

            _reader = null;

            _timer.Stop();
            _timer = null;
        }

        protected override IEnumerable<IRow> MutateRow(IRow row)
        {
            if (_storeUid == null)
            {
                _storeUid = Context.GetStoreUid(ConnectionString.Name, ConnectionString.Unescape(TableDefinition.TableName));
            }

            Context.OnRowStored?.Invoke(this, row, _storeUid.Value);

            var rc = _reader.RowCount;
            for (var i = 0; i < TableDefinition.Columns.Length; i++)
            {
                _reader.Rows[rc, i] = row[TableDefinition.Columns[i].RowColumn];
            }

            rc++;
            _reader.RowCount = rc;

            if (rc >= BatchSize)
            {
                WriteToSql();
            }

            yield return row;
        }

        private void WriteToSql()
        {
            var recordCount = _reader.RowCount;
            _timer.Restart();

            for (var retry = 0; retry <= MaxRetryCount; retry++)
            {
                DatabaseConnection connection = null;
                SqlBulkCopy bulkCopy = null;

                using (var scope = Context.BeginScope(this, TransactionScopeKind.RequiresNew, LogSeverity.Debug))
                {
                    var transactionId = Transaction.Current.ToIdentifierString();

                    connection = ConnectionManager.GetConnection(ConnectionString, this, 0);

                    var options = SqlBulkCopyOptions.Default;

                    if (BulkCopyKeepIdentity)
                        options |= SqlBulkCopyOptions.KeepIdentity;

                    if (BulkCopyCheckConstraints)
                        options |= SqlBulkCopyOptions.CheckConstraints;

#pragma warning disable CA2000 // Dispose objects before losing scope
                    bulkCopy = new SqlBulkCopy(connection.Connection as SqlConnection, options, null)
                    {
                        DestinationTableName = TableDefinition.TableName,
                        BulkCopyTimeout = CommandTimeout,
                    };
#pragma warning restore CA2000 // Dispose objects before losing scope

                    foreach (var column in TableDefinition.Columns)
                    {
                        bulkCopy.ColumnMappings.Add(column.RowColumn, column.DbColumn);
                    }

                    var iocUid = Context.RegisterIoCommandStart(this, IoCommandKind.dbWriteBulk, ConnectionString.Name, ConnectionString.Unescape(TableDefinition.TableName), bulkCopy.BulkCopyTimeout, "BULK COPY into " + TableDefinition.TableName + ", " + recordCount.ToString("D", CultureInfo.InvariantCulture) + " records" + (retry > 0 ? ", retry #" + retry.ToString("D", CultureInfo.InvariantCulture) : ""), Transaction.Current.ToIdentifierString(), null,
                        "write to table: {ConnectionStringName}/{Table}",
                        ConnectionString.Name, ConnectionString.Unescape(TableDefinition.TableName));

                    try
                    {
                        bulkCopy.WriteToServer(_reader);
                        bulkCopy.Close();
                        ConnectionManager.ReleaseConnection(this, ref connection);

                        scope.Complete();

                        _timer.Stop();
                        var time = _timer.Elapsed;
                        _rowsWritten += recordCount;

                        Context.RegisterIoCommandSuccess(this, IoCommandKind.dbWriteBulk, iocUid, recordCount);

                        _reader.Reset();
                        break;
                    }
                    catch (Exception ex)
                    {
                        Context.RegisterIoCommandFailed(this, IoCommandKind.dbWriteBulk, iocUid, recordCount, ex);

                        if (connection != null)
                        {
                            ConnectionManager.ConnectionFailed(ref connection);
                            bulkCopy?.Close();
                        }

                        _reader.ResetCurrentIndex();

                        if (retry == 0 && (ex is InvalidOperationException || ex is SqlException))
                        {
                            var fileName = "bulk-copy-error-" + Context.CreatedOnLocal.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture) + ".tsv";
                            Context.LogCustom(fileName, this, "bulk copy error: " + ConnectionString.Name + "/" + ConnectionString.Unescape(TableDefinition.TableName) + ", exception: " + ex.GetType().GetFriendlyTypeName() + ": " + ex.Message);
                            Context.LogCustom(fileName, this, string.Join("\t", _reader.ColumnIndexes.Select(kvp => kvp.Key)));

                            for (var row = 0; row < _reader.RowCount; row++)
                            {
                                var text = string.Join("\t", _reader.ColumnIndexes.Select(kvp =>
                                {
                                    var v = _reader.Rows[row, kvp.Value];
                                    return v == null
                                        ? "NULL"
                                        : "'" + v.ToString() + "' (" + v.GetType().GetFriendlyTypeName() + ")";
                                }));

                                Context.LogCustom(fileName, this, text);
                            }
                        }

                        if (retry < MaxRetryCount)
                        {
                            Context.Log(LogSeverity.Error, this, "db write failed, retrying in {DelayMsec} msec (#{AttemptIndex}): {ExceptionMessage}", RetryDelayMilliseconds * (retry + 1),
                                retry, ex.Message);

                            Context.LogOps(LogSeverity.Error, this, "db write failed, retrying in {DelayMsec} msec (#{AttemptIndex}): {ExceptionMessage}", Name,
                                RetryDelayMilliseconds * (retry + 1), retry, ex.Message);

                            Thread.Sleep(RetryDelayMilliseconds * (retry + 1));
                        }
                        else
                        {
                            var exception = new ProcessExecutionException(this, "db write failed", ex);
                            exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "db write failed, connection string key: {0}, table: {1}, message: {2}",
                                ConnectionString.Name, ConnectionString.Unescape(TableDefinition.TableName), ex.Message));
                            exception.Data.Add("ConnectionStringName", ConnectionString.Name);
                            exception.Data.Add("TableName", ConnectionString.Unescape(TableDefinition.TableName));
                            exception.Data.Add("Columns", string.Join(", ", TableDefinition.Columns.Select(x => x.RowColumn + " => " + ConnectionString.Unescape(x.DbColumn))));
                            exception.Data.Add("Timeout", CommandTimeout);
                            exception.Data.Add("Elapsed", _timer.Elapsed);
                            exception.Data.Add("TotalRowsWritten", _rowsWritten);
                            if (ex is InvalidOperationException || ex is SqlException)
                            {
                                var fileName = "bulk-copy-error-" + Context.CreatedOnLocal.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture) + ".tsv";
                                exception.Data.Add("DetailedRowLogFileName", fileName);
                            }

                            throw exception;
                        }
                    }
                }
            }
        }

        protected override void ValidateMutator()
        {
            base.ValidateMutator();

            if (ConnectionString == null)
                throw new ProcessParameterNullException(this, nameof(ConnectionString));

            if (TableDefinition == null)
                throw new ProcessParameterNullException(this, nameof(TableDefinition));

            if (ConnectionString.SqlEngine != SqlEngine.MsSql)
                throw new InvalidProcessParameterException(this, "ConnectionString", nameof(ConnectionString.ProviderName), "provider name must be System.Data.SqlClient");
        }
    }
}