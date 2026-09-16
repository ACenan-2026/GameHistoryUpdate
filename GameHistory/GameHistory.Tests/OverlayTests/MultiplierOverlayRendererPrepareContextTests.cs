using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using GameHistory.Models;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.OverlayTests
{
    // Covers MultiplierOverlayRenderer.TryCreate / PrepareContext / ResolveGameConfigRoot — the config-and-filesystem
    // entry point. These tests drive the real ConfigurationManager appSettings (from the isolated test App.config,
    // overridden per test at runtime) and real temp <Game>_reels.xml files on disk.
    //
    // appSettings is process-global; MSTest runs tests in an assembly sequentially by default, so each test sets the
    // keys it needs up-front and these do not interfere. TestCleanup restores the feature-off baseline and deletes
    // the temp config trees.
    [TestClass]
    public class MultiplierOverlayRendererPrepareContextTests
    {
        private const string Game = "TestGame";
        private readonly List<string> _tempDirs = new List<string>();

        [TestCleanup]
        public void Cleanup()
        {
            // Restore the App.config baseline so nothing leaks into another test.
            SetAppSetting("MultiplierRecompute.Enabled", "false");
            SetAppSetting("MultiplierRecompute.GateOverlayOnRecordedWin", "false");
            SetAppSetting("MultiplierRecompute.GameConfigRoot", "");

            foreach (var d in _tempDirs)
            {
                try { Directory.Delete(d, recursive: true); } catch { /* best effort */ }
            }
        }

        // ----- appSettings helpers -----------------------------------------------------------------

        // Note: only Set() is safe here. ConfigurationManager.AppSettings.Add/Remove/Clear also mutate the
        // underlying AppSettingsSection element, which is read-only at runtime and throws; Set() writes only to the
        // in-memory NameValueCollection, so tests toggle keys (and "clear" via empty string) with Set() alone.
        private static void SetAppSetting(string key, string value) =>
            ConfigurationManager.AppSettings.Set(key, value);

        // ----- temp config tree --------------------------------------------------------------------

        private const string ValidReels =
            "<AgtReelConfig><GameHistoryConfig gameName=\"" + Game + "\"><multiplierGroups>" +
            "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\"><symbol name=\"B10\" value=\"10\" /></group>" +
            "</multiplierGroups></GameHistoryConfig></AgtReelConfig>";

        private const string EmptyReels = "<AgtReelConfig><agtReels /></AgtReelConfig>";

        // Creates a fresh temp directory to serve as a GameConfig root; registered for cleanup.
        private string NewRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "gcfg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            _tempDirs.Add(root);
            return root;
        }

        // Writes <root>\<Game>\<Game>_reels.xml with the given content.
        private static void WriteReels(string root, string xml)
        {
            var dir = Path.Combine(root, Game);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, Game + "_reels.xml"), xml);
        }

        private static FakeRoundReader Reader(string gameName = Game, decimal? totalBet = 2m) =>
            new FakeRoundReader { GameName = gameName, TotalBet = totalBet };

        // mapPath used when no "~" resolution is expected; throws so a test notices if it is unexpectedly invoked.
        private static readonly Func<string, string> UnusedMapPath =
            _ => throw new InvalidOperationException("mapPath should not be called for an absolute config root");

        // ----- the Enabled gate --------------------------------------------------------------------

        [TestMethod]
        public void TryCreate_returns_null_when_the_feature_is_disabled()
        {
            SetAppSetting("MultiplierRecompute.Enabled", "false");

            Assert.IsNull(MultiplierOverlayRenderer.TryCreate(Reader(), UnusedMapPath));
        }

        [TestMethod]
        public void TryCreate_returns_null_when_the_game_name_is_empty()
        {
            SetAppSetting("MultiplierRecompute.Enabled", "true");
            var root = NewRoot();
            WriteReels(root, ValidReels);
            SetAppSetting("MultiplierRecompute.GameConfigRoot", root);

            Assert.IsNull(MultiplierOverlayRenderer.TryCreate(Reader(gameName: ""), _ => root));
        }

        // ----- config resolution / presence --------------------------------------------------------

        [TestMethod]
        public void TryCreate_returns_null_when_the_config_file_is_missing()
        {
            SetAppSetting("MultiplierRecompute.Enabled", "true");
            var root = NewRoot();   // exists, but no <Game>_reels.xml written
            SetAppSetting("MultiplierRecompute.GameConfigRoot", root);

            Assert.IsNull(MultiplierOverlayRenderer.TryCreate(Reader(), UnusedMapPath));
        }

        [TestMethod]
        public void TryCreate_returns_null_when_the_config_has_no_multiplier_symbols()
        {
            SetAppSetting("MultiplierRecompute.Enabled", "true");
            var root = NewRoot();
            WriteReels(root, EmptyReels);
            SetAppSetting("MultiplierRecompute.GameConfigRoot", root);

            Assert.IsNull(MultiplierOverlayRenderer.TryCreate(Reader(), UnusedMapPath));
        }

        [TestMethod]
        public void TryCreate_with_a_valid_absolute_root_returns_a_renderer_that_overlays_the_amount()
        {
            SetAppSetting("MultiplierRecompute.Enabled", "true");
            var root = NewRoot();
            WriteReels(root, ValidReels);
            SetAppSetting("MultiplierRecompute.GameConfigRoot", root);

            var renderer = MultiplierOverlayRenderer.TryCreate(Reader(totalBet: 2m), UnusedMapPath);

            Assert.IsNotNull(renderer);

            var reader = Reader(totalBet: 2m);
            var spin = new SlotSymbolTableViewModel
            {
                Reels = new List<SlotSymbolReelViewModel>
                {
                    new SlotSymbolReelViewModel("r")
                    {
                        Floors = new List<SlotSymbolViewModel> { new SlotSymbolViewModel { SymbolName = "B10" } }
                    }
                }
            };
            var tile = renderer.BeginSpin(spin, "details", reader).BuildTile("~/img/B10.png", "B10", 0, 0);

            StringAssert.Contains(tile, ">20<");   // total bet 2 * value 10
        }

        [TestMethod]
        public void TryCreate_resolves_an_app_relative_config_root_via_mapPath()
        {
            SetAppSetting("MultiplierRecompute.Enabled", "true");
            var root = NewRoot();
            WriteReels(root, ValidReels);
            SetAppSetting("MultiplierRecompute.GameConfigRoot", "~/GameConfig");

            // The "~/..." root must be resolved through the injected mapPath delegate.
            bool mapPathCalled = false;
            Func<string, string> mapPath = p =>
            {
                mapPathCalled = true;
                Assert.AreEqual("~/GameConfig", p);
                return root;
            };

            var renderer = MultiplierOverlayRenderer.TryCreate(Reader(), mapPath);

            Assert.IsTrue(mapPathCalled);
            Assert.IsNotNull(renderer);
        }

        [TestMethod]
        public void TryCreate_falls_back_to_GameConfig_beside_the_app_root_when_no_root_is_configured()
        {
            SetAppSetting("MultiplierRecompute.Enabled", "true");
            // Empty (not removed) forces the fallback branch: ResolveGameConfigRoot treats a null/whitespace value
            // as "unset". We must not Remove() the key — ConfigurationManager.AppSettings.Remove() also mutates the
            // underlying (read-only at runtime) config element and throws, whereas Set() only touches the in-memory
            // collection.
            SetAppSetting("MultiplierRecompute.GameConfigRoot", "");

            // Simulate the deployed layout: <base>\wwwroot\GameHistory (app root) with GameConfig as its sibling.
            var baseDir = NewRoot();
            var appRoot = Path.Combine(baseDir, "wwwroot", "GameHistory");
            Directory.CreateDirectory(appRoot);
            WriteReels(Path.Combine(baseDir, "wwwroot", "GameConfig"), ValidReels);

            Func<string, string> mapPath = p => p == "~" ? appRoot : appRoot;

            var renderer = MultiplierOverlayRenderer.TryCreate(Reader(), mapPath);

            Assert.IsNotNull(renderer);
        }
    }
}
