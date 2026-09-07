/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace BulkCrapUninstaller.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class ConfidenceBenchmarks
    {
        private ConfidenceValue[] _existing;
        private ConfidenceValue[] _incoming;

        [Params(50, 200, 500)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _existing = Enumerable.Range(0, Count)
                .Select(x => new ConfidenceValue(x, "existing-" + x))
                .ToArray();
            _incoming = Enumerable.Range(Count / 2, Count)
                .Select(x => new ConfidenceValue(x, x < Count ? "existing-" + x : "incoming-" + x))
                .ToArray();
        }

        [Benchmark(Baseline = true)]
        public int RepeatedListContains()
        {
            var target = new List<ConfidenceValue>(_existing);
            foreach (var value in _incoming)
            {
                if (!target.Contains(value))
                    target.Add(value);
            }
            return target.Count;
        }

        [Benchmark]
        public int HashSetBackedAddRange()
        {
            var target = new List<ConfidenceValue>(_existing);
            var seen = new HashSet<ConfidenceValue>(target);
            foreach (var value in _incoming)
            {
                if (seen.Add(value))
                    target.Add(value);
            }
            return target.Count;
        }

        private sealed record ConfidenceValue(int Change, string Reason);
    }
}
