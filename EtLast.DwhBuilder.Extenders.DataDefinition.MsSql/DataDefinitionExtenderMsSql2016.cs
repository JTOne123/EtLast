﻿namespace FizzCode.EtLast.DwhBuilder.Extenders.DataDefinition.MsSql
{
    using System.Collections.Generic;
    using System.Linq;
    using FizzCode.DbTools.DataDefinition;
    using FizzCode.DbTools.DataDefinition.MsSql2016;

    public static class DataDefinitionExtenderMsSql2016
    {
        public static void ExtendWithEtlRunInfo<T>(T dataDeclaration, DwhBuilderConfiguration configuration)
            where T : DatabaseDeclaration
        {
            var etlRunTable = new SqlTable(dataDeclaration.DefaultSchema, configuration.EtlRunTableName);
            dataDeclaration.AddTable(etlRunTable);

            etlRunTable.AddInt("EtlRunId").SetIdentity().SetPK();
            etlRunTable.AddNVarChar("Name", 200, false);
            etlRunTable.AddNVarChar("MachineName", 200, false);
            etlRunTable.AddNVarChar("UserName", 200, false);
            etlRunTable.AddDateTimeOffset("StartedOn", 7, false);
            etlRunTable.AddDateTimeOffset("FinishedOn", 7, true);
            etlRunTable.AddNVarChar("Result", 20, true);

            dataDeclaration.AddAutoNaming(new List<SqlTable> { etlRunTable });

            var baseTables = dataDeclaration.GetTables();
            foreach (var baseTable in baseTables)
            {
                if (baseTable.HasProperty<EtlRunInfoDisabledProperty>())
                    continue;

                if (baseTable == etlRunTable)
                    continue;

                baseTable.AddInt(configuration.EtlInsertRunIdColumnName, false).SetForeignKeyTo(etlRunTable.SchemaAndTableName);
                baseTable.AddInt(configuration.EtlUpdateRunIdColumnName, false).SetForeignKeyTo(etlRunTable.SchemaAndTableName);
            }
        }

        public static void ExtendWithHistoryTables<T>(T model, DwhBuilderConfiguration configuration)
            where T : DatabaseDeclaration
        {
            var baseTables = model.GetTables();

            var baseTablesWithHistory = baseTables
                .Where(x => x.HasProperty<HasHistoryTableProperty>()
                         && x.SchemaAndTableName.TableName != configuration.EtlRunTableName)
                .ToList();

            var historyTables = new List<SqlTable>();
            foreach (var baseTable in baseTablesWithHistory)
            {
                var historyTable = CreateHistoryTable(baseTable, configuration);
                historyTables.Add(historyTable);
            }

            model.AddAutoNaming(historyTables);
        }

        private static SqlTable CreateHistoryTable(SqlTable baseTable, DwhBuilderConfiguration configuration)
        {
            var historyTable = new SqlTable(baseTable.SchemaAndTableName.Schema, baseTable.SchemaAndTableName.TableName + configuration.HistoryTableNamePostfix);
            baseTable.DatabaseDefinition.AddTable(historyTable);

            var identityColumnName = (configuration.HistoryTableIdentityColumnBase ?? historyTable.SchemaAndTableName.TableName) + configuration.HistoryTableIdentityColumnPostfix;
            historyTable.AddInt(identityColumnName).SetIdentity().SetPK();

            // step #1: copy all columns (including foreign keys)
            foreach (var column in baseTable.Columns)
            {
                var historyColumn = new SqlColumn();
                column.CopyTo(historyColumn);
                historyTable.Columns.Add(column.Name, historyColumn);
                historyColumn.Table = historyTable;
            }

            var baseTablePk = baseTable.Properties.OfType<PrimaryKey>().FirstOrDefault();
            var historyFkToBase = new ForeignKey(historyTable, baseTable, "FK_" + historyTable.SchemaAndTableName.SchemaAndName + "__ToBase");
            foreach (var basePkColumn in baseTablePk.SqlColumns)
            {
                historyFkToBase.ForeignKeyColumns.Add(
                    new ForeignKeyColumnMap(historyTable.Columns[basePkColumn.SqlColumn.Name], basePkColumn.SqlColumn));
            }

            historyTable.Properties.Add(historyFkToBase);

            // step #2: copy foreign key properties (columns were already copied in step #1)
            // only those foreign keys are copied to the history table where each column exists in the history table
            var baseForeignKeys = baseTable.Properties.OfType<ForeignKey>()
                .ToList();

            foreach (var baseFk in baseForeignKeys)
            {
                var historyFk = new ForeignKey(historyTable, baseFk.ReferredTable, null);
                historyTable.Properties.Add(historyFk);

                foreach (var fkCol in baseFk.ForeignKeyColumns)
                {
                    var fkColumn = historyTable.Columns[fkCol.ForeignKeyColumn.Name];
                    historyFk.ForeignKeyColumns.Add(new ForeignKeyColumnMap(fkColumn, fkCol.ReferredColumn));
                }
            }

            baseTable.AddDateTimeOffset(configuration.ValidFromColumnName, 7, configuration.InfinitePastDateTime == null && !configuration.UseContextCreationTimeForNewRecords);
            historyTable.AddDateTimeOffset(configuration.ValidFromColumnName, 7, configuration.InfinitePastDateTime == null && !configuration.UseContextCreationTimeForNewRecords);
            historyTable.AddDateTimeOffset(configuration.ValidToColumnName, 7, configuration.InfiniteFutureDateTime == null);

            return historyTable;
        }
    }
}