﻿namespace FizzCode.EtLast.Diagnostics.Interface
{
    using System.Collections.Generic;

    public class RowCreatedEvent : AbstractRowEvent
    {
        public int ProcessInvocationUID { get; set; }
        public KeyValuePair<string, object>[] Values { get; set; }
    }
}