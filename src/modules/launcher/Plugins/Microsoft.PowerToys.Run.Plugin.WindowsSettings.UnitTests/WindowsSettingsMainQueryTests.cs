// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.UnitTests
{
    [TestClass]
    public class WindowsSettingsMainQueryTests
    {
        private static WindowsSetting Make(string name, string type = "Settings", params string[] areas)
        {
            return new WindowsSetting
            {
                Name = name,
                Type = type,
                Command = "ms-settings:" + name,
                Areas = areas,
                JoinedFullSettingsPath = type,
            };
        }

        private static Main PluginWith(params WindowsSetting[] entries)
        {
            var plugin = new Main();
            var field = typeof(Main).GetField("_windowsSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is null)
            {
                throw new InvalidOperationException("Windows Settings catalog field was not found.");
            }

            field.SetValue(plugin, new WindowsSettings { Settings = entries });
            return plugin;
        }

        private static List<Result> Search(Main plugin, string text) => plugin.Query(new Query(text));

        [DataRow("disp", "Display", true)]
        [DataRow("DISPLAY", "Display", true)]
        [DataRow("IsPl", "Display", true)]
        [DataRow("mon", "Display", false)]
        [DataRow("print", "Display", false)]
        [DataRow("audio", "Display", false)]
        [DataRow("network", "Network connections", true)]
        [DataRow("connections", "Network connections", true)]
        [DataRow("NETWORK", "Network connections", true)]
        [DataRow("necti", "Network connections", true)]
        [DataRow("blue", "Network connections", false)]
        [DataRow("device", "Network connections", false)]
        [DataTestMethod]
        public void NameQueriesUseCaseInsensitiveSubstringMatching(string query, string name, bool matches)
        {
            using var plugin = PluginWith(Make(name));

            var results = Search(plugin, query);

            Assert.AreEqual(matches, results.Count == 1, query + " => " + name);
        }

        [TestMethod]
        public void QueryBeforeInitializationReturnsAnEmptyList()
        {
            using var plugin = new Main();

            var result = plugin.Query(new Query("display"));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void EmptyCatalogReturnsAnEmptyList()
        {
            using var plugin = PluginWith();

            Assert.AreEqual(0, Search(plugin, "network").Count);
        }

        [TestMethod]
        public void EmptyQueryMatchesAllSettingsInTitleOrder()
        {
            using var plugin = PluginWith(Make("Zulu"), Make("Alpha"), Make("Mike"));

            var result = Search(plugin, string.Empty);

            CollectionAssert.AreEqual(new[] { "Alpha", "Mike", "Zulu" }, result.Select(r => r.Title).ToArray());
        }

        [TestMethod]
        public void WhitespaceQueryMatchesEveryEntry()
        {
            using var plugin = PluginWith(Make("One"), Make("Two"), Make("Three"));

            var result = Search(plugin, "    ");

            Assert.AreEqual(3, result.Count);
        }

        [TestMethod]
        public void AreaNameCanMatchWhenTheTitleDoesNot()
        {
            using var plugin = PluginWith(Make("Network settings", "Settings", "Bluetooth and devices"));

            var result = Search(plugin, "bluetooth");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Network settings", result.Single().Title);
        }

        [TestMethod]
        public void AnAreaPrefixWithAColonIsRecognized()
        {
            using var plugin = PluginWith(Make("Adapter options", "Settings", "Network and Internet"));

            var result = Search(plugin, "Net:");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Adapter options", result.Single().Title);
        }

        [TestMethod]
        public void AColonWithoutAnAreaNameDoesNotMatchArbitrarily()
        {
            using var plugin = PluginWith(Make("Adapter options", "Settings", "Network and Internet"));

            Assert.AreEqual(0, Search(plugin, "Display:").Count);
        }

        [TestMethod]
        public void MultipleAreasCanBeSearchedIndependently()
        {
            using var plugin = PluginWith(Make("Adapter options", "Settings", "Network", "Advanced sharing"));

            Assert.AreEqual(1, Search(plugin, "Network").Count);
            Assert.AreEqual(1, Search(plugin, "sharing").Count);
            Assert.AreEqual(0, Search(plugin, "Printers").Count);
        }

        [TestMethod]
        public void AlternativeNamesAreIncludedInSearch()
        {
            var setting = Make("Display");
            setting.AltNames = new[] { "Monitor", "Screen", "Resolution" };
            using var plugin = PluginWith(setting);

            Assert.AreEqual(1, Search(plugin, "monitor").Count);
            Assert.AreEqual(1, Search(plugin, "SCREEN").Count);
            Assert.AreEqual(1, Search(plugin, "solution").Count);
        }

        [TestMethod]
        public void NullAreaAndAlternativeNameCollectionsAreSafe()
        {
            var setting = Make("Display");
            setting.Areas = null;
            setting.AltNames = null;
            using var plugin = PluginWith(setting);

            Assert.AreEqual(1, Search(plugin, "display").Count);
            Assert.AreEqual(0, Search(plugin, "network").Count);
        }

        [TestMethod]
        public void HierarchyQueriesMatchFullSettingsPathPrefixes()
        {
            using var plugin = PluginWith(Make("Adapter options", "Settings", "Network", "Adapters"));

            Assert.AreEqual(1, Search(plugin, "Settings>Network>Adapters").Count);
            Assert.AreEqual(1, Search(plugin, "Set>Net>Adap").Count);
            Assert.AreEqual(0, Search(plugin, "Settings>System>Adapters").Count);
        }

        [TestMethod]
        public void PathQueriesRejectTooManySegments()
        {
            using var plugin = PluginWith(Make("Adapter options", "Settings", "Network"));

            Assert.AreEqual(0, Search(plugin, "Settings>Network>More").Count);
        }

        [TestMethod]
        public void RegularTextDoesNotMatchTheTypeWithoutAPathDelimiter()
        {
            using var plugin = PluginWith(Make("Display", "Control Panel", "System"));

            Assert.AreEqual(0, Search(plugin, "Control Panel").Count);
            Assert.AreEqual(1, Search(plugin, "Control Panel>System").Count);
        }

        [TestMethod]
        public void AQueryWithAnActionKeywordUsesItsSearchPortion()
        {
            using var plugin = PluginWith(Make("Display"));

            var result = plugin.Query(new Query("?Display", "?"));

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Display", result.Single().Title);
        }

        [TestMethod]
        public void QueryReturnsTheOriginalSettingAsResultContext()
        {
            var entry = Make("Display");
            using var plugin = PluginWith(entry);

            var result = Search(plugin, "Display").Single();

            Assert.AreSame(entry, result.ContextData);
        }

        [TestMethod]
        public void TheQueryDoesNotExecuteAnySettingCommand()
        {
            var entry = Make("Network");
            entry.Command = "not-a-real-command.exe";
            using var plugin = PluginWith(entry);

            var result = Search(plugin, "Network").Single();

            Assert.AreEqual("not-a-real-command.exe", entry.Command);
            Assert.IsNotNull(result.Action);
        }

        [TestMethod]
        public void EqualTitlesFromDifferentAppsAreDeduplicated()
        {
            var first = Make("Display", "Settings");
            var second = Make("Display", "Control Panel");
            using var plugin = PluginWith(first, second);

            var result = Search(plugin, "Display");

            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public void DistinctTitlesAreNotDeduplicated()
        {
            using var plugin = PluginWith(Make("Display"), Make("Display advanced"), Make("Advanced display"));

            var result = Search(plugin, "display");

            Assert.AreEqual(3, result.Count);
        }

        [TestMethod]
        public void QueryingAnUnmatchedTermDoesNotChangeTheCatalog()
        {
            var first = Make("Display");
            var second = Make("Network");
            using var plugin = PluginWith(first, second);

            Assert.AreEqual(0, Search(plugin, "something that does not exist").Count);
            Assert.AreEqual(2, Search(plugin, string.Empty).Count);
        }

        [TestMethod]
        public void ASettingCanBeFoundThroughDifferentSearchChannels()
        {
            var item = Make("Adapter options", "Settings", "Network");
            item.AltNames = new[] { "Connectivity" };
            using var plugin = PluginWith(item);

            Assert.AreEqual(1, Search(plugin, "Adapter").Count);
            Assert.AreEqual(1, Search(plugin, "Network").Count);
            Assert.AreEqual(1, Search(plugin, "connectivity").Count);
            Assert.AreEqual(1, Search(plugin, "Settings>Network").Count);
        }

        [TestMethod]
        public void QueryResultScoringRespectsNamePrefixes()
        {
            using var plugin = PluginWith(Make("Advanced network"), Make("Network settings"));

            var result = Search(plugin, "network");

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(5_000, result[0].Score);
            Assert.AreEqual(10_000, result[1].Score);
        }

        [TestMethod]
        public void QueriesReturnNoResultsForNoMatchingAreasOrNames()
        {
            using var plugin = PluginWith(
                Make("Display", "Settings", "System"),
                Make("Sound", "Settings", "Audio"),
                Make("Mouse", "Control Panel", "Devices"));

            Assert.AreEqual(0, Search(plugin, "Windows Firewall").Count);
        }
    }
}
