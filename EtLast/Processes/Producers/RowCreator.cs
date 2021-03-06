﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Creates <see cref="IRow"/>s from <see cref="InputRows"/>.
    /// Use to create test input rows, or to inject litaral-like data.
    /// </summary>
    /// <remarks>Do not use to read into <see cref="InputRows"/> from a data source.</remarks>
    public class RowCreator : AbstractProducer
    {
        public string[] Columns { get; set; }
        public List<object[]> InputRows { get; set; } = new List<object[]>();
        public int BatchSize { get; set; } = 100;

        public RowCreator(ITopic topic, string name)
            : base(topic, name)
        {
        }

        protected override void ValidateImpl()
        {
            if (InputRows == null)
                throw new ProcessParameterNullException(this, nameof(InputRows));
        }

        protected override IEnumerable<IRow> Produce()
        {
            Context.Log(LogSeverity.Debug, this, "returning pre-defined rows");

            foreach (var inputRow in InputRows)
            {
                if (Context.CancellationTokenSource.IsCancellationRequested)
                    yield break;

                var initialValues = Enumerable
                    .Range(0, Math.Min(Columns.Length, inputRow.Length))
                    .Select(i => new KeyValuePair<string, object>(Columns[i], inputRow[i]))
                    .ToList();

                yield return Context.CreateRow(this, initialValues);
            }
        }
    }
}