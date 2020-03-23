﻿namespace FizzCode.EtLast.DwhBuilder.MsSql
{
    using System.Collections.Generic;
    using System.Linq;
    using FizzCode.EtLast;
    using FizzCode.EtLast.AdoNet;

    public static partial class TableBuilderExtensions
    {
        public static DwhTableBuilder[] SimpleCopyFinalizer(this DwhTableBuilder[] builders)
        {
            foreach (var builder in builders)
            {
                builder.AddFinalizerCreator(CreateSimpleCopyFinalizer);
            }

            return builders;
        }

        private static IEnumerable<IExecutable> CreateSimpleCopyFinalizer(DwhTableBuilder builder)
        {
            var currentEtlRunId = builder.ResilientTable.Topic.Context.AdditionalData.GetAs("CurrentEtlRunId", 0);

            var columnDefaults = new Dictionary<string, object>();

            if (builder.EtlInsertRunIdColumnNameEscaped != null)
                columnDefaults.Add(builder.EtlInsertRunIdColumnNameEscaped, currentEtlRunId);

            if (builder.EtlUpdateRunIdColumnNameEscaped != null)
                columnDefaults.Add(builder.EtlUpdateRunIdColumnNameEscaped, currentEtlRunId);

            var columnNames = builder.Table.Columns
                .Where(x => x.GetUsedByEtlRunInfo())
                .Select(c => c.NameEscaped(builder.ResilientTable.Scope.Configuration.ConnectionString))
                .ToArray();

            yield return new CopyTableIntoExistingTable(builder.ResilientTable.Topic, "CopyToBase")
            {
                ConnectionString = builder.ResilientTable.Scope.Configuration.ConnectionString,
                Configuration = new TableCopyConfiguration()
                {
                    SourceTableName = builder.ResilientTable.TempTableName,
                    TargetTableName = builder.ResilientTable.TableName,
                    ColumnConfiguration = columnNames
                        .Select(x => new ColumnCopyConfiguration(x))
                        .ToList()
                },
                ColumnDefaults = columnDefaults,
                CommandTimeout = 60 * 60,
            };
        }
    }
}