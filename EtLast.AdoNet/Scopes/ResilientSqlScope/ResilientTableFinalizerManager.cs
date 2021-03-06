﻿namespace FizzCode.EtLast.AdoNet
{
    using System.Collections.Generic;
    using System.Linq;

    internal class ResilientTableFinalizerManager : IProcess
    {
        public ProcessInvocationInfo InvocationInfo { get; set; }

        private readonly ResilientSqlScope _scope;
        public IEtlContext Context => _scope.Context;
        public string Name { get; } = "TableFinalizerManager";
        public ITopic Topic => _scope.Topic;
        public ProcessKind Kind => ProcessKind.scope;

        public ResilientTableFinalizerManager(ResilientSqlScope scope)
        {
            _scope = scope;
        }

        private class TableWithOrder
        {
            internal ResilientTable Table { get; set; }
            internal int OriginalIndex { get; set; }
        }

        public void Execute()
        {
            Context.RegisterProcessInvocationStart(this, _scope);

            var tablesOrderedTemp = new List<TableWithOrder>();
            for (var i = 0; i < _scope.Configuration.Tables.Count; i++)
            {
                tablesOrderedTemp.Add(new TableWithOrder()
                {
                    Table = _scope.Configuration.Tables[i],
                    OriginalIndex = i,
                });
            }

            var tablesOrdered = tablesOrderedTemp
                .OrderBy(x => x.Table.OrderDuringFinalization)
                .ThenBy(x => x.OriginalIndex)
                .Select(x => x.Table)
                .ToList();

            var recordCounts = new int[tablesOrdered.Count];
            for (var i = 0; i < tablesOrdered.Count; i++)
            {
                var table = tablesOrdered[i];

                var recordCount = CountTempRecordsIn(table);
                if (table.AdditionalTables?.Count > 0)
                {
                    foreach (var additionalTable in table.AdditionalTables.Values)
                    {
                        recordCount += CountTempRecordsIn(additionalTable);
                    }
                }

                recordCounts[i] = recordCount;
            }

            Context.Log(LogSeverity.Information, this, "{TableCount} temp table contains data", recordCounts.Count(x => x > 0));
            Context.Log(LogSeverity.Information, this, "{TableCount} temp table is empty", recordCounts.Count(x => x == 0));

            for (var i = 0; i < tablesOrdered.Count; i++)
            {
                var table = tablesOrdered[i];
                if (table.SkipFinalizersIfTempTableIsEmpty && recordCounts[i] == 0)
                {
                    Context.Log(LogSeverity.Debug, this, "no data found for {TableName}, skipping finalizers", _scope.Configuration.ConnectionString.Unescape(table.TableName));
                    continue;
                }

                var creatorScopeKind = table.SuppressTransactionScopeForCreators
                    ? TransactionScopeKind.Suppress
                    : TransactionScopeKind.None;

                IExecutable[] finalizers;
                using (var creatorScope = Context.BeginScope(this, creatorScopeKind, LogSeverity.Information))
                {
                    finalizers = table.FinalizerCreator
                        .Invoke(table)
                        .Where(x => x != null)
                        .ToArray();
                }

                Context.Log(LogSeverity.Debug, this, "created {FinalizerCount} finalizer(s) for {TableName}",
                    finalizers?.Length ?? 0,
                    _scope.Configuration.ConnectionString.Unescape(table.TableName));

                foreach (var finalizer in finalizers)
                {
                    var preExceptionCount = Context.ExceptionCount;
                    Context.Log(LogSeverity.Information, this, "finalizing {TableName} with {ProcessName}",
                        _scope.Configuration.ConnectionString.Unescape(table.TableName),
                        finalizer.Name);
                    finalizer.Execute(this);
                    if (Context.ExceptionCount > preExceptionCount)
                    {
                        break;
                    }
                }
            }

            Context.RegisterProcessInvocationEnd(this);
        }

        private int CountTempRecordsIn(ResilientTableBase table)
        {
            var count = new GetTableRecordCount(table.Topic, "TempRecordCountReader")
            {
                ConnectionString = _scope.Configuration.ConnectionString,
                TableName = _scope.Configuration.ConnectionString.Escape(table.TempTableName),
            }.Execute(this);

            return count;
        }
    }
}