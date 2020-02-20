﻿namespace FizzCode.EtLast.AdoNet
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Transactions;
    using FizzCode.DbTools.Configuration;

    public abstract class AbstractSqlStatementsProcess : AbstractExecutableProcess
    {
        public ConnectionStringWithProvider ConnectionString { get; set; }
        public int CommandTimeout { get; set; } = 300;

        protected AbstractSqlStatementsProcess(ITopic topic, string name)
            : base(topic, name)
        {
        }

        /// <summary>
        /// If true, this statement will be executed out of ambient transaction scope.
        /// See <see cref="TransactionScopeOption.Suppress"/>>.
        /// </summary>
        public bool SuppressExistingTransactionScope { get; set; }

        protected override void ValidateImpl()
        {
            if (ConnectionString == null)
                throw new ProcessParameterNullException(this, nameof(ConnectionString));
        }

        protected override void ExecuteImpl()
        {
            using (var scope = SuppressExistingTransactionScope ? new TransactionScope(TransactionScopeOption.Suppress) : null)
            {
                var connection = ConnectionManager.GetConnection(ConnectionString, this);
                try
                {
                    lock (connection.Lock)
                    {
                        // todo: support returning parameters
                        var sqlStatements = CreateSqlStatements(ConnectionString, connection.Connection);
                        if (sqlStatements.Count > 0)
                        {
                            using (var cmd = connection.Connection.CreateCommand())
                            {
                                cmd.CommandTimeout = CommandTimeout;

                                var startedOn = Stopwatch.StartNew();

                                Context.OnContextDataStoreCommand?.Invoke(DataStoreCommandKind.many, ConnectionString.Name, this, string.Join("\n---\n", sqlStatements), Transaction.Current.ToIdentifierString(), null);

                                for (var i = 0; i < sqlStatements.Count; i++)
                                {
                                    var sqlStatement = sqlStatements[i];

                                    cmd.CommandText = sqlStatement;
                                    try
                                    {
                                        startedOn.Restart();
                                        RunCommand(cmd, i, startedOn);
                                    }
                                    catch (Exception)
                                    {
                                        LogSucceeded(i - 1);
                                        throw;
                                    }
                                }

                                LogSucceeded(sqlStatements.Count - 1);
                            }
                        }
                    }
                }
                finally
                {
                    ConnectionManager.ReleaseConnection(this, ref connection);
                }
            }
        }

        protected abstract List<string> CreateSqlStatements(ConnectionStringWithProvider connectionString, IDbConnection connection);

        protected abstract void RunCommand(IDbCommand command, int statementIndex, Stopwatch startedOn);
        protected abstract void LogSucceeded(int lastSucceededIndex);
    }
}