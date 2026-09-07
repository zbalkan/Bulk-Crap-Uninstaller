/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Klocman.Extensions;
using Klocman.Forms.Tools;
using Klocman.IO;
using Klocman.Tools;
using UninstallTools.Factory.InfoAdders;
using UninstallTools.Properties;
using UninstallTools.Startup;

namespace UninstallTools.Factory
{
    public static class ApplicationUninstallerFactory
    {
        private static readonly InfoAdderManager InfoAdder = new();

        public static IList<ApplicationUninstallerEntry> GetUninstallerEntries(ListGenerationProgress.ListGenerationCallback callback)
        {
            var totalStopwatch = Stopwatch.StartNew();
            const int totalStepCount = 8;
            var currentStep = 1;

            var concurrentFactory = new ConcurrentApplicationFactory(GetMiscUninstallerEntries);

            try
            {
                // Find msi products ---------------------------------------------------------------------------------------
                var msiProgress = new ListGenerationProgress(currentStep++, totalStepCount, Localisation.Progress_MSI);
                callback(msiProgress);
                var msiStopwatch = Stopwatch.StartNew();
                var msiGuidCount = 0;
                var msiProducts = MsiTools.MsiEnumProducts().DoForEach(x =>
                {
                    msiProgress.Inner = new ListGenerationProgress(0, -1, string.Format(Localisation.Progress_MSI_sub, ++msiGuidCount));
                    callback(msiProgress);
                }).ToList();
                Debug.WriteLine($"[Performance] MSI product enumeration took {msiStopwatch.ElapsedMilliseconds}ms and returned {msiProducts.Count} entries");

                // Run some factories in a separate thread -----------------------------------------------------------------
                concurrentFactory.Start();
                // Start populating lookups now before they are needed since they can take a few seconds
                Task.Run(() =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    MsiTools.InitLookups();
                    Debug.WriteLine($"[Performance] MSI lookup initialization took {stopwatch.ElapsedMilliseconds}ms");
                });

                // Find stuff mentioned in registry ------------------------------------------------------------------------
                IList<ApplicationUninstallerEntry> registryResults;
                if (UninstallToolsGlobalConfig.ScanRegistry)
                {
                    var regProgress = new ListGenerationProgress(currentStep++, totalStepCount,
                        Localisation.Progress_Registry);
                    callback(regProgress);

                    var sw = Stopwatch.StartNew();
                    var registryFactory = new RegistryFactory(msiProducts);
                    registryResults = registryFactory.GetUninstallerEntries(report =>
                    {
                        regProgress.Inner = report;
                        callback(regProgress);
                    });
                    Debug.WriteLine($"[Performance] Factory {nameof(RegistryFactory)} took {sw.ElapsedMilliseconds}ms and returned {registryResults.Count} entries");

                    // Fill in install llocations for DirectoryFactory to improve speed and quality of results
                    if (UninstallToolsGlobalConfig.UninstallerFactoryCache != null)
                        ApplyCache(registryResults, UninstallToolsGlobalConfig.UninstallerFactoryCache, InfoAdder);

                    var installLocAddProgress = new ListGenerationProgress(currentStep++, totalStepCount, Localisation.Progress_GatherUninstallerInfo);
                    callback(installLocAddProgress);

                    var infoStopwatch = Stopwatch.StartNew();
                    FactoryThreadedHelpers.GenerateMissingInformation(registryResults, InfoAdder, null, true, report =>
                    {
                        installLocAddProgress.Inner = report;
                        callback(installLocAddProgress);
                    });
                    Debug.WriteLine($"[Performance] Initial registry information generation took {infoStopwatch.ElapsedMilliseconds}ms for {registryResults.Count} entries");
                }
                else
                {
                    registryResults = new List<ApplicationUninstallerEntry>();
                }

                // Look for entries on drives, based on info in registry. ----------------------------------------------------
                // Will introduce duplicates to already detected stuff. Need to check for duplicates with other entries later.
                IList<ApplicationUninstallerEntry> driveResults;
                if (UninstallToolsGlobalConfig.ScanDrives)
                {
                    var driveProgress = new ListGenerationProgress(currentStep++, totalStepCount, Localisation.Progress_DriveScan);
                    callback(driveProgress);

                    var sw = Stopwatch.StartNew();
                    var driveFactory = new DirectoryFactory(registryResults);
                    driveResults = driveFactory.GetUninstallerEntries(report =>
                    {
                        driveProgress.Inner = report;
                        callback(driveProgress);
                    });
                    Debug.WriteLine($"[Performance] Factory {nameof(DirectoryFactory)} took {sw.ElapsedMilliseconds}ms and returned {driveResults.Count} entries");
                }
                else
                {
                    driveResults = new List<ApplicationUninstallerEntry>();
                }

                // Join up with the thread ----------------------------------------------------------------------------------
                var miscProgress = new ListGenerationProgress(currentStep++, totalStepCount, Localisation.Progress_AppStores);
                callback(miscProgress);
                var otherResults = concurrentFactory.GetResults(callback, miscProgress);

                // Handle duplicate entries ----------------------------------------------------------------------------------
                var mergeProgress = new ListGenerationProgress(currentStep++, totalStepCount, Localisation.Progress_Merging);
                callback(mergeProgress);
                var mergedResults = registryResults.ToList();
                var mergeStopwatch = Stopwatch.StartNew();
                MergeResults(mergedResults, otherResults, report =>
                {
                    mergeProgress.Inner = report;
                    report.TotalCount *= 2;
                    report.Message = Localisation.Progress_Merging_Stores;
                    callback(mergeProgress);
                });
                Debug.WriteLine($"[Performance] Merging independent factory results took {mergeStopwatch.ElapsedMilliseconds}ms for {otherResults.Count} candidates");
                // Make sure to merge driveResults last
                mergeStopwatch.Restart();
                MergeResults(mergedResults, driveResults, report =>
                {
                    mergeProgress.Inner = report;
                    report.CurrentCount += report.TotalCount;
                    report.TotalCount *= 2;
                    report.Message = Localisation.Progress_Merging_Drives;
                    callback(mergeProgress);
                });
                Debug.WriteLine($"[Performance] Merging directory factory results took {mergeStopwatch.ElapsedMilliseconds}ms for {driveResults.Count} candidates");

                // Fill in any missing information -------------------------------------------------------------------------
                if (UninstallToolsGlobalConfig.UninstallerFactoryCache != null)
                    ApplyCache(mergedResults, UninstallToolsGlobalConfig.UninstallerFactoryCache, InfoAdder);

                var infoAddProgress = new ListGenerationProgress(currentStep++, totalStepCount, Localisation.Progress_GeneratingInfo);
                callback(infoAddProgress);
                var finalInfoStopwatch = Stopwatch.StartNew();
                FactoryThreadedHelpers.GenerateMissingInformation(mergedResults, InfoAdder, msiProducts, false, report =>
                {
                    infoAddProgress.Inner = report;
                    callback(infoAddProgress);
                });
                Debug.WriteLine($"[Performance] Final information generation took {finalInfoStopwatch.ElapsedMilliseconds}ms for {mergedResults.Count} entries");

                // Cache missing information to speed up future scans
                if (UninstallToolsGlobalConfig.UninstallerFactoryCache != null)
                {
                    var cacheStopwatch = Stopwatch.StartNew();
                    foreach (var entry in mergedResults)
                        UninstallToolsGlobalConfig.UninstallerFactoryCache.TryCacheItem(entry);

                    try
                    {
                        UninstallToolsGlobalConfig.UninstallerFactoryCache.Save();
                    }
                    catch (SystemException e)
                    {
                        Trace.WriteLine(@"Failed to save cache: " + e);
                    }
                    Debug.WriteLine($"[Performance] Updating and saving the factory cache took {cacheStopwatch.ElapsedMilliseconds}ms for {mergedResults.Count} entries");
                }

                // Detect startups and attach them to uninstaller entries ----------------------------------------------------
                var startupsProgress = new ListGenerationProgress(currentStep, totalStepCount, Localisation.Progress_Startup);
                callback(startupsProgress);
                var i = 0;
                var startupEntries = new List<StartupEntryBase>();
                foreach (var factory in StartupManager.Factories)
                {
                    startupsProgress.Inner = new ListGenerationProgress(i++, StartupManager.Factories.Count, factory.Key);
                    callback(startupsProgress);
                    try
                    {
                        var startupFactoryStopwatch = Stopwatch.StartNew();
                        startupEntries.AddRange(factory.Value());
                        Debug.WriteLine($"[Performance] Startup factory {factory.Key} took {startupFactoryStopwatch.ElapsedMilliseconds}ms");
                    }
                    catch (Exception ex)
                    {
                        PremadeDialogs.GenericError(ex);
                    }
                }

                startupsProgress.Inner = new ListGenerationProgress(1, 1, Localisation.Progress_Merging);
                callback(startupsProgress);
                try
                {
                    var startupMergeStopwatch = Stopwatch.StartNew();
                    AttachStartupEntries(mergedResults, startupEntries);
                    Debug.WriteLine($"[Performance] Attaching {startupEntries.Count} startup entries took {startupMergeStopwatch.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    PremadeDialogs.GenericError(ex);
                }

                return mergedResults;
            }
            finally
            {
                Debug.WriteLine($"[Performance] Complete application discovery took {totalStopwatch.ElapsedMilliseconds}ms");
                concurrentFactory.Dispose();
            }
        }

