// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.UnitTests
{
    [TestClass]
    public class SettingsPathQueryTests
    {
        private static WindowsSetting Create(string app, params string[] areas)
        {
            return new WindowsSetting
            {
                Name = "Display settings",
                Type = app,
                Areas = areas,
                Command = "ms-settings:display",
            };
        }

        [DataRow("Settings>System>Display", true)]
        [DataRow("settings>system>display", true)]
        [DataRow("SETTINGS>SYSTEM>DISPLAY", true)]
        [DataRow("Set>Sys>Dis", true)]
        [DataRow("Settings>System>Dis", true)]
        [DataRow("Settings>System", true)]
        [DataRow("Settings>", true)]
        [DataRow("Settings>System>", true)]
        [DataRow("settings>sys>d", true)]
        [DataRow("Settings>System>Display>Advanced", false)]
        [DataRow("Settings>Network", false)]
        [DataRow("Control Panel>System", false)]
        [DataRow("Settings>System>Print", false)]
        [DataRow("Settings>System>DisplayX", false)]
        [DataRow("Settings>Accessories", false)]
        [DataRow("Settings>System>Displax", false)]
        [DataRow("Settings>Systems", false)]
        [DataRow("SettingX>System", false)]
        [DataRow("Settings", false)]
        [DataRow("Display", false)]
        [DataRow("System", false)]
        [DataRow("", false)]
        [DataRow("A>B", false)]
        [DataRow("Settings><", false)]
        [DataRow("Settings>Sys>Z", false)]
        [DataTestMethod]
        public void HierarchicalQueriesMatchOnlyValidPrefixes(string search, bool expected)
        {
            var setting = Create("Settings", "System", "Display");

            Assert.AreEqual(expected, ResultHelper.FilterBySettingsPath(setting, search), search);
        }

        [TestMethod]
        public void AbsentAreasDoNotMatchSubsectionQueries()
        {
            var setting = Create("Settings");
            setting.Areas = null;

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>System"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>System>Display"));
        }

        [TestMethod]
        public void EmptyAreaListBehavesLikeMissingAreas()
        {
            var setting = Create("Control Panel");
            setting.Areas = new List<string>();

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Control Panel>"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Control Panel>Hardware"));
        }

        [TestMethod]
        public void AppNameIsTheFirstSegmentRatherThanTheSettingTitle()
        {
            var setting = Create("Control Panel", "Devices", "Mouse");
            setting.Name = "Mouse";

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Control Panel>Devices>Mouse"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Mouse>Devices>Mouse"));
        }

        [TestMethod]
        public void TheFilterDoesNotUseAlternativeSearchNames()
        {
            var setting = Create("Settings", "Accessibility", "Keyboard");
            setting.AltNames = new[] { "Typing", "Keycaps" };

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>Accessibility>Keyboard"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>Typing"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>Keycaps"));
        }

        [TestMethod]
        public void ATrailingDelimiterAllowsNavigationToTheNextLevel()
        {
            var setting = Create("Settings", "System", "Display");

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>"));
            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>System>"));
            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>System>Display>"));
        }

        [TestMethod]
        public void ExtraNonemptyHierarchyComponentsAreRejected()
        {
            var setting = Create("Settings", "System", "Display");

            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>System>Display>More"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>System>Display>More>EvenMore"));
        }

        [TestMethod]
        public void APartialPrefixIsRecognizedAtEachLevel()
        {
            var setting = Create("Settings", "Bluetooth and devices", "Printers and scanners");

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Set>Blue>Print"));
            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>Bluetooth and d>Printer"));
            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "S>B>P"));
        }

        [TestMethod]
        public void ANameWithoutAnAreaPathCanStillBeMatched()
        {
            var setting = Create("MMC");

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "MM>"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "MMC>Certificates"));
        }

        [TestMethod]
        public void TheFilterNeverMutatesSourceFields()
        {
            var setting = Create("Settings", "Network", "VPN");
            var name = setting.Name;
            var command = setting.Command;
            var type = setting.Type;
            var areas = setting.Areas;

            ResultHelper.FilterBySettingsPath(setting, "Set>Net>VP");
            ResultHelper.FilterBySettingsPath(setting, "Control Panel>Network");

            Assert.AreEqual(name, setting.Name);
            Assert.AreEqual(command, setting.Command);
            Assert.AreEqual(type, setting.Type);
            Assert.AreSame(areas, setting.Areas);
        }

        [TestMethod]
        public void ThreeOrMoreAreaLevelsCanBeQueried()
        {
            var setting = Create("Settings", "Accounts", "Sign in", "Additional options", "Hardware token");

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>Accounts>Sign in>Additional>Hardware"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>Accounts>Sign in>Hardware"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>Accounts>Sign in>Additional>Hardware>Extra"));
        }

        [TestMethod]
        public void DifferentAppTypesAreNotInterchangeable()
        {
            var settingsApp = Create("Settings", "System", "Display");
            var controlPanel = Create("Control Panel", "System", "Display");
            var console = Create("MMC", "System", "Display");

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(settingsApp, "Settings>System"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(controlPanel, "Settings>System"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(console, "Settings>System"));
        }

        [TestMethod]
        public void CompletePathQueriesMatchCaseInsensitively()
        {
            var setting = Create("Control Panel", "Network and Internet", "Network Connections");

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "control panel>network and internet>network connections"));
            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "CONTROL PANEL>NETWORK AND INTERNET>NETWORK CONNECTIONS"));
            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Control panel>Network and internet>Network connections"));
        }

        [TestMethod]
        public void AQueryMatchingOnlyTheLastAreaDoesNotSkipTheLeadingLevels()
        {
            var setting = Create("Settings", "Personalization", "Colors");

            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Colors>"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>Colors"));
            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>Personalization>Colors"));
        }

        [TestMethod]
        public void FilterBySettingsPathDoesNotRequireGeneratedDisplayPaths()
        {
            var setting = Create("Settings", "System", "Display");
            Assert.IsNull(setting.JoinedFullSettingsPath);
            Assert.IsNull(setting.JoinedAreaPath);

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>System>Display"));

            Assert.IsNull(setting.JoinedFullSettingsPath);
            Assert.IsNull(setting.JoinedAreaPath);
        }

        [TestMethod]
        public void SourceAreaEnumerationIsNotReordered()
        {
            var setting = Create("Settings", "First", "Second", "Third");
            var before = setting.Areas!.ToArray();

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>First>Second>Third"));

            CollectionAssert.AreEqual(before, setting.Areas.ToArray());
        }

        [TestMethod]
        public void FilteringManyIndependentQueriesReturnsOnlyTheirMatchingItems()
        {
            var items = new[]
            {
                Create("Settings", "System", "Display"),
                Create("Settings", "System", "Sound"),
                Create("Settings", "Apps", "Default apps"),
                Create("Control Panel", "System", "Display"),
                Create("MMC", "Event Viewer"),
            };

            var search = "Settings>System>D";
            var matches = items.Where(item => ResultHelper.FilterBySettingsPath(item, search)).ToArray();

            Assert.AreEqual(1, matches.Length);
            Assert.AreSame(items[0], matches[0]);
        }

        [TestMethod]
        public void MismatchedLevelNeverMatchesBecauseOfTextInALaterLevel()
        {
            var setting = Create("Settings", "Privacy", "Account Info", "Permissions");

            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>Permissions>Account"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>Account>Permissions"));
            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Settings>Privacy>Account>Permissions"));
        }

        [TestMethod]
        public void HierarchyMatchingWorksWithNonAsciiNames()
        {
            var setting = Create("Paramètres", "Écran", "Couleurs");

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Paramètres>Écran>Couleurs"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Paramètres>Réseau>Couleurs"));
        }

        [TestMethod]
        public void FilteringAnEntryWithOnlyAnAppNameSupportsAppPrefix()
        {
            var setting = Create("Control Panel");
            setting.Areas = null;

            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "Control>"));
            Assert.IsTrue(ResultHelper.FilterBySettingsPath(setting, "C>"));
            Assert.IsFalse(ResultHelper.FilterBySettingsPath(setting, "Settings>"));
        }
    }
}
