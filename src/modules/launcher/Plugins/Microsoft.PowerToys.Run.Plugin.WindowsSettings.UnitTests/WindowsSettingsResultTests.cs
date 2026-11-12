// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.UnitTests
{
    [TestClass]
    public class WindowsSettingsResultTests
    {
        private static WindowsSetting Make(string name, string type = "Settings")
        {
            return new WindowsSetting
            {
                Name = name,
                Type = type,
                Command = "ms-settings:test",
                Areas = new[] { "System", "Display" },
                JoinedAreaPath = "System  \u02C3  Display",
                JoinedFullSettingsPath = type + "  \u02C3  System  \u02C3  Display",
            };
        }

        private static List<Result> Render(string search, params WindowsSetting[] items)
        {
            return ResultHelper.GetResultList(items, search, "Images/windows-settings.png");
        }

        [TestMethod]
        public void EveryResultExposesItsOriginalSettingAsContextData()
        {
            var setting = Make("Display");

            var result = Render("display", setting).Single();

            Assert.AreSame(setting, result.ContextData);
        }

        [TestMethod]
        public void DisplayFieldsUseTheSettingNameAndGeneratedPath()
        {
            var setting = Make("Display", "Control Panel");

            var result = Render("display", setting).Single();

            Assert.AreEqual("Display", result.Title);
            Assert.AreEqual("Control Panel  \u02C3  System  \u02C3  Display", result.SubTitle);
        }

        [TestMethod]
        public void AllResultsUseTheRequestedIconPath()
        {
            var items = new[] { Make("Display"), Make("Network"), Make("Bluetooth") };

            var results = Render(string.Empty, items);

            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.All(result => result.IcoPath == "Images/windows-settings.png"));
        }

        [TestMethod]
        public void AnEmptyInputListYieldsNoResults()
        {
            Assert.AreEqual(0, Render(string.Empty).Count);
        }

        [TestMethod]
        public void AResultActionExistsButIsNotExecutedDuringRendering()
        {
            var setting = Make("Launch");
            setting.Command = "ms-settings:display";

            var result = Render("Launch", setting).Single();

            Assert.IsNotNull(result.Action);
            Assert.AreEqual("ms-settings:display", setting.Command);
        }

        [TestMethod]
        public void TooltipContainsSettingTypeAndCommand()
        {
            var setting = Make("Display");
            setting.Command = "ms-settings:display";

            var tooltip = Render("display", setting).Single().ToolTipData;

            Assert.AreEqual("Display", tooltip.Title);
            StringAssert.Contains(tooltip.Text, "Settings");
            StringAssert.Contains(tooltip.Text, "ms-settings:display");
        }

        [TestMethod]
        public void TooltipContainsTheFullAreaPath()
        {
            var setting = Make("Display");

            var tooltip = Render("display", setting).Single().ToolTipData;

            StringAssert.Contains(tooltip.Text, "System  \u02C3  Display");
        }

        [TestMethod]
        public void TooltipContainsAlternativeNames()
        {
            var setting = Make("Network");
            setting.AltNames = new[] { "Internet", "Connectivity", "LAN" };

            var tooltip = Render("network", setting).Single().ToolTipData;

            StringAssert.Contains(tooltip.Text, "Internet, Connectivity, LAN");
        }

        [TestMethod]
        public void TooltipContainsOptionalNote()
        {
            var setting = Make("Certificates", "MMC");
            setting.Note = "Use administrative privileges";

            var tooltip = Render("certificates", setting).Single().ToolTipData;

            StringAssert.Contains(tooltip.Text, "Use administrative privileges");
        }

        [TestMethod]
        public void AnMmcCommandWithANoteAlsoShowsTheNoteInSubtitle()
        {
            var setting = Make("Services", "MMC");
            setting.Command = "mmc.exe";
            setting.Note = "Open the service snap-in";

            var result = Render("services", setting).Single();

            StringAssert.Contains(result.SubTitle, "Open the service snap-in");
        }

        [TestMethod]
        public void AnMmcCommandWithoutANoteDoesNotAppendANote()
        {
            var setting = Make("Services", "MMC");
            setting.Command = "mmc.exe";
            setting.Note = null;

            var result = Render("services", setting).Single();

            Assert.AreEqual(setting.JoinedFullSettingsPath, result.SubTitle);
        }

        [TestMethod]
        public void OtherCommandsDoNotAppendTheirNotesToSubtitle()
        {
            var setting = Make("Display");
            setting.Note = "Only supported in certain editions";

            var result = Render("display", setting).Single();

            Assert.AreEqual(setting.JoinedFullSettingsPath, result.SubTitle);
            StringAssert.Contains(result.ToolTipData.Text, setting.Note!);
        }

        [TestMethod]
        public void EntriesWithIdenticalTitlesAreDeduplicated()
        {
            var first = Make("Display");
            var second = Make("Display", "Control Panel");

            var results = Render("display", first, second);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(first, results[0].ContextData);
        }

        [TestMethod]
        public void DuplicateTitleMatchingIsCaseSensitive()
        {
            var first = Make("Display");
            var second = Make("display");

            var results = Render("display", first, second);

            Assert.AreEqual(2, results.Count);
            CollectionAssert.AreEqual(new[] { "Display", "display" }, results.Select(result => result.Title).ToArray());
        }

        [TestMethod]
        public void SimilarButNotIdenticalTitlesAreNotDeduplicated()
        {
            var items = new[] { Make("Display"), Make("Display advanced"), Make("Display settings") };

            var results = Render("display", items);

            Assert.AreEqual(3, results.Count);
        }

        [TestMethod]
        public void DeduplicatingPreservesTheFirstTitleOccurrence()
        {
            var one = Make("Same");
            var two = Make("Different");
            var three = Make("Same");
            three.Note = "later duplicate";

            var results = Render("Same", one, two, three);

            Assert.AreEqual(2, results.Count);
            Assert.AreSame(one, results[0].ContextData);
            Assert.AreSame(two, results[1].ContextData);
        }

        [TestMethod]
        public void StartsWithNameMatchGetsHighScore()
        {
            var result = Render("display", Make("Display settings")).Single();

            Assert.AreEqual(10_000, result.Score);
        }

        [TestMethod]
        public void StartsWithNameMatchIsCaseInsensitive()
        {
            var result = Render("dIsPlAy", Make("DISPLAY settings")).Single();

            Assert.AreEqual(10_000, result.Score);
        }

        [TestMethod]
        public void AQueryMatchingSecondNameWordGetsMediumScore()
        {
            var result = Render("display", Make("Advanced display")).Single();

            Assert.AreEqual(5_000, result.Score);
        }

        [TestMethod]
        public void AQueryMatchingLaterNameWordGetsMediumScore()
        {
            var result = Render("settings", Make("Advanced display settings")).Single();

            Assert.AreEqual(5_000, result.Score);
        }

        [TestMethod]
        public void AQueryMatchingAnAreaPrefixGetsLowScore()
        {
            var result = Render("Sys", Make("Networking")).Single();

            Assert.AreEqual(1_000, result.Score);
        }

        [TestMethod]
        public void AnAlternativeNamePrefixGetsMediumScoreWhenAreasExist()
        {
            var setting = Make("Connectivity");
            setting.AltNames = new[] { "Network" };

            var result = Render("net", setting).Single();

            Assert.AreEqual(5_000, result.Score);
        }

        [TestMethod]
        public void AnAlternativeNameNotMatchingTheQueryGetsLowScore()
        {
            var setting = Make("Connectivity");
            setting.AltNames = new[] { "Network" };

            var result = Render("audio", setting).Single();

            Assert.AreEqual(1_000, result.Score);
        }

        [TestMethod]
        public void APreferredSettingScoresAboveOtherSettings()
        {
            var ordinary = Make("Display");
            var preferred = Make("Display");
            preferred.Name = "Display preferences";
            preferred.ShowAsFirstResult = true;

            var results = Render("Display", ordinary, preferred);

            Assert.AreEqual(10_000, results[0].Score);
            Assert.AreEqual(10_500, results[1].Score);
        }

        [TestMethod]
        public void PreferredSettingsKeepHighPriorityForUnmatchedQueries()
        {
            var preferred = Make("System");
            preferred.ShowAsFirstResult = true;

            var result = Render("unrelated", preferred).Single();

            Assert.AreEqual(10_500, result.Score);
        }

        [TestMethod]
        public void EmptySearchUsesDefaultRanking()
        {
            var results = Render(string.Empty, Make("First"), Make("Second"));

            Assert.AreEqual(1_000, results[0].Score);
            Assert.AreEqual(999, results[1].Score);
        }

        [TestMethod]
        public void WhitespaceSearchUsesDefaultRanking()
        {
            var results = Render("  ", Make("First"), Make("Second"));

            Assert.AreEqual(1_000, results[0].Score);
            Assert.AreEqual(999, results[1].Score);
        }

        [TestMethod]
        public void MultipleHighScoreMatchesAreRankedInTheirInputOrder()
        {
            var results = Render("disp", Make("Display"), Make("Display Advanced"), Make("display settings"));

            CollectionAssert.AreEqual(new[] { 10_000, 9_999, 9_998 }, results.Select(result => result.Score).ToArray());
        }

        [TestMethod]
        public void MultipleMediumScoreMatchesAreRankedInTheirInputOrder()
        {
            var results = Render("display", Make("Advanced display"), Make("Primary display"), Make("External display"));

            CollectionAssert.AreEqual(new[] { 5_000, 4_999, 4_998 }, results.Select(result => result.Score).ToArray());
        }

        [TestMethod]
        public void MultiplePreferredMatchesAreRankedInTheirInputOrder()
        {
            var items = new[] { Make("Alpha"), Make("Beta"), Make("Gamma") };
            foreach (var item in items)
            {
                item.ShowAsFirstResult = true;
            }

            var results = Render(string.Empty, items);

            CollectionAssert.AreEqual(new[] { 10_500, 10_499, 10_498 }, results.Select(result => result.Score).ToArray());
        }

        [TestMethod]
        public void RenderingDoesNotChangeTheOriginalSettings()
        {
            var item = Make("Display");
            var originalAreas = item.Areas;
            var originalPath = item.JoinedFullSettingsPath;

            var results = Render("disp", item);

            Assert.AreSame(item, results.Single().ContextData);
            Assert.AreSame(originalAreas, item.Areas);
            Assert.AreEqual(originalPath, item.JoinedFullSettingsPath);
            Assert.AreEqual("Display", item.Name);
        }

        [TestMethod]
        public void DifferentContextObjectsCanHaveTheSameCommand()
        {
            var a = Make("Display");
            var b = Make("Display advanced");
            a.Command = "ms-settings:display";
            b.Command = "ms-settings:display";

            var results = Render("display", a, b);

            Assert.AreEqual(2, results.Count);
            Assert.AreSame(a, results[0].ContextData);
            Assert.AreSame(b, results[1].ContextData);
        }

        [TestMethod]
        public void RankingAnEntireSyntheticCatalogPreservesAllUniqueEntries()
        {
            var items = Enumerable.Range(0, 64)
                .Select(i => Make("Display option " + i))
                .ToArray();

            var results = Render("Display", items);

            Assert.AreEqual(64, results.Count);
            Assert.AreEqual(64, results.Select(result => result.Title).Distinct().Count());
            Assert.AreEqual(10_000, results.First().Score);
            Assert.AreEqual(9_937, results.Last().Score);
        }
    }
}
