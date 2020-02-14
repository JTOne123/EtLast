﻿namespace FizzCode.EtLast.DwhBuilder.Alpha
{
    using System.Collections.Generic;
    using System.Linq;
    using FizzCode.EtLast;

    public static partial class TableBuilderExtensions
    {
        public static DwhTableBuilder[] TrimAllStringColumnLength(this DwhTableBuilder[] builders)
        {
            foreach (var builder in builders)
            {
                builder.AddMutatorCreator(CreateTrimAllStringColumnLength);
            }

            return builders;
        }

        private static IEnumerable<IMutator> CreateTrimAllStringColumnLength(DwhTableBuilder builder)
        {
            var limitedLengthStringColumns = builder.SqlTable.Columns.Where(x => x.Type.Length != null && x.Type.Length.Value != -1 &&
                (x.Type.SqlTypeInfo == DbTools.DataDefinition.MsSqlType2016.VarChar
                || x.Type.SqlTypeInfo == DbTools.DataDefinition.MsSqlType2016.NVarChar
                || x.Type.SqlTypeInfo == DbTools.DataDefinition.MsSqlType2016.NChar
                || x.Type.SqlTypeInfo == DbTools.DataDefinition.MsSqlType2016.Char))
                .ToList();

            if (limitedLengthStringColumns.Count == 0)
                yield break;

            yield return new CustomMutator(builder.DwhBuilder.Context, nameof(TrimAllStringColumnLength), builder.Topic)
            {
                Then = (proc, row) =>
                {
                    foreach (var col in limitedLengthStringColumns)
                    {
                        var v = row[col.Name];
                        if (v == null)
                            continue;

                        if (!(v is string strv))
                            continue;

                        if (strv.Length > col.Type.Length.Value)
                        {
                            var trimv = strv.Substring(0, col.Type.Length.Value);
                            row.SetStagedValue(col.Name, trimv);

                            proc.Context.Log(LogSeverity.Warning, proc, "too long string trimmed on {ConnectionStringName}/{TableName}, column: {Column}, max length: {MaxLength}, original value: {Value}, trimmed value: {TrimValue}",
                                builder.DwhBuilder.ConnectionString.Name, builder.Table.TableName, col.Name, col.Type.Length.Value, strv, trimv);
                        }
                    }

                    row.ApplyStaging(proc);

                    return true;
                }
            };
        }
    }
}