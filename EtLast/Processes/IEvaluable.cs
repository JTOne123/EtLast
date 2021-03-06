﻿namespace FizzCode.EtLast
{
    using System.Collections.Generic;

    public delegate IEnumerable<IRow> EvaluateDelegate(IProcess caller);

    public interface IEvaluable : IExecutable
    {
        /// <summary>
        /// Some consumer processes use buffering to process the rows enumerated from their input.
        /// If a process can only return data really slowly by design then it should allow the consumer to process the rows immediately by setting this value to true.
        /// </summary>
        bool ConsumerShouldNotBuffer { get; }

        Evaluator Evaluate(IProcess caller = null);
    }
}