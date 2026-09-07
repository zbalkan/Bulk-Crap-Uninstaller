/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;

namespace UninstallTools.Junk.Confidence
{
    /// <summary>
    /// Finds many ordinal, case-sensitive substring patterns in a set of input strings
    /// without rescanning every input for every pattern.
    /// </summary>
    internal sealed class StringSearchIndex
    {
        private readonly Node _root = new();

        internal StringSearchIndex(IEnumerable<string> patterns)
        {
            if (patterns == null) throw new ArgumentNullException(nameof(patterns));

            foreach (var pattern in patterns)
                Add(pattern ?? throw new ArgumentException("Patterns cannot contain null values.", nameof(patterns)));

            BuildFailureLinks();
        }

        internal HashSet<string> FindMatches(IEnumerable<string> inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            var matches = new HashSet<string>(StringComparer.Ordinal);
            foreach (var input in inputs)
            {
                if (input == null) continue;

                var node = _root;
                AddOutputs(node, matches);
                foreach (var character in input)
                {
                    while (true)
                    {
                        if (node.Children.TryGetValue(character, out var next))
                        {
                            node = next;
                            break;
                        }

                        if (ReferenceEquals(node, _root))
                            break;

                        node = node.Failure;
                    }

                    AddOutputs(node, matches);
                }
            }

            return matches;
        }

        private void Add(string pattern)
        {
            var node = _root;
            foreach (var character in pattern)
            {
                if (!node.Children.TryGetValue(character, out var next))
                {
                    next = new Node();
                    node.Children.Add(character, next);
                }

                node = next;
            }

            if (!node.Outputs.Contains(pattern))
                node.Outputs.Add(pattern);
        }

        private void BuildFailureLinks()
        {
            _root.Failure = _root;
            var pending = new Queue<Node>();
            foreach (var child in _root.Children.Values)
            {
                child.Failure = _root;
                pending.Enqueue(child);
            }

            while (pending.Count > 0)
            {
                var parent = pending.Dequeue();
                foreach (var pair in parent.Children)
                {
                    var failure = parent.Failure;
                    while (!ReferenceEquals(failure, _root) && !failure.Children.ContainsKey(pair.Key))
                        failure = failure.Failure;

                    if (failure.Children.TryGetValue(pair.Key, out var fallback) && !ReferenceEquals(fallback, pair.Value))
                        pair.Value.Failure = fallback;
                    else
                        pair.Value.Failure = _root;

                    foreach (var output in pair.Value.Failure.Outputs)
                    {
                        if (!pair.Value.Outputs.Contains(output))
                            pair.Value.Outputs.Add(output);
                    }

                    pending.Enqueue(pair.Value);
                }
            }
        }

        private static void AddOutputs(Node node, HashSet<string> matches)
        {
            matches.UnionWith(node.Outputs);
        }

        private sealed class Node
        {
            internal Dictionary<char, Node> Children { get; } = new();
            internal List<string> Outputs { get; } = new();
            internal Node Failure { get; set; }
        }
    }
}
