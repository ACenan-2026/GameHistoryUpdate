using System;
using System.Collections.Generic;
using System.IO;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests
{
    // Positive-parse coverage for MultiplierConfigParser: the renderStyle deltas (paid/unpaid/dup/unknown state)
    // and the symbol-level rules (duplicate first-wins, unnamed group, shared group style, legacy attributes
    // ignored). The bad-file/degradation paths live in FallbackTests/MultiplierConfigParserFallbackTests.
    [TestClass]
    public class MultiplierConfigParserTests
    {
        private readonly List<string> _tempFiles = new List<string>();

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var f in _tempFiles)
            {
                try { File.Delete(f); } catch { /* best effort */ }
            }
        }

        private string TempConfig(string xml)
        {
            var path = Path.Combine(Path.GetTempPath(), "cfg_" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, xml);
            _tempFiles.Add(path);
            return path;
        }

        private MultiplierSymbolMapping Parse(string xml) =>
            new MultiplierConfigParser(TempConfig(xml)).GetMultiplierParams();

        // Wraps the given <group> markup in the required document envelope.
        private MultiplierSymbolMapping ParseGroups(string groupsXml) =>
            Parse("<AgtReelConfig><GameHistoryConfig gameName=\"G\"><multiplierGroups>"
                  + groupsXml +
                  "</multiplierGroups></GameHistoryConfig></AgtReelConfig>");

        // ----- group render styles -----------------------------------------------------------------

        [TestMethod]
        public void Paid_and_unpaid_render_styles_are_parsed_onto_the_params()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\">" +
                "<renderStyle state=\"paid\" color=\"#FF0000\" />" +
                "<renderStyle state=\"unpaid\" color=\"#00FF00\" />" +
                "<symbol name=\"B01\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("#FF0000", p.PaidStyle.Color);
            Assert.AreEqual("#00FF00", p.UnpaidStyle.Color);
        }

        [TestMethod]
        public void Unpaid_style_inherits_from_the_paid_style_when_not_given()
        {
            // Only a paid renderStyle is declared; the unpaid look falls back to it (see MultiplierParams ctor).
            var mapping = ParseGroups(
                "<group name=\"g1\">" +
                "<renderStyle state=\"paid\" color=\"#123456\" />" +
                "<symbol name=\"B01\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("#123456", p.PaidStyle.Color);
            Assert.AreEqual("#123456", p.UnpaidStyle.Color);   // inherited from paid
        }

        [TestMethod]
        public void A_stateless_render_style_is_treated_as_the_paid_style()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\">" +
                "<renderStyle color=\"#ABCDEF\" />" +
                "<symbol name=\"B01\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("#ABCDEF", p.PaidStyle.Color);
        }

        [TestMethod]
        public void Duplicate_paid_render_style_keeps_the_first()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\">" +
                "<renderStyle state=\"paid\" color=\"#111111\" />" +
                "<renderStyle state=\"paid\" color=\"#222222\" />" +
                "<symbol name=\"B01\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("#111111", p.PaidStyle.Color);
        }

        [TestMethod]
        public void Unknown_render_style_state_is_ignored_and_the_look_stays_default()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\">" +
                "<renderStyle state=\"sideways\" color=\"#FF0000\" />" +
                "<symbol name=\"B01\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            // The bad block contributed nothing, so the style resolves to the historical default.
            Assert.AreEqual(RenderStyle.Default.Color, p.PaidStyle.Color);
        }

        [TestMethod]
        public void A_group_with_no_render_style_uses_the_default_look()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\"><symbol name=\"B01\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual(RenderStyle.Default.Color, p.PaidStyle.Color);
            Assert.AreEqual(RenderStyle.Default.Size, p.PaidStyle.Size);
        }

        // ----- symbol-level rules ------------------------------------------------------------------

        [TestMethod]
        public void Duplicate_symbol_keeps_the_first_definition()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\"><renderStyle color=\"#111111\" /><symbol name=\"B01\" /></group>" +
                "<group name=\"g2\"><renderStyle color=\"#222222\" /><symbol name=\"B01\" /></group>");

            Assert.AreEqual(1, mapping.Mappings.Count);
            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("#111111", p.PaidStyle.Color);   // first definition kept
        }

        [TestMethod]
        public void An_unnamed_group_still_parses_its_symbols()
        {
            var mapping = ParseGroups(
                "<group><symbol name=\"B01\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out _));
        }

        [TestMethod]
        public void Every_symbol_in_a_group_shares_the_group_style()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\"><renderStyle color=\"#FF0000\" />" +
                "<symbol name=\"B01\" /><symbol name=\"B02\" /></group>" +
                "<group name=\"g2\"><symbol name=\"TB\" /></group>");

            Assert.AreEqual(3, mapping.Mappings.Count);
            Assert.IsTrue(mapping.TryGet("B01", out var b01));
            Assert.IsTrue(mapping.TryGet("B02", out var b02));
            Assert.IsTrue(mapping.TryGet("TB", out var tb));
            Assert.AreEqual("#FF0000", b01.PaidStyle.Color);
            Assert.AreEqual("#FF0000", b02.PaidStyle.Color);
            Assert.AreEqual(RenderStyle.Default.Color, tb.PaidStyle.Color);   // other groups are unaffected
        }

        [TestMethod]
        public void Legacy_maths_attributes_are_tolerated_and_ignored()
        {
            // Configs written for the old recompute feature still carry strategy/paid/overlay/claim/value; they
            // must still parse, with only the render styles taken.
            var mapping = Parse(
                "<AgtReelConfig><GameHistoryConfig gameName=\"G\" postWinDivider=\"2\"><multiplierGroups>" +
                "<group name=\"g1\" overlay=\"once\" claim=\"shared\">" +
                "<renderStyle color=\"#ABCDEF\" /><symbol name=\"B01\" value=\"5\" paid=\"false\" /></group>" +
                "</multiplierGroups></GameHistoryConfig></AgtReelConfig>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("#ABCDEF", p.PaidStyle.Color);
        }

        [TestMethod]
        public void MultiplierParams_with_no_styles_resolves_both_to_the_default_look()
        {
            var p = new MultiplierParams();

            Assert.AreEqual(RenderStyle.Default.Color, p.PaidStyle.Color);
            Assert.AreEqual(RenderStyle.Default.Color, p.UnpaidStyle.Color);
        }
    }
}
