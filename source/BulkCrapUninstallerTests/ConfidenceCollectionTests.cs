/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

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

            Assert.HasCount(3, collection);
        }

        [TestMethod]
        public void AddRange_SupportsRecordsWithoutReasons()
        {
            var collection = new ConfidenceCollection();

            collection.AddRange(new[] { new ConfidenceRecord(1), new ConfidenceRecord(1) });

            Assert.HasCount(1, collection);
        }
    }
}
