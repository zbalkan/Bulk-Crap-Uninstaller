/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using UninstallTools.Junk.Confidence;

namespace BulkCrapUninstaller.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class ConfidenceBenchmarks
    {
        private ConfidenceRecord[] _existing;
        private ConfidenceRecord[] _incoming;

        [Params(50, 200, 500)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _existing = Enumerable.Range(0, Count)
                .Select(x => new ConfidenceRecord(x, "existing-" + x))
                .ToArray();
            _incoming = Enumerable.Range(Count / 2, Count)
                .Select(x => new ConfidenceRecord(x, x < Count ? "existing-" + x : "incoming-" + x))
                .ToArray();
        }

        [Benchmark(Baseline = true)]
        public int RepeatedListContains()
        {
            var target = new List<ConfidenceRecord>(_existing);
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
            var target = new ConfidenceCollection();
            foreach (var value in _existing)
                target.Add(value);
            target.AddRange(_incoming);
            return target.Count();
        }
    }
}
