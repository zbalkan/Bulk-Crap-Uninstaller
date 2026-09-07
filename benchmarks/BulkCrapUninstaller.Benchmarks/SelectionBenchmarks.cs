/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System.Linq;
using BenchmarkDotNet.Attributes;

namespace BulkCrapUninstaller.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class SelectionBenchmarks
    {
        private int[] _scores;

        [Params(100, 500, 2000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _scores = [.. Enumerable.Range(0, Count).Select(x => (x * 7919) % 101)];
        }

        [Benchmark(Baseline = true)]
        public int SortToFindMaximum()
        {
            return _scores.Where(x => x >= 1).OrderByDescending(x => x).FirstOrDefault();
        }

        [Benchmark]
        public int SinglePassMaximum()
        {
            var best = 0;
            foreach (var score in _scores)
            {
                if (score > best) best = score;
            }
            return best;
        }
    }
}
