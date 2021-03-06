﻿namespace FizzCode.EtLast
{
    using System.Collections.Generic;

    public class AddIncrementalIdMutator : AbstractMutator
    {
        public string Column { get; set; }

        /// <summary>
        /// Default value is 0.
        /// </summary>
        public int FirstId { get; set; }

        private int _nextId;

        public AddIncrementalIdMutator(ITopic topic, string name)
            : base(topic, name)
        {
        }

        protected override void StartMutator()
        {
            _nextId = FirstId;
        }

        protected override IEnumerable<IRow> MutateRow(IRow row)
        {
            row.SetValue(Column, _nextId);
            _nextId++;
            yield return row;
        }

        protected override void ValidateMutator()
        {
            if (string.IsNullOrEmpty(Column))
                throw new ProcessParameterNullException(this, nameof(Column));
        }
    }
}