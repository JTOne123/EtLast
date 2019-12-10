﻿namespace FizzCode.EtLast
{
    using System;
    using System.Diagnostics;

    public abstract class AbstractExecutableProcess : AbstractProcess, IExecutable
    {
        protected AbstractExecutableProcess(IEtlContext context, string name = null)
            : base(context, name)
        {
        }

        public void Execute(IProcess caller = null)
        {
            LastInvocation = Stopwatch.StartNew();
            Caller = caller;

            Validate();

            if (Context.CancellationTokenSource.IsCancellationRequested)
                return;

            if (If?.Invoke(this) == false)
                return;

            try
            {
                ExecuteImpl();
            }
            catch (EtlException ex) { Context.AddException(this, ex); }
            catch (Exception ex) { Context.AddException(this, new ProcessExecutionException(this, ex)); }
        }

        protected abstract void ExecuteImpl();
    }
}