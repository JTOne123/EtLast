﻿namespace FizzCode.EtLast.DwhBuilder
{
    using System.Linq;
    using FizzCode.LightWeight.RelationalModel;

    public static class RelationalModelExtender
    {
        public static void ExtendWithEtlRunInfo(RelationalSchema etlRunTableSchema, DwhBuilderConfiguration configuration)
        {
            var etlRunTable = etlRunTableSchema.AddTable(configuration.EtlRunTableName).SetEtlRunInfo();

            etlRunTable.AddColumn("StartedOn", false);
            etlRunTable.AddColumn("Name", false);
            etlRunTable.AddColumn("MachineName", false);
            etlRunTable.AddColumn("UserName", false);
            etlRunTable.AddColumn("FinishedOn", false);
            etlRunTable.AddColumn("Result", false);

            foreach (var schema in etlRunTableSchema.Model.Schemas)
            {
                foreach (var baseTable in schema.Tables)
                {
                    if (baseTable.GetEtlRunInfoDisabled() || baseTable == etlRunTable)
                        continue;

                    var etlRunInsertColumn = baseTable.AddColumn(configuration.EtlRunInsertColumnName, false).SetUsedByEtlRunInfo();
                    var etlRunUpdateColumn = baseTable.AddColumn(configuration.EtlRunUpdateColumnName, false).SetUsedByEtlRunInfo();

                    baseTable.AddForeignKeyTo(etlRunTable).AddColumnPair(etlRunInsertColumn, etlRunTable["StartedOn"]);
                    baseTable.AddForeignKeyTo(etlRunTable).AddColumnPair(etlRunUpdateColumn, etlRunTable["StartedOn"]);
                }
            }
        }

        public static void ExtendWithHistoryTables(RelationalModel model, DwhBuilderConfiguration configuration)
        {
            var baseTablesWithHistory = model.Schemas.SelectMany(x => x.Tables)
                .Where(x => x.GetHasHistoryTable() && !x.GetIsEtlRunInfo())
                .ToList();

            var etlRunTable = model.Schemas
                .SelectMany(x => x.Tables)
                .FirstOrDefault(x => x.GetIsEtlRunInfo());

            foreach (var baseTable in baseTablesWithHistory)
            {
                CreateHistoryTable(baseTable, configuration, etlRunTable);
            }
        }

        private static void CreateHistoryTable(RelationalTable baseTable, DwhBuilderConfiguration configuration, RelationalTable etlRunTable)
        {
            var historyTable = baseTable.Schema.AddTable(baseTable.Name + configuration.HistoryTableNamePostfix).SetIsHistoryTable();
            var identityColumnName = (configuration.HistoryTableIdentityColumnBase ?? historyTable.Name) + configuration.HistoryTableIdentityColumnPostfix;
            historyTable.AddColumn(identityColumnName, true).SetIdentity();

            foreach (var column in baseTable.Columns)
            {
                historyTable.AddColumn(column.Name, false);
            }

            if (baseTable.PrimaryKeyColumns.Count > 0)
            {
                var historyFkToBase = historyTable.AddForeignKeyTo(baseTable);
                foreach (var basePkColumn in baseTable.PrimaryKeyColumns)
                {
                    historyFkToBase.AddColumnPair(historyTable[basePkColumn.Name], basePkColumn);
                }
            }

            foreach (var baseFk in baseTable.ForeignKeys)
            {
                var historyFk = historyTable.AddForeignKeyTo(baseFk.TargetTable);

                foreach (var baseFkPair in baseFk.ColumnPairs)
                {
                    historyFk.AddColumnPair(historyTable[baseFkPair.SourceColumn.Name], baseFkPair.TargetColumn);
                }
            }

            baseTable.AddColumn(configuration.ValidFromColumnName, false);
            historyTable.AddColumn(configuration.ValidFromColumnName, false);
            historyTable.AddColumn(configuration.ValidToColumnName, false);

            if (etlRunTable != null)
            {
                var c1 = historyTable.AddColumn(configuration.EtlRunFromColumnName, false);
                var c2 = historyTable.AddColumn(configuration.EtlRunToColumnName, false);

                historyTable.AddForeignKeyTo(etlRunTable).AddColumnPair(c1, etlRunTable["StartedOn"]);
                historyTable.AddForeignKeyTo(etlRunTable).AddColumnPair(c2, etlRunTable["StartedOn"]);
            }
        }
    }
}