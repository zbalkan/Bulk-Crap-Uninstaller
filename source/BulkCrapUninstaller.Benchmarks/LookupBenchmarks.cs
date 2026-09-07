/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace BulkCrapUninstaller.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class LookupBenchmarks
    {
        private List<Entry> _entries;
        private Dictionary<string, Entry> _entriesById;
        private string[] _queries;

        [Params(100, 500, 2000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _entries = new List<Entry>(Count);
            _entriesById = new Dictionary<string, Entry>(Count, StringComparer.OrdinalIgnoreCase);
            _queries = new string[Count];

            for (var i = 0; i < Count; i++)
            {
                var entry = new Entry($"{{00000000-0000-0000-0000-{i:D12}}}");
                _entries.Add(entry);
                _entriesById.Add(entry.Id, entry);
                _queries[i] = entry.Id;
            }
        }

        [Benchmark(Baseline = true)]
        public int RepeatedLinearLookup()
        {
            var hits = 0;
            foreach (var query in _queries)
            {
                foreach (var entry in _entries)
                {
                    if (!string.Equals(entry.Id, query, StringComparison.OrdinalIgnoreCase)) continue;
                    hits++;
                    break;
                }
            }

            return hits;
        }

        [Benchmark]
        public int DictionaryLookup()
        {
            var hits = 0;
            foreach (var query in _queries)
            {
                if (_entriesById.ContainsKey(query))
                    hits++;
            }

            return hits;
        }

        private sealed record Entry(string Id);
    }
}
