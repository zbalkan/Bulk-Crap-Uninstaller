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
    public class CollectionBenchmarks
    {
        private List<int> _items;
        private HashSet<int> _itemSet;
        private int[] _queries;

        [Params(100, 500, 2000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _items = Enumerable.Range(0, Count).ToList();
            _itemSet = new HashSet<int>(_items);
            _queries = Enumerable.Range(Count / 2, Count).ToArray();
        }

        [Benchmark(Baseline = true)]
        public int ListMembership()
        {
            var hits = 0;
            foreach (var query in _queries)
            {
                if (_items.Contains(query)) hits++;
            }
            return hits;
        }

        [Benchmark]
        public int HashSetMembership()
        {
            var hits = 0;
            foreach (var query in _queries)
            {
                if (_itemSet.Contains(query)) hits++;
            }
            return hits;
        }
    }
}
