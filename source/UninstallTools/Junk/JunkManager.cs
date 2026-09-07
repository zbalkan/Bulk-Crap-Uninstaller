/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Klocman.Extensions;
using Klocman.Forms.Tools;
using Klocman.Tools;
using UninstallTools.Junk.Containers;
using UninstallTools.Properties;

namespace UninstallTools.Junk
{
    public static class JunkManager
    {
        private static IEnumerable<IJunkResult> CleanUpResults(IEnumerable<IJunkResult> input)
        {
            var prohibitedLocations = GetProhibitedLocations();

            return RemoveDuplicates(input)
                .Where(x => JunkDoesNotPointToDirectories(x, prohibitedLocations))
                .Where(JunkDoesNotPointToSelf);
        }

        private static IEnumerable<IJunkResult> CleanUpResultsWithPerformance(
            ICollection<IJunkResult> input, Stopwatch totalStopwatch, string operationName)
        {
            var cleanupStopwatch = Stopwatch.StartNew();
            var outputCount = 0;
            foreach (var result in CleanUpResults(input))
            {
                outputCount++;
                yield return result;
            }

            Debug.WriteLine($"[Performance] {operationName} cleanup took {cleanupStopwatch.ElapsedMilliseconds}ms and reduced {input.Count} results to {outputCount}");
            Debug.WriteLine($"[Performance] Complete {operationName} took {totalStopwatch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Make sure that the junk result doesn't point to this application.
        /// </summary>
        private static bool JunkDoesNotPointToSelf(IJunkResult x)
        {
            if (x is FileSystemJunk fileSystemJunk)
            {
                return fileSystemJunk.Path == null || 
                       !fileSystemJunk.Path.FullName.StartsWith(UninstallToolsGlobalConfig.AppLocation, StringComparison.OrdinalIgnoreCase);
            }

            if (x is StartupJunkNode startupJunk)
            {
                return startupJunk.Entry?.CommandFilePath == null || 
                       !startupJunk.Entry.CommandFilePath.StartsWith(UninstallToolsGlobalConfig.AppLocation, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        /// <summary>
        /// Merge duplicate junk entries and their confidence parts
        /// </summary>
        private static IEnumerable<IJunkResult> RemoveDuplicates(IEnumerable<IJunkResult> input)
        {
            foreach (var appGroup in input.GroupBy(x => x.Application))
            {
                foreach (var group in appGroup.GroupBy(x => PathTools.NormalizePath(x.GetDisplayName()).ToLowerInvariant()))
                {
                    IJunkResult firstJunkResult = null;
                    foreach (var junkResult in group)
                    {
                        if (firstJunkResult == null)
                            firstJunkResult = junkResult;
                        else
                            firstJunkResult.Confidence.AddRange(junkResult.Confidence.ConfidenceParts);
                    }

                    if (firstJunkResult != null)
                        yield return firstJunkResult;
                }
            }
        }

        private static bool JunkDoesNotPointToDirectories(IJunkResult arg, HashSet<string> prohibitedDirs)
        {
            if (arg is not FileSystemJunk fileSystemJunk)
                return true;

            return !prohibitedDirs.Contains(fileSystemJunk.Path.FullName.ToLowerInvariant());
        }

        /// <summary>
        /// Prevent suggesting removing special directories if the app for some reason was installed into them or otherwise used them
        /// </summary>
        private static HashSet<string> GetProhibitedLocations()
        {
            var results = new HashSet<string>();

            void AddRange(IEnumerable<string> paths)
            {
                foreach (var path in paths
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Attempt(System.IO.Path.GetFullPath)
                    .Select(x => x.ToLowerInvariant()))
                {
                    results.Add(path);
                }
            }

            AddRange(Enum.GetValues<Klocman.Native.CSIDL>().Attempt(WindowsTools.GetEnvironmentPath));

            var knownFolderstype = Type.GetType("Windows.Storage.KnownFolders, Microsoft.Windows.SDK.NET", false);
            // Might not be available on some systems
            if (knownFolderstype != null)
            {
                try
                {
                    AddRange(knownFolderstype.GetProperties().Attempt(p => ((Windows.Storage.StorageFolder)p.GetValue(null))!.Path));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("Failed to collect KnownFolders: " + ex);
                }
            }

            return results;
        }

        public static IEnumerable<IJunkResult> FindJunk(IEnumerable<ApplicationUninstallerEntry> targets,
            ICollection<ApplicationUninstallerEntry> allUninstallers, ListGenerationProgress.ListGenerationCallback progressCallback)
        {
            var totalStopwatch = Stopwatch.StartNew();
            progressCallback(new ListGenerationProgress(-1, 0, Localisation.Junk_Progress_Startup));

            var discoveryStopwatch = Stopwatch.StartNew();
            var scanners = ReflectionTools.GetTypesImplementingBase<IJunkCreator>()
                .Attempt(Activator.CreateInstance)
                .Cast<IJunkCreator>()
                .ToList();
            Debug.WriteLine($"[Performance] Junk scanner discovery took {discoveryStopwatch.ElapsedMilliseconds}ms and found {scanners.Count} scanners");

            foreach (var junkCreator in scanners)
            {
                var setupStopwatch = Stopwatch.StartNew();
                junkCreator.Setup(allUninstallers);
                Debug.WriteLine($"[Performance] Scanner {junkCreator.GetType().Name} setup took {setupStopwatch.ElapsedMilliseconds}ms");
            }

            var results = new List<IJunkResult>();
            var targetEntries = targets as IList<ApplicationUninstallerEntry> ?? targets.ToList();
            var progress = 0;
            foreach (var junkCreator in scanners)
            {
                var scannerStopwatch = Stopwatch.StartNew();
                var initialResultCount = results.Count;
                var scannerProgress = new ListGenerationProgress(progress++, scanners.Count, junkCreator.CategoryName);

                var entryProgress = 0;
                foreach (var target in targetEntries)
                {
                    scannerProgress.Inner = new ListGenerationProgress(entryProgress++, targetEntries.Count, target.DisplayName);
                    progressCallback(scannerProgress);

                    try { results.AddRange(junkCreator.FindJunk(target)); }
                    catch (SystemException ex) { PremadeDialogs.GenericError(ex); }
                }
                Debug.WriteLine($"[Performance] Scanner {junkCreator.GetType().Name} scan took {scannerStopwatch.ElapsedMilliseconds}ms for {targetEntries.Count} targets and returned {results.Count - initialResultCount} results");
            }

            progressCallback(new ListGenerationProgress(-1, 0, Localisation.Junk_Progress_Finishing));

            foreach (var target in targetEntries)
                results.AddRange(target.AdditionalJunk);

            return CleanUpResultsWithPerformance(results, totalStopwatch, "junk scan");
        }

        public static IEnumerable<IJunkResult> FindProgramFilesJunk(
            ICollection<ApplicationUninstallerEntry> allUninstallers)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var pfScanner = new ProgramFilesOrphans();
            var setupStopwatch = Stopwatch.StartNew();
            pfScanner.Setup(allUninstallers);
            Debug.WriteLine($"[Performance] Scanner {nameof(ProgramFilesOrphans)} setup took {setupStopwatch.ElapsedMilliseconds}ms");

            var scanStopwatch = Stopwatch.StartNew();
            var results = pfScanner.FindAllJunk().ToList();
            Debug.WriteLine($"[Performance] Scanner {nameof(ProgramFilesOrphans)} scan took {scanStopwatch.ElapsedMilliseconds}ms and returned {results.Count} results");

            return CleanUpResultsWithPerformance(results, totalStopwatch, "orphaned Program Files scan");
        }
    }
}