        /// <summary>
        /// Merge new results into the base list
        /// </summary>
        internal static void MergeResults(ICollection<ApplicationUninstallerEntry> baseEntries,
            ICollection<ApplicationUninstallerEntry> newResults, ListGenerationProgress.ListGenerationCallback progressCallback)
        {
            var newToAdd = new List<ApplicationUninstallerEntry>();
            var progress = 0;
            foreach (var entry in newResults)
            {
                progressCallback?.Invoke(new ListGenerationProgress(progress++, newResults.Count, null));

                var matchedEntry = baseEntries.Select(x => new { x, score = ApplicationEntryTools.AreEntriesRelated(x, entry) })
                    .Where(x => x.score >= 1)
                    .OrderByDescending(x => x.score)
                    .Select(x => x.x)
                    .FirstOrDefault();

                if (matchedEntry != null)
                {
                    // Prevent setting incorrect UninstallerType
                    if (matchedEntry.UninstallPossible)
                        entry.UninstallerKind = UninstallerType.Unknown;

                    InfoAdder.CopyMissingInformation(matchedEntry, entry);
                    continue;
                }

                // If the entry failed to match to anything, add it to the base results as new
                newToAdd.Add(entry);
            }

            foreach (var newEntry in newToAdd)
                baseEntries.Add(newEntry);
        }

