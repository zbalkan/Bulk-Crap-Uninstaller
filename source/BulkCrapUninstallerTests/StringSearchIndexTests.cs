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

            Assert.AreSequenceEqual(new[] { "app", "application", "cat" }, result.ToArray(), SequenceOrder.InAnyOrder);
        }

        [TestMethod]
        public void FindMatches_IsOrdinalAndCaseSensitive()
        {
            var index = new StringSearchIndex(new[] { "App", "app" });

            var result = index.FindMatches(new[] { "Application" });

            Assert.AreSequenceEqual(new[] { "App" }, result.ToArray(), SequenceOrder.InAnyOrder);
        }

        [TestMethod]
        public void FindMatches_HandlesDuplicateAndEmptyPatterns()
        {
            var index = new StringSearchIndex(new[] { string.Empty, "app", "app" });

            var result = index.FindMatches(new[] { "unrelated" });

            Assert.AreSequenceEqual(new[] { string.Empty }, result.ToArray(), SequenceOrder.InAnyOrder);
        }
    }
}
