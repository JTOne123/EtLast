﻿namespace FizzCode.EtLast.AdoNet
{
    using System;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Transactions;
    using FizzCode.DbTools.Configuration;

    public class CopyTableIntoExistingTableJob : AbstractSqlStatementJob
    {
        public TableCopyConfiguration Configuration { get; set; }

        /// <summary>
        /// Optional. Default is NULL which means everything will be transferred from the source table to the target table.
        /// </summary>
        public string WhereClause { get; set; }

        public bool CopyIdentityColumns { get; set; }

        protected override void Validate()
        {
            if (Configuration == null)
                throw new JobParameterNullException(Process, this, nameof(Configuration));
            if (string.IsNullOrEmpty(Configuration.SourceTableName))
                throw new JobParameterNullException(Process, this, nameof(Configuration.SourceTableName));
            if (string.IsNullOrEmpty(Configuration.TargetTableName))
                throw new JobParameterNullException(Process, this, nameof(Configuration.TargetTableName));
        }

        protected override string CreateSqlStatement(ConnectionStringWithProvider connectionString)
        {
            var statement = string.Empty;
            if (CopyIdentityColumns && ConnectionString.KnownProvider == KnownProvider.SqlServer)
            {
                statement = "SET IDENTITY_INSERT " + Configuration.TargetTableName + " ON; ";
            }

            if (Configuration.ColumnConfiguration == null || Configuration.ColumnConfiguration.Count == 0)
            {
                statement += "INSERT INTO " + Configuration.TargetTableName + " SELECT * FROM " + Configuration.SourceTableName;
            }
            else
            {
                var sourceColumnList = string.Join(", ", Configuration.ColumnConfiguration.Select(x => x.FromColumn));
                var targetColumnList = string.Join(", ", Configuration.ColumnConfiguration.Select(x => x.ToColumn));

                statement += "INSERT INTO " + Configuration.TargetTableName + " (" + targetColumnList + ") SELECT " + sourceColumnList + " FROM " + Configuration.SourceTableName;
            }

            if (WhereClause != null)
            {
                statement += " WHERE " + WhereClause.Trim();
            }

            if (CopyIdentityColumns && ConnectionString.KnownProvider == KnownProvider.SqlServer)
            {
                statement += "; SET IDENTITY_INSERT " + Configuration.TargetTableName + " OFF; ";
            }

            return statement;
        }

        protected override void RunCommand(IDbCommand command, Stopwatch startedOn)
        {
            Process.Context.Log(LogSeverity.Debug, Process, "({Job}) copying records from {ConnectionStringKey}/{SourceTableName} to {TargetTableName} with SQL statement {SqlStatement}, timeout: {Timeout} sec, transaction: {Transaction}",
                Name, ConnectionString.Name, Helpers.UnEscapeTableName(Configuration.SourceTableName), Helpers.UnEscapeTableName(Configuration.TargetTableName), command.CommandText, command.CommandTimeout, Transaction.Current.ToIdentifierString());

            try
            {
                var recordCount = command.ExecuteNonQuery();

                Process.Context.Log(LogSeverity.Information, Process, "({Job}) {RecordCount} records copied to {ConnectionStringKey}/{TargetTableName} from {SourceTableName} in {Elapsed}",
                    Name, recordCount, ConnectionString.Name, Helpers.UnEscapeTableName(Configuration.TargetTableName), Helpers.UnEscapeTableName(Configuration.SourceTableName), startedOn.Elapsed);

                // todo: support stats in jobs...
                // Stat.IncrementCounter("records written", recordCount);
                // Stat.IncrementCounter("write time", startedOn.ElapsedMilliseconds);

                Process.Context.Stat.IncrementCounter("database records copied / " + ConnectionString.Name, recordCount);
                Process.Context.Stat.IncrementDebugCounter("database records copied / " + ConnectionString.Name + " / " + Helpers.UnEscapeTableName(Configuration.SourceTableName) + " -> " + Helpers.UnEscapeTableName(Configuration.TargetTableName), recordCount);
                Process.Context.Stat.IncrementCounter("database copy time / " + ConnectionString.Name, startedOn.ElapsedMilliseconds);
                Process.Context.Stat.IncrementDebugCounter("database copy time / " + ConnectionString.Name + " / " + Helpers.UnEscapeTableName(Configuration.SourceTableName) + " -> " + Helpers.UnEscapeTableName(Configuration.TargetTableName), startedOn.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                var exception = new JobExecutionException(Process, this, "database table copy failed", ex);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "database table copy failed, connection string key: {0}, source table: {1}, target table: {2}, source columns: {3}, message: {4}, command: {5}, timeout: {6}",
                    ConnectionString.Name, Helpers.UnEscapeTableName(Configuration.SourceTableName), Helpers.UnEscapeTableName(Configuration.TargetTableName),
                    Configuration.ColumnConfiguration != null
                        ? string.Join(",", Configuration.ColumnConfiguration.Select(x => x.FromColumn))
                        : "all",
                    ex.Message, command.CommandText, CommandTimeout));

                exception.Data.Add("ConnectionStringKey", ConnectionString.Name);
                exception.Data.Add("SourceTableName", Helpers.UnEscapeTableName(Configuration.SourceTableName));
                exception.Data.Add("TargetTableName", Helpers.UnEscapeTableName(Configuration.TargetTableName));
                if (Configuration.ColumnConfiguration != null)
                {
                    exception.Data.Add("SourceColumns", string.Join(",", Configuration.ColumnConfiguration.Select(x => Helpers.UnEscapeColumnName(x.FromColumn))));
                }

                exception.Data.Add("Statement", command.CommandText);
                exception.Data.Add("Timeout", CommandTimeout);
                exception.Data.Add("Elapsed", startedOn.Elapsed);
                throw exception;
            }
        }
    }
}