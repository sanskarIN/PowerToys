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
    public class WindowsSettingsBuildFilterTests
    {
        private static WindowsSetting Make(string name, uint? introduced = null, uint? deprecated = null)
        {
            return new WindowsSetting
            {
                Name = name,
                Type = "Settings",
                Command = "ms-settings:" + name,
                IntroducedInBuild = introduced,
                DeprecatedInBuild = deprecated,
            };
        }

        private static WindowsSetting[] Filter(uint build, params WindowsSetting[] source)
        {
            var catalog = new WindowsSettings { Settings = source };
            UnsupportedSettingsHelper.FilterByBuildForWindowsBuild(catalog, build);
            return catalog.Settings.ToArray();
        }

        [DataRow(0, null, null, true)]
        [DataRow(0, 1, null, false)]
        [DataRow(1, 1, null, true)]
        [DataRow(99, 100, null, false)]
        [DataRow(100, 100, null, true)]
        [DataRow(101, 100, null, true)]
        [DataRow(999, 0, 1000, true)]
        [DataRow(1000, 0, 1000, false)]
        [DataRow(9999, 10000, 20000, false)]
        [DataRow(10000, 10000, 20000, true)]
        [DataRow(19999, 10000, 20000, true)]
        [DataRow(20000, 10000, 20000, false)]
        [DataRow(20001, 10000, 20000, false)]
        [DataTestMethod]
        public void IntroductionIsInclusiveAndDeprecationIsExclusive(
            int currentBuild,
            int? introduced,
            int? deprecated,
            bool expected)
        {
            var item = Make("Feature", introduced is null ? null : (uint)introduced, deprecated is null ? null : (uint)deprecated);

            Assert.AreEqual(expected, Filter((uint)currentBuild, item).Contains(item));
        }

        [TestMethod]
        public void UnboundedItemAppearsAtBothExtremes()
        {
            var item = Make("Always");

            CollectionAssert.AreEqual(new[] { item }, Filter(0, item));
            CollectionAssert.AreEqual(new[] { item }, Filter(uint.MaxValue, item));
        }

        [TestMethod]
        public void IntroductionBoundaryUsesGreaterThanOrEqualComparison()
        {
            var item = Make("New", introduced: 22621);

            Assert.AreEqual(0, Filter(22620, item).Length);
            Assert.AreEqual(1, Filter(22621, item).Length);
            Assert.AreEqual(1, Filter(22622, item).Length);
        }

        [TestMethod]
        public void DeprecationBoundaryUsesStrictlyLessThanComparison()
        {
            var item = Make("Old", deprecated: 22621);

            Assert.AreEqual(1, Filter(22620, item).Length);
            Assert.AreEqual(0, Filter(22621, item).Length);
            Assert.AreEqual(0, Filter(22622, item).Length);
        }

        [TestMethod]
        public void ASettingAvailableForOnlyOneBuildIsHandled()
        {
            var item = Make("Transient", introduced: 19041, deprecated: 19042);

            Assert.AreEqual(0, Filter(19040, item).Length);
            Assert.AreEqual(1, Filter(19041, item).Length);
            Assert.AreEqual(0, Filter(19042, item).Length);
        }

        [TestMethod]
        public void ZeroLengthAndReversedWindowsNeverMatch()
        {
            var zero = Make("Zero", introduced: 19041, deprecated: 19041);
            var reversed = Make("Reversed", introduced: 22000, deprecated: 19041);

            foreach (var build in new uint[] { 0, 19040, 19041, 20000, 22000, uint.MaxValue })
            {
                Assert.AreEqual(0, Filter(build, zero, reversed).Length, "Build " + build);
            }
        }

        [TestMethod]
        public void DifferentWindowsBuildsRetainOnlyEligibleEntries()
        {
            var legacy = Make("Legacy", deprecated: 19041);
            var modern = Make("Modern", introduced: 19041, deprecated: 22621);
            var next = Make("Next", introduced: 22621);
            var universal = Make("Universal");

            CollectionAssert.AreEqual(new[] { legacy, universal }, Filter(19040, legacy, modern, next, universal));
            CollectionAssert.AreEqual(new[] { modern, universal }, Filter(19041, legacy, modern, next, universal));
            CollectionAssert.AreEqual(new[] { next, universal }, Filter(22621, legacy, modern, next, universal));
        }

        [TestMethod]
        public void FilteredResultsAreOrderedByName()
        {
            var zulu = Make("Zulu");
            var alpha = Make("Alpha");
            var mike = Make("Mike");

            CollectionAssert.AreEqual(new[] { alpha, mike, zulu }, Filter(22621, zulu, mike, alpha));
        }

        [TestMethod]
        public void FilteringPreservesSettingsObjectIdentityAndMetadata()
        {
            var item = Make("Original", introduced: 100, deprecated: 200);
            item.AltNames = new[] { "Alias" };
            item.Areas = new[] { "Section", "Page" };
            item.Note = "Additional details";
            item.ShowAsFirstResult = true;

            var retained = Filter(150, item);

            Assert.AreSame(item, retained.Single());
            Assert.AreEqual("Original", item.Name);
            Assert.AreEqual((uint?)100, item.IntroducedInBuild);
            Assert.AreEqual((uint?)200, item.DeprecatedInBuild);
            Assert.AreEqual("Additional details", item.Note);
            Assert.IsTrue(item.ShowAsFirstResult);
            CollectionAssert.AreEqual(new[] { "Section", "Page" }, item.Areas.ToArray());
        }

        [TestMethod]
        public void EmptyCatalogCanBeFiltered()
        {
            var catalog = new WindowsSettings();

            UnsupportedSettingsHelper.FilterByBuildForWindowsBuild(catalog, 22621);

            Assert.AreEqual(0, catalog.Settings.Count());
        }

        [TestMethod]
        public void NullCatalogCollectionReturnsWithoutFailure()
        {
            var catalog = new WindowsSettings { Settings = null! };

            UnsupportedSettingsHelper.FilterByBuildForWindowsBuild(catalog, 22621);

            Assert.IsNull(catalog.Settings);
        }

        [TestMethod]
        public void FilteringReplacesTheCatalogEntriesWithTheEligibleSubset()
        {
            var removed = Make("Old", deprecated: 100);
            var retained = Make("New", introduced: 100);
            var catalog = new WindowsSettings { Settings = new[] { removed, retained } };

            UnsupportedSettingsHelper.FilterByBuildForWindowsBuild(catalog, 101);

            CollectionAssert.AreEqual(new[] { retained }, catalog.Settings.ToArray());
        }

        [TestMethod]
        public void TwoEligibleSettingsWithTheSameDisplayNameAreNotDeduplicated()
        {
            var first = Make("Duplicate", introduced: 100);
            var second = Make("Duplicate", deprecated: 200);

            var result = Filter(150, first, second);

            Assert.AreEqual(2, result.Length);
            Assert.AreSame(first, result[0]);
            Assert.AreSame(second, result[1]);
        }

        [TestMethod]
        public void ALongMixedCatalogHasExpectedBoundaryCount()
        {
            var source = Enumerable.Range(0, 100)
                .Select(index => Make(
                    "Item" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
                    introduced: (uint)(1000 + index)))
                .ToArray();

            var result = Filter(1050, source);

            Assert.AreEqual(51, result.Length);
            Assert.IsTrue(result.All(item => item.IntroducedInBuild <= 1050));
            Assert.IsTrue(source.All(item => item.IntroducedInBuild >= 1000));
        }

        [TestMethod]
        public void MaximumUnsignedBuildDoesNotOverflow()
        {
            var introducedAtMaximum = Make("Latest", introduced: uint.MaxValue);
            var deprecatedAtMaximum = Make("Earlier", deprecated: uint.MaxValue);

            CollectionAssert.AreEqual(new[] { deprecatedAtMaximum }, Filter(uint.MaxValue - 1, introducedAtMaximum, deprecatedAtMaximum));
            CollectionAssert.AreEqual(new[] { introducedAtMaximum }, Filter(uint.MaxValue, introducedAtMaximum, deprecatedAtMaximum));
        }

        [TestMethod]
        public void FilteringUsesCurrentCultureOrderingWithoutMutatingNames()
        {
            var items = new[] { Make("beta"), Make("Alpha"), Make("gamma") };
            var expected = items.OrderBy(item => item.Name).ToArray();

            var actual = Filter(22000, items);

            CollectionAssert.AreEqual(expected, actual);
            CollectionAssert.AreEqual(new[] { "beta", "Alpha", "gamma" }, items.Select(item => item.Name).ToArray());
        }
    }
}