        private static void ApplyCache(ICollection<ApplicationUninstallerEntry> baseEntries, ApplicationUninstallerFactoryCache cache, InfoAdderManager infoAdder)
        {
            var hits = 0;
            foreach (var entry in baseEntries)
            {
                var matchedEntry = cache.TryGetCachedItem(entry);
                if (matchedEntry != null)
                {
                    infoAdder.CopyMissingInformation(entry, matchedEntry);
                    hits++;
                }
                else
                {
                    Debug.WriteLine("Cache miss: " + entry.DisplayName);
                }
            }
            Trace.WriteLine($@"Cache hits: {hits}/{baseEntries.Count}");
        }

        private static List<ApplicationUninstallerEntry> GetMiscUninstallerEntries(ListGenerationProgress.ListGenerationCallback progressCallback)
        {
            var otherResults = new List<ApplicationUninstallerEntry>();

            var miscFactories = ReflectionTools.GetTypesImplementingBase<IIndependantUninstallerFactory>()
                .Attempt(Activator.CreateInstance)
                .Cast<IIndependantUninstallerFactory>()
                .Where(x => x.IsEnabled())
                .ToList();

            var progress = 0;
            foreach (var kvp in miscFactories)
            {
                progressCallback(new ListGenerationProgress(progress++, miscFactories.Count, kvp.DisplayName));
                try
                {
                    var factoryStopwatch = Stopwatch.StartNew();
                    var factoryResults = kvp.GetUninstallerEntries(null);
                    Debug.WriteLine($"[Performance] Factory {kvp.GetType().Name} took {factoryStopwatch.ElapsedMilliseconds}ms and returned {factoryResults.Count} entries");

                    var mergeStopwatch = Stopwatch.StartNew();
                    MergeResults(otherResults, factoryResults, null);
                    Debug.WriteLine($"[Performance] Merging factory {kvp.GetType().Name} results took {mergeStopwatch.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    PremadeDialogs.GenericError(ex);
                }
            }

            return otherResults;
        }

        /// <summary>
        /// Attach startup entries to uninstaller entries that are automatically detected as related.
        /// </summary>
        public static void AttachStartupEntries(IEnumerable<ApplicationUninstallerEntry> uninstallers, IEnumerable<StartupEntryBase> startupEntries)
        {
            // Using DoForEach to avoid multiple enumerations
            StartupManager.AssignStartupEntries(uninstallers
                .DoForEach(x => { if (x != null) x.StartupEntries = null; }), startupEntries);
        }
    }
}
