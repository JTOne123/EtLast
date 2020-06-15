﻿namespace FizzCode.EtLast
{
    public class ProcessBuilder
    {
        public IEvaluable InputProcess { get; set; }
        public MutatorList Mutators { get; set; }

        public IEvaluable Build()
        {
            if (InputProcess == null)
                throw new ParameterNullException(nameof(ProcessBuilder), nameof(InputProcess));

            if (Mutators == null || Mutators.Count == 0)
                return InputProcess;

            var last = InputProcess;
            foreach (var list in Mutators)
            {
                if (list != null)
                {
                    foreach (var mutator in list)
                    {
                        if (mutator != null)
                        {
                            mutator.InputProcess = last;
                            last = mutator;
                        }
                    }
                }
            }

            return last;
        }
    }
}