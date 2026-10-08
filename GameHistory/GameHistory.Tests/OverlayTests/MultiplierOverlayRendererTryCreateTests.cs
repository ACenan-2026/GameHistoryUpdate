using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.OverlayTests
{
    // Covers MultiplierOverlayRenderer.TryCreate / LoadMapping / ResolveGameConfigRoot, the config-and-filesystem
    // entry point. These tests drive the real ConfigurationManager appSettings (from the isolated test App.config,
    // overridden per test at runtime) and real temp <Game>_reels.xml files on disk.
    //
    // Whether the config was loaded is observed through the overlay colour: the test config styles B10 with
    // ConfigColour, so a tile in that colour proves the config was found and parsed, and a tile in the default
    // colour proves the renderer fell back.
    //
    // appSettings is process-global; MSTest runs tests in an assembly sequentially by default, so each test sets the
    // keys it needs up-front and these do not interfere. TestCleanup restores the feature-off baseline and deletes
    // the temp config trees.
    [TestClass]
    public class MultiplierOverlayRendererTryCreateTests
    {
        private const string Game = "TestGame";
        private const string ConfigColour = "#ABCDEF";
        private const string TileUrl = "~/img/B10.png";
        private readonly List<string> _tempDirs = new List<string>();

        [TestCleanup]
        public void Cleanup()
        {
            // Restore the App.config baseline so nothing leaks into another test.
            SetAppSetting("MultiplierRecompute.Enabled", "false");
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
            "<group name=\"g1\"><renderStyle color=\"" + ConfigColour + "\" /><symbol name=\"B10\" /></group>" +
            "</multiplierGroups></GameHistoryConfig></AgtReelConfig>";

        private const string EmptyReels = "<AgtReelConfig><agtReels /></AgtReelConfig>";

        private const string MalformedReels = "<AgtReelConfig><GameHistoryConfig></AgtReelConfig>";

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

        // Enables the feature and points it at a fresh root containing the given config (or none when null).
        private string EnabledWithConfig(string xml)
        {
            SetAppSetting("MultiplierRecompute.Enabled", "true");
            var root = NewRoot();
            if (xml != null) WriteReels(root, xml);
            SetAppSetting("MultiplierRecompute.GameConfigRoot", root);
            return root;
        }

        private static FakeRoundReader Reader(string gameName = Game) => new FakeRoundReader { GameName = gameName };

        private static string B10Tile(MultiplierOverlayRenderer renderer) =>
            renderer.BuildTile(TileUrl, overlayAmount: 1.23m, symbolName: "B10");

        // Asserts the renderer exists, still overlays, and fell back to the default style (config not used).
        private static void AssertDefaultStyleRenderer(MultiplierOverlayRenderer renderer)
        {
            Assert.IsNotNull(renderer);
            var tile = B10Tile(renderer);
            StringAssert.Contains(tile, ">1.23<");
            StringAssert.Contains(tile, "color:" + RenderStyle.Default.Color);
        }

        // mapPath used when no "~" resolution is expected; throws so a test notices if it is unexpectedly invoked.
        private static readonly Func<string, string> UnusedMapPath =
            _ => throw new InvalidOperationException("mapPath should not be called for an absolute config root");

        // ----- the Enabled switch ------------------------------------------------------------------

        [TestMethod]
        public void TryCreate_returns_null_when_the_feature_is_disabled()
        {
            SetAppSetting("MultiplierRecompute.Enabled", "false");

            Assert.IsNull(MultiplierOverlayRenderer.TryCreate(Reader(), UnusedMapPath));
        }

        [TestMethod]
        public void TryCreate_returns_null_when_the_feature_is_disabled_even_with_a_valid_config()
        {
            EnabledWithConfig(ValidReels);
            SetAppSetting("MultiplierRecompute.Enabled", "false");

            Assert.IsNull(MultiplierOverlayRenderer.TryCreate(Reader(), UnusedMapPath));
        }

        // ----- a valid config is used --------------------------------------------------------------

        [TestMethod]
        public void TryCreate_with_a_valid_absolute_root_uses_the_configured_style()
        {
            EnabledWithConfig(ValidReels);

            var renderer = MultiplierOverlayRenderer.TryCreate(Reader(), UnusedMapPath);

            Assert.IsNotNull(renderer);
            StringAssert.Contains(B10Tile(renderer), "color:" + ConfigColour);
        }

        [TestMethod]
        public void TryCreate_resolves_an_app_relative_config_root_via_mapPath()
        {
            var root = EnabledWithConfig(ValidReels);
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
            StringAssert.Contains(B10Tile(renderer), "color:" + ConfigColour);
        }

        [TestMethod]
        public void TryCreate_falls_back_to_GameConfig_beside_the_app_root_when_no_root_is_configured()
        {
            SetAppSetting("MultiplierRecompute.Enabled", "true");
            // Empty (not removed) forces the fallback branch: ResolveGameConfigRoot treats a null/whitespace value
            // as "unset". We must not Remove() the key: ConfigurationManager.AppSettings.Remove() also mutates the
            // underlying (read-only at runtime) config element and throws, whereas Set() only touches the in-memory
            // collection.
            SetAppSetting("MultiplierRecompute.GameConfigRoot", "");

            // Simulate the deployed layout: <base>\wwwroot\GameHistory (app root) with GameConfig as its sibling.
            var baseDir = NewRoot();
            var appRoot = Path.Combine(baseDir, "wwwroot", "GameHistory");
            Directory.CreateDirectory(appRoot);
            WriteReels(Path.Combine(baseDir, "wwwroot", "GameConfig"), ValidReels);

            var renderer = MultiplierOverlayRenderer.TryCreate(Reader(), _ => appRoot);

            StringAssert.Contains(B10Tile(renderer), "color:" + ConfigColour);
        }

        // ----- no usable config: still overlays, in the default style -------------------------------

        [TestMethod]
        public void TryCreate_overlays_with_the_default_style_when_the_game_name_is_empty()
        {
            var root = EnabledWithConfig(ValidReels);

            AssertDefaultStyleRenderer(MultiplierOverlayRenderer.TryCreate(Reader(gameName: ""), _ => root));
        }

        [TestMethod]
        public void TryCreate_overlays_with_the_default_style_when_the_config_file_is_missing()
        {
            EnabledWithConfig(null);   // root exists, but no <Game>_reels.xml written

            AssertDefaultStyleRenderer(MultiplierOverlayRenderer.TryCreate(Reader(), UnusedMapPath));
        }

        [TestMethod]
        public void TryCreate_overlays_with_the_default_style_when_the_config_has_no_multiplier_symbols()
        {
            EnabledWithConfig(EmptyReels);

            AssertDefaultStyleRenderer(MultiplierOverlayRenderer.TryCreate(Reader(), UnusedMapPath));
        }

        [TestMethod]
        public void TryCreate_overlays_with_the_default_style_when_the_config_is_malformed()
        {
            EnabledWithConfig(MalformedReels);

            AssertDefaultStyleRenderer(MultiplierOverlayRenderer.TryCreate(Reader(), UnusedMapPath));
        }
    }
}
