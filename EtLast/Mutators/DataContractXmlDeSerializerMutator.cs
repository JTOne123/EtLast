﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Xml;

    public class DataContractXmlDeSerializerMutator<T> : AbstractMutator
    {
        public ColumnCopyConfiguration ColumnConfiguration { get; set; }

        public InvalidValueAction ActionIfFailed { get; set; }
        public object SpecialValueIfFailed { get; set; }

        public DataContractXmlDeSerializerMutator(ITopic topic, string name)
            : base(topic, name)
        {
        }

        protected override IEnumerable<IRow> MutateRow(IRow row)
        {
            var sourceByteArray = row.GetAs<byte[]>(ColumnConfiguration.FromColumn);

            if (sourceByteArray == null)
            {
                yield return row;
                yield break;
            }

            var removeRow = false;
            try
            {
                using (var ms = new MemoryStream(sourceByteArray))
                {
                    object obj = null;
                    using (var reader = XmlDictionaryReader.CreateTextReader(sourceByteArray, XmlDictionaryReaderQuotas.Max))
                    {
                        var ser = new DataContractSerializer(typeof(T));
                        obj = ser.ReadObject(reader, true);
                    }

                    row.SetValue(ColumnConfiguration.ToColumn, obj);
                }
            }
            catch (Exception ex)
            {
                switch (ActionIfFailed)
                {
                    case InvalidValueAction.SetSpecialValue:
                        row.SetValue(ColumnConfiguration.ToColumn, SpecialValueIfFailed);
                        break;
                    case InvalidValueAction.Throw:
                        throw new ProcessExecutionException(this, row, "DataContract XML deserialization failed", ex);
                    case InvalidValueAction.RemoveRow:
                        removeRow = true;
                        break;
                    case InvalidValueAction.WrapError:
                        row.SetValue(ColumnConfiguration.ToColumn, new EtlRowError
                        {
                            Process = this,
                            OriginalValue = null,
                            Message = "DataContract XML deserialization failed: " + ex.Message,
                        });
                        break;
                }
            }

            if (!removeRow)
                yield return row;
        }

        protected override void ValidateMutator()
        {
            if (ColumnConfiguration == null)
                throw new ProcessParameterNullException(this, nameof(ColumnConfiguration));
        }
    }
}