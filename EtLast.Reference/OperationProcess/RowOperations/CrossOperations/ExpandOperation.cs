﻿namespace FizzCode.EtLast
{
    using System.Collections.Generic;

    public class ExpandOperation : AbstractKeyBasedCrossOperation
    {
        public NoMatchMode Mode { get; set; }
        public RowTestDelegate If { get; set; }
        public List<ColumnCopyConfiguration> ColumnConfiguration { get; set; }
        private readonly Dictionary<string, IRow> _lookup = new Dictionary<string, IRow>();

        public ExpandOperation(NoMatchMode mode)
        {
            Mode = mode;
        }

        public override void Apply(IRow row)
        {
            if (If?.Invoke(row) == false)
            {
                Stat.IncrementCounter("ignored", 1);
                return;
            }

            Stat.IncrementCounter("processed", 1);

            var leftKey = GetLeftKey(Process, row);
            if (leftKey == null || !_lookup.TryGetValue(leftKey, out var rightRow))
            {
                if (Mode == NoMatchMode.Remove)
                {
                    Process.RemoveRow(row, this);
                }
                else if (Mode == NoMatchMode.Throw)
                {
                    var exception = new OperationExecutionException(Process, this, row, "no right row found");
                    exception.Data.Add("LeftKey", leftKey);
                    throw exception;
                }

                return;
            }

            foreach (var config in ColumnConfiguration)
            {
                config.Copy(this, rightRow, row);
            }
        }

        public override void Prepare()
        {
            base.Prepare();
            if (ColumnConfiguration == null)
                throw new OperationParameterNullException(this, nameof(ColumnConfiguration));

            Process.Context.Log(LogSeverity.Debug, Process, "{OperationName} getting right rows from {InputProcess}", Name, RightProcess.Name);
            _lookup.Clear();
            var rows = RightProcess.Evaluate(Process);
            var rowCount = 0;
            foreach (var row in rows)
            {
                rowCount++;
                var key = GetRightKey(Process, row);
                if (string.IsNullOrEmpty(key))
                    continue;

                _lookup[key] = row;
            }

            Process.Context.Log(LogSeverity.Debug, Process, "{OperationName} fetched {RowCount} rows, lookup size is {LookupSize}", Name, rowCount, _lookup.Count);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _lookup.Clear();
        }
    }
}