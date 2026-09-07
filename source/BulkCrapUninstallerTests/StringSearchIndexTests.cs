/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UninstallTools.Junk.Confidence;

namespace BulkCrapUninstallerTests
{
    [TestClass]
    public class StringSearchIndexTests
    {
        [TestMethod]
        public void FindMatches_ReturnsOverlappingPatterns()
        {
            var index = new StringSearchIndex(new[] { "app", "application", "cat", "missing" });

            var result = index.FindMatches(new[] { "my application catalog" });

            CollectionAssert.AreEquivalent(new[] { "app", "application", "cat" }, result.ToArray());
        }

        [TestMethod]
        public void FindMatches_IsOrdinalAndCaseSensitive()
        {
            var index = new StringSearchIndex(new[] { "App", "app" });

            var result = index.FindMatches(new[] { "Application" });

            CollectionAssert.AreEquivalent(new[] { "App" }, result.ToArray());
        }

        [TestMethod]
        public void FindMatches_HandlesDuplicateAndEmptyPatterns()
        {
            var index = new StringSearchIndex(new[] { string.Empty, "app", "app" });

            var result = index.FindMatches(new[] { "unrelated" });

            CollectionAssert.AreEquivalent(new[] { string.Empty }, result.ToArray());
        }
    }
}
