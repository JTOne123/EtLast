﻿namespace FizzCode.EtLast
{
    public class CustomOperation : AbstractRowOperation
    {
        public RowTestDelegate If { get; set; }
        public RowActionDelegate Then { get; set; }
        public RowActionDelegate Else { get; set; }

        public override void Apply(IRow row)
        {
            if (If != null)
            {
                var result = If.Invoke(row);
                if (result)
                {
                    Then.Invoke(this, row);
                    Stat.IncrementDebugCounter("then executed", 1);
                }
                else if (Else != null)
                {
                    Else.Invoke(this, row);
                    Stat.IncrementDebugCounter("else executed", 1);
                }
            }
            else
            {
                Then.Invoke(this, row);
                Stat.IncrementDebugCounter("then executed", 1);
            }
        }

        public override void Prepare()
        {
            if (Then == null)
                throw new OperationParameterNullException(this, nameof(Then));
            if (Else != null && If == null)
                throw new OperationParameterNullException(this, nameof(If));
        }
    }
}