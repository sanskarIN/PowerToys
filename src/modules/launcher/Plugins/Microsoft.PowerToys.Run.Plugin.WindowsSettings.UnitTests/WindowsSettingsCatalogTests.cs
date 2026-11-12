// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.UnitTests
{
    [TestClass]
    public class WindowsSettingsCatalogTests
    {
        private static WindowsSetting[] Load() =>
            JsonSettingsListHelper.ReadAllPossibleSettings().Settings.ToArray();

        [TestMethod]
        public void TheEmbeddedCatalogIsNotEmpty()
        {
            var loaded = Load();

            Assert.IsTrue(loaded.Length > 100, "The embedded Windows settings catalog was not loaded.");
        }

        [TestMethod]
        public void TheEmbeddedCatalogContainsTheSettingsApplicationEntry()
        {
            var entry = Load().Single(item => item.Name == "OpenSettingsApp");

            Assert.AreEqual("AppSettingsApp", entry.Type);
            Assert.AreEqual("ms-settings:", entry.Command);
            Assert.IsTrue(entry.ShowAsFirstResult);
        }

        [TestMethod]
        public void TheEmbeddedCatalogContainsTheControlPanelEntry()
        {
            var entry = Load().Single(item => item.Name == "OpenControlPanel");

            Assert.AreEqual("AppControlPanel", entry.Type);
            Assert.AreEqual("control.exe", entry.Command);
        }

        [TestMethod]
        public void EveryEntryHasAName()
        {
            var invalid = Load().Where(item => string.IsNullOrWhiteSpace(item.Name)).ToArray();

            Assert.AreEqual(0, invalid.Length, "Catalog entries require search keys.");
        }

        [TestMethod]
        public void EveryEntryHasAnApplicationType()
        {
            var invalid = Load().Where(item => string.IsNullOrWhiteSpace(item.Type)).ToArray();

            Assert.AreEqual(0, invalid.Length, "Catalog entries require app types for path generation.");
        }

        [TestMethod]
        public void EveryEntryHasACommand()
        {
            var invalid = Load().Where(item => string.IsNullOrWhiteSpace(item.Command)).ToArray();

            Assert.AreEqual(0, invalid.Length, "Catalog entries require a launch command.");
        }

        [TestMethod]
        public void OptionalAreaNamesAreNonemptyWhenPresent()
        {
            var invalid = Load()
                .Where(item => item.Areas is not null && item.Areas.Any(string.IsNullOrWhiteSpace))
                .ToArray();

            Assert.AreEqual(0, invalid.Length);
        }

        [TestMethod]
        public void OptionalAlternativeNamesAreNonemptyWhenPresent()
        {
            var invalid = Load()
                .Where(item => item.AltNames is not null && item.AltNames.Any(string.IsNullOrWhiteSpace))
                .ToArray();

            Assert.AreEqual(0, invalid.Length);
        }

        [TestMethod]
        public void TheCatalogContainsBothBoundedAndUnboundedSettings()
        {
            var entries = Load();

            Assert.IsTrue(entries.Any(item => item.IntroducedInBuild.HasValue));
            Assert.IsTrue(entries.Any(item => item.DeprecatedInBuild.HasValue));
            Assert.IsTrue(entries.Any(item => !item.IntroducedInBuild.HasValue && !item.DeprecatedInBuild.HasValue));
        }

        [TestMethod]
        public void TheCatalogContainsDifferentSettingsApplicationTypes()
        {
            var types = Load().Select(item => item.Type).Distinct().ToArray();

            CollectionAssert.Contains(types, "AppSettingsApp");
            CollectionAssert.Contains(types, "AppControlPanel");
            CollectionAssert.Contains(types, "AppMMC");
        }

        [TestMethod]
        public void TheCatalogHasEntriesWithAndWithoutAreaPaths()
        {
            var entries = Load();

            Assert.IsTrue(entries.Any(item => item.Areas is null || item.Areas.Count == 0));
            Assert.IsTrue(entries.Any(item => item.Areas is not null && item.Areas.Count > 0));
        }

        [TestMethod]
        public void TheCatalogHasSearchAliases()
        {
            var entries = Load();

            Assert.IsTrue(entries.Any(item => item.AltNames is not null && item.AltNames.Any()));
        }

        [TestMethod]
        public void ReadingTheCatalogTwiceSucceeds()
        {
            var first = Load();
            var second = Load();

            Assert.IsTrue(first.Length > 100);
            Assert.AreEqual(first.Length, second.Length);
            CollectionAssert.AreEqual(first.Select(x => x.Name).ToArray(), second.Select(x => x.Name).ToArray());
        }

        [TestMethod]
        public void RepeatedReadsDoNotMutateSerializerOptions()
        {
            var initial = Load();
            for (var index = 0; index < 24; index++)
            {
                var next = Load();
                Assert.AreEqual(initial.Length, next.Length, "Reload " + index);
            }
        }

        [TestMethod]
        public async Task ConcurrentCatalogReadsReturnCompleteIndependentCatalogs()
        {
            Task<WindowsSetting[]>[] reads = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(Load))
                .ToArray();

            WindowsSetting[][] catalogs = await Task.WhenAll(reads);

            Assert.AreEqual(16, catalogs.Length);
            Assert.IsTrue(catalogs.All(items => items.Length > 100));
            Assert.AreEqual(1, catalogs.Select(items => items.Length).Distinct().Count());
        }

        [TestMethod]
        public void EachReadProducesIndependentWindowsSettingObjects()
        {
            var first = Load();
            var second = Load();

            Assert.AreNotSame(first[0], second[0]);
            Assert.AreEqual(first[0].Name, second[0].Name);
            Assert.AreEqual(first[0].Command, second[0].Command);
        }

        [TestMethod]
        public void MutatingOneReadDoesNotChangeASubsequentRead()
        {
            var first = Load();
            var originalName = first[0].Name;
            first[0].Name = "Modified by test";

            var second = Load();

            Assert.AreEqual(originalName, second[0].Name);
            Assert.AreNotEqual("Modified by test", second[0].Name);
        }

        [TestMethod]
        public void GeneratingPathsDoesNotPersistInTheEmbeddedCatalog()
        {
            var first = JsonSettingsListHelper.ReadAllPossibleSettings();
            WindowsSettingsPathHelper.GenerateSettingsPathValues(first);
            Assert.IsTrue(first.Settings.Any(item => item.JoinedFullSettingsPath is not null));

            var next = JsonSettingsListHelper.ReadAllPossibleSettings();

            Assert.IsTrue(next.Settings.All(item => item.JoinedFullSettingsPath is null));
        }

        [TestMethod]
        public void ApplyingBuildFilteringDoesNotChangeTheEmbeddedCatalog()
        {
            var first = JsonSettingsListHelper.ReadAllPossibleSettings();
            var originalCount = first.Settings.Count();
            UnsupportedSettingsHelper.FilterByBuildForWindowsBuild(first, 10000);
            Assert.IsTrue(first.Settings.Count() < originalCount);

            var next = JsonSettingsListHelper.ReadAllPossibleSettings();

            Assert.AreEqual(originalCount, next.Settings.Count());
        }

        [TestMethod]
        public void ADefaultSettingHasNoOptionalMetadata()
        {
            var setting = new WindowsSetting();

            Assert.AreEqual(string.Empty, setting.Name);
            Assert.AreEqual(string.Empty, setting.Command);
            Assert.AreEqual(string.Empty, setting.Type);
            Assert.IsFalse(setting.ShowAsFirstResult);
            Assert.IsNull(setting.Areas);
            Assert.IsNull(setting.AltNames);
            Assert.IsNull(setting.JoinedAreaPath);
            Assert.IsNull(setting.JoinedFullSettingsPath);
        }

        [TestMethod]
        public void ADefaultCatalogIsEmptyAndCanBeReplaced()
        {
            var catalog = new WindowsSettings();
            Assert.AreEqual(0, catalog.Settings.Count());

            var first = new WindowsSetting { Name = "First" };
            var second = new WindowsSetting { Name = "Second" };
            catalog.Settings = new[] { first, second };

            CollectionAssert.AreEqual(new[] { first, second }, catalog.Settings.ToArray());
        }

        [TestMethod]
        public void CatalogReadingDoesNotRequireCallingTheMainPluginInitialization()
        {
            var catalog = JsonSettingsListHelper.ReadAllPossibleSettings();

            Assert.IsNotNull(catalog);
            Assert.IsTrue(catalog.Settings.Any(item => item.Name == "OpenSettingsApp"));
        }
    }
}
