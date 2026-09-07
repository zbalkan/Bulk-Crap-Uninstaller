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
    public class PatternSearchBenchmarks
    {
        private string[] _patterns;
        private string[] _inputs;
        private StringSearchIndex _index;

        [Params(25, 100, 400)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _patterns = [.. Enumerable.Range(0, Count).Select(x => $"application-{x:D4}")];
            _inputs = [.. Enumerable.Range(Count / 2, Count).Select(x => $"vendor application-{x:D4} component")];
            _index = new StringSearchIndex(_patterns);
        }

        [Benchmark(Baseline = true)]
        public HashSet<string> NestedContains()
        {
            var matches = new HashSet<string>();
            foreach (var pattern in _patterns)
            {
                if (_inputs.Any(input => input.Contains(pattern)))
                    matches.Add(pattern);
            }
            return matches;
        }

        [Benchmark]
        public HashSet<string> IndexedSearch()
        {
            return _index.FindMatches(_inputs);
        }
    }
}
