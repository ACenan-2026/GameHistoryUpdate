using System;
using System.Collections.Generic;
using System.IO;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests
{
    // Positive-parse coverage for MultiplierConfigParser: the group-level attributes (paid, overlay placement),
    // the renderStyle deltas (paid/unpaid/dup/unknown state), and the symbol-level rules (duplicate first-wins,
    // per-symbol 'paid' ignored, unnamed group). The bad-file/degradation paths live in
    // FallbackTests/MultiplierConfigParserFallbackTests.
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

        // ----- overlay placement -------------------------------------------------------------------

        [TestMethod]
        public void Overlay_absent_defaults_to_all()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\"><symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual(MultiplierOverlayPlacement.All, p.Placement);
        }

        [TestMethod]
        public void Overlay_all_parses_to_all()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\" overlay=\"all\"><symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual(MultiplierOverlayPlacement.All, p.Placement);
        }

        [DataTestMethod]
        [DataRow("once")]
        [DataRow("onceLast")]
        [DataRow("onceOnLastOccurrence")]
        [DataRow("ONCE")]        // case-insensitive
        public void Overlay_once_synonyms_parse_to_once_on_last_occurrence(string overlay)
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\" overlay=\"" + overlay + "\">" +
                "<symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual(MultiplierOverlayPlacement.OnceOnLastOccurrence, p.Placement);
        }

        [TestMethod]
        public void Overlay_unrecognised_value_degrades_to_all()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\" overlay=\"sometimes\">" +
                "<symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual(MultiplierOverlayPlacement.All, p.Placement);
        }

        // ----- group paid --------------------------------------------------------------------------

        [TestMethod]
        public void Group_paid_true_makes_its_symbols_paid()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\"><symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.IsTrue(p.Paid);
        }

        [TestMethod]
        public void Group_paid_false_makes_its_symbols_unpaid()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"false\"><symbol name=\"TB\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("TB", out var p));
            Assert.IsFalse(p.Paid);
        }

        [TestMethod]
        public void Group_with_no_paid_attribute_defaults_to_unpaid()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\"><symbol name=\"TB\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("TB", out var p));
            Assert.IsFalse(p.Paid);
        }

        [TestMethod]
        public void Group_with_an_unparseable_paid_attribute_defaults_to_unpaid()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"yes\"><symbol name=\"TB\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("TB", out var p));
            Assert.IsFalse(p.Paid);
        }

        // ----- group render styles -----------------------------------------------------------------

        [TestMethod]
        public void Paid_and_unpaid_render_styles_are_parsed_onto_the_params()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\">" +
                "<renderStyle state=\"paid\" color=\"#FF0000\" />" +
                "<renderStyle state=\"unpaid\" color=\"#00FF00\" />" +
                "<symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("#FF0000", p.PaidStyle.Color);
            Assert.AreEqual("#00FF00", p.UnpaidStyle.Color);
        }

        [TestMethod]
        public void Unpaid_style_inherits_from_the_paid_style_when_not_given()
        {
            // Only a paid renderStyle is declared; the unpaid look falls back to it (see MultiplierParams ctor).
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\">" +
                "<renderStyle state=\"paid\" color=\"#123456\" />" +
                "<symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("#123456", p.PaidStyle.Color);
            Assert.AreEqual("#123456", p.UnpaidStyle.Color);   // inherited from paid
        }

        [TestMethod]
        public void A_stateless_render_style_is_treated_as_the_paid_style()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\">" +
                "<renderStyle color=\"#ABCDEF\" />" +
                "<symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("#ABCDEF", p.PaidStyle.Color);
        }

        [TestMethod]
        public void Duplicate_paid_render_style_keeps_the_first()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\">" +
                "<renderStyle state=\"paid\" color=\"#111111\" />" +
                "<renderStyle state=\"paid\" color=\"#222222\" />" +
                "<symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("#111111", p.PaidStyle.Color);
        }

        [TestMethod]
        public void Unknown_render_style_state_is_ignored_and_the_look_stays_default()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\">" +
                "<renderStyle state=\"sideways\" color=\"#FF0000\" />" +
                "<symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            // The bad block contributed nothing, so the style resolves to the historical default.
            Assert.AreEqual(RenderStyle.Default.Color, p.PaidStyle.Color);
        }

        [TestMethod]
        public void A_group_with_no_render_style_uses_the_default_look()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\"><symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual(RenderStyle.Default.Color, p.PaidStyle.Color);
            Assert.AreEqual(RenderStyle.Default.Size, p.PaidStyle.Size);
        }

        // ----- symbol-level rules ------------------------------------------------------------------

        [TestMethod]
        public void Duplicate_symbol_keeps_the_first_definition()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\">" +
                "<symbol name=\"B01\" value=\"1\" />" +
                "<symbol name=\"B01\" value=\"9\" /></group>");

            Assert.AreEqual(1, mapping.Mappings.Count);
            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual(1, p.Multiplier);   // first definition kept
        }

        [TestMethod]
        public void A_per_symbol_paid_attribute_is_ignored_in_favour_of_the_group()
        {
            // The symbol declares paid="false" but the group is paid="true"; the group wins (a warning is logged).
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\">" +
                "<symbol name=\"B01\" value=\"1\" paid=\"false\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.IsTrue(p.Paid);   // group-level paid, not the ignored per-symbol one
        }

        [TestMethod]
        public void An_unnamed_group_still_parses_its_symbols_under_a_placeholder_group_name()
        {
            var mapping = ParseGroups(
                "<group strategy=\"TotalBet\" paid=\"true\"><symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("(unnamed)", p.GroupName);
        }

        [TestMethod]
        public void Multiple_groups_all_contribute_their_symbols_with_group_context()
        {
            var mapping = ParseGroups(
                "<group name=\"paidGrp\" strategy=\"TotalBet\" paid=\"true\"><symbol name=\"B01\" value=\"1\" /></group>" +
                "<group name=\"unpaidGrp\" strategy=\"TotalBet\" paid=\"false\"><symbol name=\"TB\" value=\"1\" /></group>");

            Assert.AreEqual(2, mapping.Mappings.Count);
            Assert.IsTrue(mapping.TryGet("B01", out var paid));
            Assert.IsTrue(mapping.TryGet("TB", out var unpaid));
            Assert.AreEqual("paidGrp", paid.GroupName);
            Assert.IsTrue(paid.Paid);
            Assert.AreEqual("unpaidGrp", unpaid.GroupName);
            Assert.IsFalse(unpaid.Paid);
        }

        [TestMethod]
        public void Strategy_type_and_attributes_are_captured_on_each_symbol()
        {
            var mapping = ParseGroups(
                "<group name=\"g1\" strategy=\"LineBetWithStaticMult\" paid=\"true\" numLines=\"20\" staticBetMultiplier=\"30\">" +
                "<symbol name=\"B01\" value=\"1\" /></group>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.AreEqual("LineBetWithStaticMult", p.Strategy.Type);
            Assert.IsTrue(p.Strategy.Attributes.TryGetValue("numLines", out var lines));
            Assert.AreEqual("20", lines);
            Assert.IsTrue(p.Strategy.Attributes.TryGetValue("staticBetMultiplier", out var staticMult));
            Assert.AreEqual("30", staticMult);
        }
    }
}
