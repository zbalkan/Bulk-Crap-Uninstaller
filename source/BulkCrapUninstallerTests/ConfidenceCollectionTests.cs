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
    public class ConfidenceCollectionTests
    {
        [TestMethod]
        public void AddRange_PreservesExistingDuplicatesAndRejectsNewDuplicates()
        {
            var existing = new ConfidenceRecord(1, "same");
            var collection = new ConfidenceCollection();
            collection.Add(existing);
            collection.Add(existing);

            collection.AddRange(new[]
            {
                new ConfidenceRecord(1, "same"),
                new ConfidenceRecord(2, "new"),
                new ConfidenceRecord(2, "new")
            });

            Assert.AreEqual(3, collection.Count());
        }

        [TestMethod]
        public void AddRange_SupportsRecordsWithoutReasons()
        {
            var collection = new ConfidenceCollection();

            collection.AddRange(new[] { new ConfidenceRecord(1), new ConfidenceRecord(1) });

            Assert.AreEqual(1, collection.Count());
        }
    }
}
