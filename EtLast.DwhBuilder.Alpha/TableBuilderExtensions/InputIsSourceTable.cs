﻿namespace FizzCode.EtLast.DwhBuilder.Alpha
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using FizzCode.DbTools.Configuration;
    using FizzCode.DbTools.DataDefinition;
    using FizzCode.DbTools.DataDefinition.MsSql2016;
    using FizzCode.EtLast.AdoNet;

    public delegate void SourceReadSqlStatementCustomizerDelegate(DwhTableBuilder tableBuilder, List<string> whereClauseList, Dictionary<string, object> parameters);

    public static partial class TableBuilderExtensions
    {
        public static DwhTableBuilder[] InputIsSourceTable(this DwhTableBuilder[] builders, DatabaseDefinition sourceModel, ConnectionStringWithProvider sourceConnectionString, AdoNetReaderConnectionScope readerScope, SourceReadSqlStatementCustomizerDelegate sqlStatementCustomizer = null, string customWhereClause = null)
        {
            foreach (var builder in builders)
            {
                builder.SetInputProcessCreator(() => CreateSourceTableReader(builder, sourceModel, sourceConnectionString, readerScope, sqlStatementCustomizer, customWhereClause));
            }

            return builders;
        }

        private static IEvaluable CreateSourceTableReader(DwhTableBuilder builder, DatabaseDefinition sourceModel, ConnectionStringWithProvider sourceConnectionString, AdoNetReaderConnectionScope readerScope, SourceReadSqlStatementCustomizerDelegate sqlStatementCustomizer, string customWhereClause)
        {
            var whereClauseList = new List<string>();
            if (customWhereClause != null)
                whereClauseList.Add(customWhereClause);

            var parameterList = new Dictionary<string, object>();

            sqlStatementCustomizer?.Invoke(builder, whereClauseList, parameterList);

            if (builder.DwhBuilder.Configuration.IncrementalLoadEnabled && builder.RecordTimestampIndicatorColumn != null)
            {
                var lastTimestamp = GetMaxRecordTimestamp(builder);
                if (lastTimestamp != null)
                {
                    whereClauseList.Add(builder.RecordTimestampIndicatorColumn.Name + " >= @MaxRecordTimestamp");
                    parameterList.Add("MaxRecordTimestamp", lastTimestamp.Value);
                }
            }

            var sourceTableName = builder.SqlTable.Properties.OfType<SourceTableNameOverrideProperty>().FirstOrDefault()?.SourceTableName ?? builder.SqlTable.SchemaAndTableName.TableName;
            var sourceSqlTable = sourceModel
                .GetTables()
                .First(x => string.Equals(x.SchemaAndTableName.TableName, sourceTableName, StringComparison.InvariantCultureIgnoreCase));

            return new AdoNetDbReaderProcess(builder.Table.Topic, "SourceTableReader")
            {
                ConnectionString = sourceConnectionString,
                CustomConnectionCreator = readerScope != null ? readerScope.GetConnection : (ConnectionCreatorDelegate)null,
                TableName = builder.DwhBuilder.ConnectionString.Escape(sourceSqlTable.SchemaAndTableName.TableName, sourceSqlTable.SchemaAndTableName.Schema),
                CustomWhereClause = whereClauseList.Count == 0
                    ? null
                    : string.Join(" and ", whereClauseList),
                Parameters = parameterList,
                ColumnConfiguration = sourceSqlTable.Columns.Select(column =>
                    new ReaderColumnConfiguration(column.Name, GetConverter(column.Type.SqlTypeInfo), NullSourceHandler.SetSpecialValue, InvalidSourceHandler.WrapError)
                ).ToList(),
            };
        }

        private static DateTimeOffset? GetMaxRecordTimestamp(DwhTableBuilder builder)
        {
            if (builder.RecordTimestampIndicatorColumn == null)
                return null;

            var result = new GetTableMaxValueProcess<DateTimeOffset?>(builder.Table.Topic, nameof(GetMaxRecordTimestamp) + "Reader")
            {
                ConnectionString = builder.Table.Scope.Configuration.ConnectionString,
                TableName = builder.Table.TableName,
                ColumnName = builder.RecordTimestampIndicatorColumn.Name,
            }.Execute(builder.Table.Scope);

            if (result == null)
                return null;

            if (result.MaxValue == null)
            {
                if (result.RecordCount > 0)
                    return builder.DwhBuilder.Configuration.InfinitePastDateTime;

                return null;
            }

            return result.MaxValue;
        }

        private static ITypeConverter GetConverter(SqlTypeInfo sqlTypeInfo)
        {
            return sqlTypeInfo switch
            {
                SqlBit _ => new BoolConverter(),
                SqlTinyInt _ => new ByteConverter(),
                SqlInt _ => new IntConverter(),
                SqlFloat _ => new DoubleConverter(),
                SqlReal _ => new DoubleConverter(),
                SqlDecimal _ => new DecimalConverter(),
                SqlMoney _ => new DecimalConverter(),
                SqlVarChar _ => new StringConverter(),
                SqlNVarChar _ => new StringConverter(),
                SqlNText _ => new StringConverter(),
                SqlChar _ => new StringConverter(),
                SqlNChar _ => new StringConverter(),
                SqlDateTime _ => new DateTimeConverter(),
                SqlDateTimeOffset _ => new DateTimeOffsetConverter(),
                SqlBigInt _ => new LongConverter(),
                SqlBinary _ => new ByteArrayConverter(),
                _ => null,
            };
        }
    }
}