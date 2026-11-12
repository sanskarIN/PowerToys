// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.UnitTests
{
    [TestClass]
    public class WindowsSettingsPathTests
    {
        private const string Separator = "  \u02C3  ";

        private static WindowsSetting Create(string name, string type, params string[] areas) =>
            new WindowsSetting
            {
                Name = name,
                Type = type,
                Command = "ms-settings:test",
                Areas = areas,
            };

        private static void BuildPaths(params WindowsSetting[] entries)
        {
            WindowsSettings catalog = new WindowsSettings { Settings = entries };
            WindowsSettingsPathHelper.GenerateSettingsPathValues(catalog);
        }

        [DataRow("Settings", "System", "Display")]
        [DataRow("Control Panel", "Network", "Adapters")]
        [DataRow("Settings", "Privacy", "Location")]
        [DataRow("Settings", "Time and language", "Date and time")]
        [DataRow("MMC", "Certificates", "Current user")]
        [DataRow("Settings", "Accessibility", "Text size")]
        [DataRow("Control Panel", "System and Security", "Windows Tools")]
        [DataRow("Settings", "Apps", "Default apps")]
        [DataRow("Settings", "Bluetooth and devices", "Printers")]
        [DataRow("Settings", "Personalization", "Themes")]
        [DataRow("Settings", "Accounts", "Sign-in options")]
        [DataRow("Settings", "Windows Update", "Advanced options")]
        [DataTestMethod]
        public void TwoAreaPathsPreserveTheAppAndAreaHierarchy(string app, string area, string leaf)
        {
            var entry = Create("Setting", app, area, leaf);

            BuildPaths(entry);

            Assert.AreEqual(area + Separator + leaf, entry.JoinedAreaPath);
            Assert.AreEqual(app + Separator + area + Separator + leaf, entry.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void EntryWithoutAreasHasOnlyTheAppAsItsPath()
        {
            var entry = Create("Root", "Control Panel");

            BuildPaths(entry);

            Assert.AreEqual(string.Empty, entry.JoinedAreaPath);
            Assert.AreEqual("Control Panel", entry.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void NullAreasAreTreatedAsNoAreas()
        {
            var entry = Create("Root", "Settings");
            entry.Areas = null;

            BuildPaths(entry);

            Assert.AreEqual(string.Empty, entry.JoinedAreaPath);
            Assert.AreEqual("Settings", entry.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void EmptyAreaListIsTreatedAsNoAreas()
        {
            var entry = Create("Root", "Settings");
            entry.Areas = new List<string>();

            BuildPaths(entry);

            Assert.AreEqual(string.Empty, entry.JoinedAreaPath);
            Assert.AreEqual("Settings", entry.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void ThreeAreaSegmentsAppearInTheOriginalOrder()
        {
            var entry = Create("Detail", "Settings", "One", "Two", "Three");

            BuildPaths(entry);

            Assert.AreEqual("One" + Separator + "Two" + Separator + "Three", entry.JoinedAreaPath);
            Assert.AreEqual("Settings" + Separator + "One" + Separator + "Two" + Separator + "Three", entry.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void ASingleAreaContainsExactlyOneSeparatorInTheFullPath()
        {
            var entry = Create("Detail", "Settings", "Devices");

            BuildPaths(entry);

            Assert.AreEqual("Devices", entry.JoinedAreaPath);
            Assert.AreEqual(1, entry.JoinedFullSettingsPath!.Split(new[] { Separator }, StringSplitOptions.None).Length - 1);
        }

        [TestMethod]
        public void PathsCanContainNonAsciiAreaNamesWithoutChangingThem()
        {
            var entry = Create("Language", "Paramètres", "中文", "हिन्दी", "日本語");

            BuildPaths(entry);

            Assert.AreEqual("中文" + Separator + "हिन्दी" + Separator + "日本語", entry.JoinedAreaPath);
            Assert.AreEqual("Paramètres" + Separator + entry.JoinedAreaPath, entry.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void PathsCanContainPunctuationWithoutAdditionalEscaping()
        {
            var entry = Create("Port", "Settings", "Network & Internet", "VPN (advanced)", "IPv4/IPv6");

            BuildPaths(entry);

            Assert.AreEqual("Network & Internet" + Separator + "VPN (advanced)" + Separator + "IPv4/IPv6", entry.JoinedAreaPath);
        }

        [TestMethod]
        public void ExistingGeneratedFieldsAreReplaced()
        {
            var entry = Create("Display", "Settings", "System", "Display");
            entry.JoinedAreaPath = "old area";
            entry.JoinedFullSettingsPath = "old full path";

            BuildPaths(entry);

            Assert.AreEqual("System" + Separator + "Display", entry.JoinedAreaPath);
            Assert.AreEqual("Settings" + Separator + "System" + Separator + "Display", entry.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void RebuildingPathsAfterChangingAreasUsesUpdatedValues()
        {
            var entry = Create("Display", "Settings", "System", "Display");

            BuildPaths(entry);
            entry.Areas = new List<string> { "Personalization", "Colors" };
            BuildPaths(entry);

            Assert.AreEqual("Personalization" + Separator + "Colors", entry.JoinedAreaPath);
            Assert.AreEqual("Settings" + Separator + entry.JoinedAreaPath, entry.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void RebuildingPathsAfterChangingTypeUsesUpdatedApp()
        {
            var entry = Create("Tools", "Settings", "System");

            BuildPaths(entry);
            entry.Type = "Control Panel";
            BuildPaths(entry);

            Assert.AreEqual("System", entry.JoinedAreaPath);
            Assert.AreEqual("Control Panel" + Separator + "System", entry.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void GeneratingPathsTwiceIsIdempotent()
        {
            var entry = Create("Display", "Settings", "System", "Display");

            BuildPaths(entry);
            var firstArea = entry.JoinedAreaPath;
            var firstFull = entry.JoinedFullSettingsPath;
            BuildPaths(entry);

            Assert.AreEqual(firstArea, entry.JoinedAreaPath);
            Assert.AreEqual(firstFull, entry.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void GeneratingPathsDoesNotModifySearchMetadata()
        {
            var entry = Create("Display", "Settings", "System");
            entry.AltNames = new[] { "Monitor", "Screen" };
            entry.Note = "Additional details";
            entry.IntroducedInBuild = 22000;
            entry.DeprecatedInBuild = 33000;
            entry.ShowAsFirstResult = true;
            var areas = entry.Areas;

            BuildPaths(entry);

            Assert.AreEqual("Display", entry.Name);
            Assert.AreEqual("ms-settings:test", entry.Command);
            Assert.AreSame(areas, entry.Areas);
            Assert.AreEqual(2, entry.AltNames.Count());
            Assert.AreEqual("Additional details", entry.Note);
            Assert.AreEqual((uint?)22000, entry.IntroducedInBuild);
            Assert.AreEqual((uint?)33000, entry.DeprecatedInBuild);
            Assert.IsTrue(entry.ShowAsFirstResult);
        }

        [TestMethod]
        public void MultipleEntriesGetIndependentPaths()
        {
            var a = Create("Display", "Settings", "System", "Display");
            var b = Create("Mouse", "Control Panel", "Hardware", "Mouse");
            var c = Create("Certificates", "MMC");

            BuildPaths(a, b, c);

            Assert.AreEqual("Settings" + Separator + "System" + Separator + "Display", a.JoinedFullSettingsPath);
            Assert.AreEqual("Control Panel" + Separator + "Hardware" + Separator + "Mouse", b.JoinedFullSettingsPath);
            Assert.AreEqual("MMC", c.JoinedFullSettingsPath);
            Assert.AreEqual(string.Empty, c.JoinedAreaPath);
        }

        [TestMethod]
        public void EmptyCatalogCanBeProcessed()
        {
            var catalog = new WindowsSettings();

            WindowsSettingsPathHelper.GenerateSettingsPathValues(catalog);

            Assert.IsNotNull(catalog.Settings);
            Assert.AreEqual(0, catalog.Settings.Count());
        }

        [TestMethod]
        public void NullSettingsCollectionCanBeProcessed()
        {
            var catalog = new WindowsSettings { Settings = null! };

            WindowsSettingsPathHelper.GenerateSettingsPathValues(catalog);

            Assert.IsNull(catalog.Settings);
        }

        [TestMethod]
        public void ThePathGeneratorDoesNotReorderTheEntries()
        {
            var third = Create("Zulu", "Settings", "Third");
            var first = Create("Alpha", "Settings", "First");
            var second = Create("Mike", "Settings", "Second");
            var catalog = new WindowsSettings { Settings = new[] { third, first, second } };

            WindowsSettingsPathHelper.GenerateSettingsPathValues(catalog);

            CollectionAssert.AreEqual(
                new[] { third, first, second },
                catalog.Settings.ToArray());
        }

        [TestMethod]
        public void ASharedAreasListIsNotMutated()
        {
            List<string> areas = new List<string> { "One", "Two" };
            var left = Create("Left", "Settings");
            var right = Create("Right", "Control Panel");
            left.Areas = areas;
            right.Areas = areas;

            BuildPaths(left, right);

            CollectionAssert.AreEqual(new[] { "One", "Two" }, areas);
            Assert.AreEqual("One" + Separator + "Two", left.JoinedAreaPath);
            Assert.AreEqual(left.JoinedAreaPath, right.JoinedAreaPath);
            Assert.AreNotEqual(left.JoinedFullSettingsPath, right.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void ALongHierarchyKeepsEverySegment()
        {
            var segments = Enumerable.Range(0, 25).Select(i => "Area" + i).ToArray();
            var entry = Create("Deep", "Settings", segments);

            BuildPaths(entry);

            CollectionAssert.AreEqual(segments, entry.JoinedAreaPath!.Split(new[] { Separator }, StringSplitOptions.None));
            Assert.AreEqual(segments.Length + 1, entry.JoinedFullSettingsPath!.Split(new[] { Separator }, StringSplitOptions.None).Length);
        }

        [TestMethod]
        public void EveryEntryInALargeCatalogGetsAPath()
        {
            var entries = Enumerable.Range(0, 128)
                .Select(i => Create("Setting" + i, "Settings", "Group" + (i % 7), "Item" + i))
                .ToArray();

            BuildPaths(entries);

            Assert.AreEqual(128, entries.Count(e => e.JoinedAreaPath is not null));
            Assert.AreEqual(128, entries.Select(e => e.JoinedFullSettingsPath).Distinct().Count());
            Assert.IsTrue(entries.All(e => e.JoinedFullSettingsPath!.StartsWith("Settings" + Separator, StringComparison.Ordinal)));
        }
    }
}
