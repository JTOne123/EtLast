﻿namespace FizzCode.EtLast
{
    public class RemoveRowOperation : AbstractRowOperation
    {
        public IfRowDelegate If { get; set; }

        public override void Apply(IRow row)
        {
            if (If?.Invoke(row) == false)
                return;

            Process.RemoveRow(row, this);
        }

        public override void Prepare()
        {
        }
    }
}