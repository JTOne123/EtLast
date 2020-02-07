﻿namespace FizzCode.EtLast.DwhBuilder.Alpha
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using FizzCode.DbTools.DataDefinition;
    using FizzCode.EtLast;
    using FizzCode.EtLast.AdoNet;

    public static partial class TableBuilderExtensions
    {
        public static DwhTableBuilder[] AutoValidityRange(this DwhTableBuilder[] builders, Action<AutoValidityRangeBuilder> customizer)
        {
            foreach (var tableBuilder in builders)
            {
                var tempBuilder = new AutoValidityRangeBuilder(tableBuilder);
                customizer.Invoke(tempBuilder);

                if (tempBuilder.MatchColumns == null)
                    throw new NotSupportedException("you must specify the key columns of " + nameof(AutoValidityRange) + " for table " + tableBuilder.Table.TableName);

                tableBuilder.AddOperationCreator(_ => CreateAutoValidityRangeOperations(tempBuilder));
            }

            return builders;
        }

        private static IEnumerable<IRowOperation> CreateAutoValidityRangeOperations(AutoValidityRangeBuilder builder)
        {
            var pk = builder.TableBuilder.SqlTable.Properties.OfType<PrimaryKey>().FirstOrDefault();

            var finalValueColumns = builder.CompareValueColumns
                .Where(x => builder.MatchColumns.All(kc => !string.Equals(x, kc, StringComparison.InvariantCultureIgnoreCase))
                        && (pk?.SqlColumns.All(pkc => !string.Equals(x, pkc.SqlColumn.Name, StringComparison.InvariantCultureIgnoreCase)) != false)
                        && builder.PreviousValueColumnNameMap.All(kc => !string.Equals(x, kc.Value, StringComparison.InvariantCultureIgnoreCase)))
                .ToArray();

            var equalityComparer = new ColumnBasedRowEqualityComparer()
            {
                Columns = finalValueColumns,
            };

            if (builder.MatchColumns.Length == 1)
            {
                yield return new DeferredCompareWithRowOperation()
                {
                    InstanceName = nameof(AutoValidityRange),
                    If = row => !row.IsNullOrEmpty(builder.MatchColumns[0]),
                    RightProcessCreator = rows => CreateAutoValidity_ExpandDeferredReaderProcess(builder, builder.MatchColumns[0], finalValueColumns, rows),
                    LeftKeySelector = row => row.FormatToString(builder.MatchColumns[0]),
                    RightKeySelector = row => row.FormatToString(builder.MatchColumns[0]),
                    EqualityComparer = equalityComparer,
                    NoMatchAction = new NoMatchAction(MatchMode.Custom)
                    {
                        CustomAction = (op, row) =>
                        {
                            // this is the first version
                            row.SetValue(builder.TableBuilder.ValidFromColumnName, builder.TableBuilder.DwhBuilder.DefaultValidFromDateTime, op);
                            row.SetValue(builder.TableBuilder.ValidToColumnName, builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime, op);
                        }
                    },
                    MatchButDifferentAction = new MatchAction(MatchMode.Custom)
                    {
                        CustomAction = (op, row, match) =>
                        {
                            foreach (var kvp in builder.PreviousValueColumnNameMap)
                            {
                                var previousValue = match[kvp.Key];
                                row.SetValue(kvp.Value, previousValue, op);
                            }

                            row.SetValue(builder.TableBuilder.ValidFromColumnName, builder.TableBuilder.DwhBuilder.Context.CreatedOnLocal, op);
                            row.SetValue(builder.TableBuilder.ValidToColumnName, builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime, op);
                        },
                    },
                    MatchAndEqualsAction = new MatchAction(MatchMode.Remove)
                };
            }
            else
            {
                var parameters = new Dictionary<string, object>();
                if (builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime != null)
                    parameters.Add("InfiniteFuture", builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime);

                yield return new CompareWithRowOperation()
                {
                    InstanceName = nameof(AutoValidityRange),
                    RightProcess = new CustomSqlAdoNetDbReaderProcess(builder.TableBuilder.DwhBuilder.Context, "PreviousValueReader", builder.TableBuilder.Table.Topic)
                    {
                        ConnectionString = builder.TableBuilder.DwhBuilder.ConnectionString,
                        Sql = "SELECT " + string.Join(",", builder.MatchColumns.Concat(finalValueColumns).Select(x => builder.TableBuilder.DwhBuilder.ConnectionString.Escape(x)))
                            + " FROM " + builder.TableBuilder.DwhBuilder.ConnectionString.Escape(builder.TableBuilder.SqlTable.SchemaAndTableName.TableName, builder.TableBuilder.SqlTable.SchemaAndTableName.Schema)
                            + " WHERE " + builder.TableBuilder.ValidToColumnNameEscaped + (builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime == null ? " IS NULL" : "=@InfiniteFuture"),
                        Parameters = parameters,
                    },
                    LeftKeySelector = row => string.Join("\0", builder.MatchColumns.Select(c => row.FormatToString(c) ?? "-")),
                    RightKeySelector = row => string.Join("\0", builder.MatchColumns.Select(c => row.FormatToString(c) ?? "-")),
                    EqualityComparer = equalityComparer,
                    NoMatchAction = new NoMatchAction(MatchMode.Custom)
                    {
                        CustomAction = (op, row) =>
                        {
                            // this is the first version
                            row.SetValue(builder.TableBuilder.ValidFromColumnName, builder.TableBuilder.DwhBuilder.DefaultValidFromDateTime, op);
                            row.SetValue(builder.TableBuilder.ValidToColumnName, builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime, op);
                        }
                    },
                    MatchButDifferentAction = new MatchAction(MatchMode.Custom)
                    {
                        CustomAction = (op, row, match) =>
                        {
                            foreach (var kvp in builder.PreviousValueColumnNameMap)
                            {
                                var previousValue = match[kvp.Key];
                                row.SetValue(kvp.Value, previousValue, op);
                            }

                            row.SetValue(builder.TableBuilder.ValidFromColumnName, builder.TableBuilder.DwhBuilder.Context.CreatedOnLocal, op);
                            row.SetValue(builder.TableBuilder.ValidToColumnName, builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime, op);
                        },
                    },
                    MatchAndEqualsAction = new MatchAction(MatchMode.Remove)
                };
            }
        }

        private static CustomSqlAdoNetDbReaderProcess CreateAutoValidity_ExpandDeferredReaderProcess(AutoValidityRangeBuilder builder, string matchColumn, string[] valueColumns, IRow[] rows)
        {
            var parameters = new Dictionary<string, object>
            {
                ["keyList"] = rows
                    .Select(row => row.FormatToString(matchColumn))
                    .Distinct()
                    .ToArray(),
            };

            if (builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime != null)
                parameters.Add("InfiniteFuture", builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime);

            return new CustomSqlAdoNetDbReaderProcess(builder.TableBuilder.DwhBuilder.Context, "PreviousValueReader", builder.TableBuilder.Table.Topic)
            {
                ConnectionString = builder.TableBuilder.DwhBuilder.ConnectionString,
                Sql = "SELECT " + builder.TableBuilder.DwhBuilder.ConnectionString.Escape(matchColumn)
                    + "," + string.Join(", ", valueColumns.Select(c => builder.TableBuilder.DwhBuilder.ConnectionString.Escape(c)))
                    + " FROM " + builder.TableBuilder.DwhBuilder.ConnectionString.Escape(builder.TableBuilder.SqlTable.SchemaAndTableName.TableName, builder.TableBuilder.SqlTable.SchemaAndTableName.Schema)
                    + " WHERE "
                        + builder.TableBuilder.DwhBuilder.ConnectionString.Escape(matchColumn) + " IN (@keyList)"
                        + " and " + builder.TableBuilder.ValidToColumnNameEscaped + (builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime == null ? " IS NULL" : "=@InfiniteFuture"),
                InlineArrayParameters = true,
                Parameters = parameters,
            };
        }
    }
}