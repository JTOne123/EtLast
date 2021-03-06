﻿namespace FizzCode.EtLast.DwhBuilder
{
    using System;
    using System.Runtime.InteropServices;

    [ComVisible(true)]
    [Serializable]
    public class DwhBuilderParameterNullException<TTableBuilder> : InvalidDwhBuilderParameterException<TTableBuilder>
        where TTableBuilder : IDwhTableBuilder
    {
        public DwhBuilderParameterNullException(IDwhBuilder<TTableBuilder> builder, string parameterName)
            : base(builder, parameterName, null, "value cannot be null or empty")
        {
        }
    }
}