using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using chocolatey;
using chocolatey.infrastructure.app.domain;
using chocolatey.infrastructure.results;
using Klocman.Extensions;
using Klocman.IO;
using Klocman.Tools;
using UninstallTools.Junk.Containers;
using UninstallTools.Junk.Confidence;
using UninstallTools.Properties;

namespace UninstallTools.Factory
{
    public sealed class ChocolateyFactory : IIndependantUninstallerFactory
    {
        private static GetChocolatey choco;
        private static bool? chocoIsAvailable;
        private static string chocoFullFilename;

        public IList<ApplicationUninstallerEntry> GetUninstallerEntries(ListGenerationProgress.ListGenerationCallback progressCallback = null)
        {
            var results = new List<ApplicationUninstallerEntry>();
            if (!IsChocoAvailable()) return results;

            InitiateChocolatey();
            var sw = Stopwatch.StartNew();
            var packages = choco.List<PackageResult>();
            Console.WriteLine($"[Performance] Retrieving package list took {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            packages.AsParallel().AsOrdered().ForEach(package => results.Add(ConvertToEntry(package)));
            Console.WriteLine($"[Performance] PARALLEL: Mapping PackageResult to Application for entry took {sw.ElapsedMilliseconds}ms");

            return results;
        }

        public bool IsEnabled() => UninstallToolsGlobalConfig.ScanChocolatey;
        public string DisplayName => Localisation.Progress_AppStores_Chocolatey;

        private static void InitiateChocolatey()
        {
            if (choco == null)
            {
                choco = Lets.GetChocolatey(initializeLogging: false);
                choco.Set(
                    config =>
                    {
                        config.CommandName = nameof(CommandNameType.list);
                        config.ListCommand.LocalOnly = true;
                    });
            }
        }

        private static ApplicationUninstallerEntry ConvertToEntry(PackageResult package)
        {
            var entry = new ApplicationUninstallerEntry
            {
                DisplayName = SanitizeString(package.Package.Title),
                DisplayVersion = ApplicationEntryTools.CleanupDisplayVersion(package.Version),
                Comment = SanitizeString(package.Package.Summary ?? package.Package.Description.Split('\n')[0] ?? package.Package.Tags),
                AboutUrl = package.Package.DocsUrl?.AbsoluteUri ?? package.Package.ProjectUrl?.AbsoluteUri,
                InstallLocation = package.InstallLocation,
                DisplayIcon = string.Empty,
                EstimatedSize = FileSize.Empty,
                IconBitmap = null,
                InstallDate = DateTime.MinValue,
                RatingId = $"Choco {package.Package.Id}",
                UninstallerKind = UninstallerType.Chocolatey
            };

            // A special occasion that needs to be handled.
            if (package.Package.Title.Equals("Chocolatey"))
            {
                entry.InstallLocation = Environment.GetEnvironmentVariable("ChocolateyInstall");
            }

            var psc = new ProcessStartCommand(chocoFullFilename, $"uninstall {package.Package.Id} -y -r");
            entry.UninstallString = psc.ToString();

            // Prevent chocolatey from trying to run the original uninstaller (it's deleted by now), only remove the package
            psc.Arguments += " -n --skipautouninstaller";
            var junk = new RunProcessJunk(entry, null, psc, Localisation.ChocolateyFactory_UninstallInChocolateyJunkName);
            junk.Confidence.Add(ConfidenceRecords.ExplicitConnection);
            junk.Confidence.Add(4);
            entry.AdditionalJunk.Add(junk);
            return entry;
        }

        private static bool IsChocoAvailable()
        {
            if (!chocoIsAvailable.HasValue)
            {
                GetChocoInfo();
            }

            return chocoIsAvailable.Value;
        }
        private static void GetChocoInfo()
        {
            var chocoPath = string.Empty;
            if (string.IsNullOrWhiteSpace(chocoFullFilename))
            {
                var pathVar = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrWhiteSpace(pathVar))
                {
                    chocoPath =
                        pathVar.Split(';')
                            .SingleOrDefault(
                                path => path.IndexOf("Chocolatey", StringComparison.OrdinalIgnoreCase) > -1);
                }
            }

            if (string.IsNullOrWhiteSpace(chocoPath))
            {
                chocoPath = string.Empty;
            }
            var exePath = Path.Combine(chocoPath, "choco.exe");
            if (File.Exists(exePath))
            {
                chocoIsAvailable = true;
                chocoFullFilename = exePath;
            }
        }

        // Chocolatey CLI probably handles this before printing, that's why we do not need this code while parsing
        private static string SanitizeString(string text)
        {
            if (text.IndexOf('\u2013') > -1) text = text.Replace('\u2013', '-'); // en dash
            if (text.IndexOf('\u2014') > -1) text = text.Replace('\u2014', '-'); // em dash
            if (text.IndexOf('\u2015') > -1) text = text.Replace('\u2015', '-'); // horizontal bar
            if (text.IndexOf('\u2017') > -1) text = text.Replace('\u2017', '_'); // double low line
            if (text.IndexOf('\u2018') > -1) text = text.Replace('\u2018', '\''); // left single quotation mark
            if (text.IndexOf('\u2019') > -1) text = text.Replace('\u2019', '\''); // right single quotation mark
            if (text.IndexOf('\u201a') > -1) text = text.Replace('\u201a', ','); // single low-9 quotation mark
            if (text.IndexOf('\u201b') > -1) text = text.Replace('\u201b', '\''); // single high-reversed-9 quotation mark
            if (text.IndexOf('\u201c') > -1) text = text.Replace('\u201c', '\"'); // left double quotation mark
            if (text.IndexOf('\u201d') > -1) text = text.Replace('\u201d', '\"'); // right double quotation mark
            if (text.IndexOf('\u201e') > -1) text = text.Replace('\u201e', '\"'); // double low-9 quotation mark
            if (text.IndexOf('\u2026') > -1) text = text.Replace("\u2026", "..."); // horizontal ellipsis
            if (text.IndexOf('\u2032') > -1) text = text.Replace('\u2032', '\''); // prime
            if (text.IndexOf('\u2033') > -1) text = text.Replace('\u2033', '\"'); // double prime
            if (text.IndexOf('\u00AE') > -1) text = text.Replace('\u00AE', '©'); // register mark
            return text;
        }
    }
}
