using System;
using System.Collections.Generic;
using System.IO;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.FallbackTests
{
    // Bad config files must degrade gracefully: an absent/empty config yields an empty mapping (which the renderer
    // turns into "no overlay"), missing attributes are tolerated, and only outright malformed XML surfaces an error
    // (which PrepareContext catches and turns into a plain render).
    [TestClass]
    public class MultiplierConfigParserFallbackTests
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

        [TestMethod]
        public void No_GameHistoryConfig_element_yields_an_empty_mapping()
        {
            var mapping = Parse("<AgtReelConfig><agtReels /></AgtReelConfig>");

            Assert.AreEqual(0, mapping.Mappings.Count);
        }

        [TestMethod]
        public void Empty_multiplierGroups_yields_an_empty_mapping()
        {
            var mapping = Parse("<AgtReelConfig><GameHistoryConfig gameName=\"G\"><multiplierGroups /></GameHistoryConfig></AgtReelConfig>");

            Assert.AreEqual(0, mapping.Mappings.Count);
        }

        [TestMethod]
        public void A_group_missing_its_strategy_still_parses_but_the_symbol_resolves_no_strategy()
        {
            var mapping = Parse(
                "<AgtReelConfig><GameHistoryConfig gameName=\"G\"><multiplierGroups>" +
                "<group name=\"g1\" paid=\"true\"><symbol name=\"B01\" value=\"1\" /></group>" +
                "</multiplierGroups></GameHistoryConfig></AgtReelConfig>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.IsNull(p.Strategy.Type);   // no strategy -> nothing to compute an amount with -> plain tile
        }

        [TestMethod]
        public void A_symbol_missing_its_value_parses_with_a_null_multiplier()
        {
            var mapping = Parse(
                "<AgtReelConfig><GameHistoryConfig gameName=\"G\"><multiplierGroups>" +
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\"><symbol name=\"B01\" /></group>" +
                "</multiplierGroups></GameHistoryConfig></AgtReelConfig>");

            Assert.IsTrue(mapping.TryGet("B01", out var p));
            Assert.IsNull(p.Multiplier);
        }

        [TestMethod]
        public void A_symbol_missing_its_name_is_skipped()
        {
            var mapping = Parse(
                "<AgtReelConfig><GameHistoryConfig gameName=\"G\"><multiplierGroups>" +
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\">" +
                "<symbol value=\"5\" /><symbol name=\"B02\" value=\"2\" /></group>" +
                "</multiplierGroups></GameHistoryConfig></AgtReelConfig>");

            Assert.AreEqual(1, mapping.Mappings.Count);
            Assert.IsTrue(mapping.TryGet("B02", out _));
        }

        [TestMethod]
        public void Malformed_xml_throws_from_the_constructor()
        {
            // The parser itself does not swallow a broken file; PrepareContext wraps this and renders plain.
            var path = TempConfig("<AgtReelConfig><GameHistoryConfig></AgtReelConfig>");

            Assert.Throws<System.Xml.XmlException>(() => new MultiplierConfigParser(path));
        }
    }
}
