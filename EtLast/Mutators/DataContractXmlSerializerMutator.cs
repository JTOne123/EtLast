﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Xml;

    public class DataContractXmlSerializerMutator<T> : AbstractMutator
    {
        public ColumnCopyConfiguration ColumnConfiguration { get; set; }

        public InvalidValueAction ActionIfFailed { get; set; }
        public object SpecialValueIfFailed { get; set; }

        public DataContractXmlSerializerMutator(ITopic topic, string name)
            : base(topic, name)
        {
        }

        protected override IEnumerable<IRow> MutateRow(IRow row)
        {
            var sourceObject = row.GetAs<T>(ColumnConfiguration.FromColumn);
            if (sourceObject == null)
            {
                yield return row;
                yield break;
            }

            var removeRow = false;
            try
            {
                using (var ms = new MemoryStream())
                {
                    using (var writer = XmlDictionaryWriter.CreateTextWriter(ms))
                    {
                        var ser = new DataContractSerializer(typeof(T));
                        ser.WriteObject(writer, sourceObject);
                    }

                    var data = ms.ToArray();
                    row.SetValue(ColumnConfiguration.ToColumn, data);
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
                        throw new ProcessExecutionException(this, row, "DataContract XML serialization failed", ex);
                    case InvalidValueAction.RemoveRow:
                        removeRow = true;
                        break;
                    case InvalidValueAction.WrapError:
                        row.SetValue(ColumnConfiguration.ToColumn, new EtlRowError
                        {
                            Process = this,
                            OriginalValue = null,
                            Message = "DataContract XML serialization failed: " + ex.Message,
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