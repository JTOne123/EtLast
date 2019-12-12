﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    public class DeferredCompareWithRowOperation : AbstractRowOperation, IDeferredRowOperation
    {
        public RowTestDelegate If { get; set; }

        /// <summary>
        /// The amount of rows processed in a batch. Default value is 1000.
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Forces the operation to process the accumulated batch after a fixed amount of time even if the batch is not reached <see cref="BatchSize"/> yet.
        /// </summary>
        public int ForceProcessBatchAfterMilliseconds { get; set; } = 200;

        public MatchKeySelector LeftKeySelector { get; set; }
        public MatchKeySelector RightKeySelector { get; set; }
        public Func<IRow[], IEvaluable> RightProcessCreator { get; set; }

        public IRowEqualityComparer EqualityComparer { get; set; }
        public MatchAction NoMatchAction { get; set; }
        public MatchAction MatchAction { get; set; }

        private readonly Dictionary<string, IRow> _lookup = new Dictionary<string, IRow>();
        private List<IRow> _batchRows;
        private Stopwatch _lastNewRowSeenOn;

        public override void Apply(IRow row)
        {
            if (If?.Invoke(row) == false)
            {
                CounterCollection.IncrementDebugCounter("ignored", 1);
                return;
            }

            if (row.DeferState == DeferState.None)
            {
                _lastNewRowSeenOn.Restart();
                _batchRows.Add(row);
            }

            var timeout = Process.ReadingInput
                ? ForceProcessBatchAfterMilliseconds
                : ForceProcessBatchAfterMilliseconds / 10;

            var processBatch = _batchRows.Count >= BatchSize || (_lastNewRowSeenOn.ElapsedMilliseconds >= timeout && _batchRows.Count > 0);
            if (processBatch)
            {
                ProcessRows();

                foreach (var batchRow in _batchRows)
                {
                    batchRow.DeferState = DeferState.DeferDone;
                }

                _batchRows.Clear();
                _lastNewRowSeenOn.Restart();
            }
            else if (row.DeferState == DeferState.None)
            {
                row.DeferState = DeferState.DeferWait; // prevent proceeding to the next operation
            }
        }

        public override void Prepare()
        {
            base.Prepare();
            if (MatchAction == null && NoMatchAction == null)
                throw new InvalidOperationParameterException(this, nameof(MatchAction) + "&" + nameof(NoMatchAction), null, "at least one of these parameters must be specified: " + nameof(MatchAction) + " or " + nameof(NoMatchAction));

            if (MatchAction?.Mode == MatchMode.Custom && MatchAction.CustomAction == null)
                throw new OperationParameterNullException(this, nameof(MatchAction) + "." + nameof(MatchAction.CustomAction));

            if (NoMatchAction?.Mode == MatchMode.Custom && NoMatchAction.CustomAction == null)
                throw new OperationParameterNullException(this, nameof(NoMatchAction) + "." + nameof(NoMatchAction.CustomAction));

            if (NoMatchAction != null && MatchAction != null && ((NoMatchAction.Mode == MatchMode.Remove && MatchAction.Mode == MatchMode.Remove) || (NoMatchAction.Mode == MatchMode.Throw && MatchAction.Mode == MatchMode.Throw)))
                throw new InvalidOperationParameterException(this, nameof(MatchAction) + "&" + nameof(NoMatchAction), null, "at least one of these parameters must use a different action moode: " + nameof(MatchAction) + " or " + nameof(NoMatchAction));

            if (EqualityComparer == null)
                throw new OperationParameterNullException(this, nameof(EqualityComparer));

            if (LeftKeySelector == null)
                throw new OperationParameterNullException(this, nameof(LeftKeySelector));

            if (RightKeySelector == null)
                throw new OperationParameterNullException(this, nameof(RightKeySelector));

            if (RightProcessCreator == null)
                throw new OperationParameterNullException(this, nameof(RightProcessCreator));

            _batchRows = new List<IRow>(BatchSize);
            _lastNewRowSeenOn = Stopwatch.StartNew();
        }

        private void ProcessRows()
        {
            CounterCollection.IncrementDebugCounter("processed", _batchRows.Count, true);
            CounterCollection.IncrementCounter("batches", 1, true);

            var rightProcess = RightProcessCreator.Invoke(_batchRows.ToArray());

            Process.Context.Log(LogSeverity.Debug, Process, this, "evaluating <{InputProcess}> to process {RowCount} rows", rightProcess.Name,
                _batchRows.Count);

            var rightRows = rightProcess.Evaluate(Process);
            var rightRowCount = 0;
            foreach (var row in rightRows)
            {
                rightRowCount++;
                var key = GetRightKey(row);
                if (string.IsNullOrEmpty(key))
                    continue;

                _lookup[key] = row;
            }

            Process.Context.Log(LogSeverity.Debug, Process, this, "fetched {RowCount} rows, lookup size is {LookupSize}", rightRowCount,
                _lookup.Count);

            CounterCollection.IncrementCounter("right rows loaded", rightRowCount, true);

            try
            {
                foreach (var row in _batchRows)
                {
                    var key = GetLeftKey(row);
                    if (key == null || !_lookup.TryGetValue(key, out var rightRow))
                    {
                        if (NoMatchAction != null)
                        {
                            HandleNoMatch(row, key, null);
                        }

                        return;
                    }

                    var match = EqualityComparer.Compare(row, rightRow);
                    if (!match)
                    {
                        if (NoMatchAction != null)
                        {
                            HandleNoMatch(row, key, rightRow);
                        }
                    }
                    else if (MatchAction != null)
                    {
                        HandleMatch(row, key, rightRow);
                    }
                }
            }
            finally
            {
                _lookup.Clear(); // no caching due to the 1:1 nature of the operation
            }
        }

        private void HandleMatch(IRow row, string key, IRow rightRow)
        {
            switch (MatchAction.Mode)
            {
                case MatchMode.Remove:
                    Process.RemoveRow(row, this);
                    break;
                case MatchMode.Throw:
                    var exception = new OperationExecutionException(Process, this, row, "match");
                    exception.Data.Add("Key", key);
                    throw exception;
                case MatchMode.Custom:
                    MatchAction.CustomAction.Invoke(this, row, rightRow);
                    break;
            }
        }

        private void HandleNoMatch(IRow row, string leftKey, IRow rightRow)
        {
            switch (NoMatchAction.Mode)
            {
                case MatchMode.Remove:
                    Process.RemoveRow(row, this);
                    break;
                case MatchMode.Throw:
                    var exception = new OperationExecutionException(Process, this, row, "no match");
                    exception.Data.Add("Key", leftKey);
                    throw exception;
                case MatchMode.Custom:
                    NoMatchAction.CustomAction.Invoke(this, row, rightRow);
                    break;
            }
        }

        public override void Shutdown()
        {
            _batchRows.Clear();
            _batchRows = null;

            _lastNewRowSeenOn = null;

            base.Shutdown();
        }

        protected string GetLeftKey(IRow row)
        {
            try
            {
                return LeftKeySelector(row);
            }
            catch (EtlException) { throw; }
            catch (Exception)
            {
                var exception = new OperationExecutionException(Process, this, row, nameof(LeftKeySelector) + " failed");
                throw exception;
            }
        }

        protected string GetRightKey(IRow row)
        {
            try
            {
                return RightKeySelector(row);
            }
            catch (EtlException) { throw; }
            catch (Exception)
            {
                var exception = new OperationExecutionException(Process, this, row, nameof(RightKeySelector) + " failed");
                throw exception;
            }
        }
    }
}