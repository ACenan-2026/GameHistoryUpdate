using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.OverlayTests
{
    // Covers MultiplierOverlayRenderer.BuildTile / PlainTile: the amount is supplied by the caller, so these tests
    // pin down how a tile is DRAWN, not what the amount is: overlay vs plain, number formatting, and which render
    // style is chosen. Config loading and the Enabled switch live in MultiplierOverlayRendererTryCreateTests.
    [TestClass]
    public class BuildTileTests
    {
        private const string Url = "~/Content/Images/Game/Desktop/B10.png";
        private const string PaidColour = "#FF0000";
        private const string UnpaidColour = "#00FF00";

        private static RenderStyle Colour(string c) => new RenderStyle(c, null, null, null, null);

        // A renderer whose config styles B10 red when paid (non-zero) and green when unpaid (zero).
        private static MultiplierOverlayRenderer StyledRenderer()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", new MultiplierParams(Colour(PaidColour), Colour(UnpaidColour)));
            return new MultiplierOverlayRenderer(mapping);
        }

        // A renderer with no config, as when the game has no _reels.xml.
        private static MultiplierOverlayRenderer UnconfiguredRenderer() => new MultiplierOverlayRenderer(null);

        // ----- plain tile -------------------------------------------------------------------------------

        [TestMethod]
        public void PlainTile_is_the_bare_original_image()
        {
            Assert.AreEqual("<img src=\"" + Url + "\" >", MultiplierOverlayRenderer.PlainTile(Url));
        }

        [TestMethod]
        public void RenderOverlay_false_returns_the_plain_tile_even_for_a_configured_symbol()
        {
            var html = StyledRenderer().BuildTile(Url, overlayAmount: 250m, renderOverlay: false, symbolName: "B10");

            Assert.AreEqual(MultiplierOverlayRenderer.PlainTile(Url), html);
        }

        // ----- overlay markup ---------------------------------------------------------------------------

        [TestMethod]
        public void Overlay_keeps_the_original_artwork_and_draws_the_amount_on_top()
        {
            var html = StyledRenderer().BuildTile(Url, overlayAmount: 1.23m, symbolName: "B10");

            StringAssert.Contains(html, "<img src=\"" + Url + "\"");
            StringAssert.Contains(html, "position:absolute");
            StringAssert.Contains(html, ">1.23<");
        }

        [DataTestMethod]
        [DataRow("250", "250")]
        [DataRow("5.50", "5.5")]
        [DataRow("12.00", "12")]
        [DataRow("0.30", "0.3")]
        [DataRow("1.234", "1.23")]   // two decimal places at most
        public void Amount_is_formatted_with_trailing_zeros_trimmed(string amount, string expected)
        {
            var html = UnconfiguredRenderer().BuildTile(Url, overlayAmount: decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));

            StringAssert.Contains(html, ">" + expected + "<");
        }

        [TestMethod]
        public void A_null_amount_is_drawn_as_zero()
        {
            var html = UnconfiguredRenderer().BuildTile(Url, overlayAmount: null);

            StringAssert.Contains(html, ">0<");
        }

        // ----- every symbol is overlaid -----------------------------------------------------------------

        [DataTestMethod]
        [DataRow("B10")]   // configured
        [DataRow("Ae")]    // not configured
        [DataRow("")]      // blank / hidden
        [DataRow((string)null)]    // missing name must not throw
        public void Every_symbol_is_overlaid_with_a_config(string symbolName)
        {
            var html = StyledRenderer().BuildTile(Url, overlayAmount: 1.23m, symbolName: symbolName);

            StringAssert.Contains(html, ">1.23<");
        }

        [DataTestMethod]
        [DataRow("B10")]
        [DataRow("")]
        [DataRow((string)null)]
        public void Every_symbol_is_overlaid_without_a_config(string symbolName)
        {
            var html = UnconfiguredRenderer().BuildTile(Url, overlayAmount: 1.23m, symbolName: symbolName);

            StringAssert.Contains(html, ">1.23<");
            StringAssert.Contains(html, "color:" + RenderStyle.Default.Color);
        }

        // ----- style selection --------------------------------------------------------------------------

        [TestMethod]
        public void A_configured_symbol_with_a_non_zero_amount_uses_its_paid_style()
        {
            var html = StyledRenderer().BuildTile(Url, overlayAmount: 1.23m, symbolName: "B10");

            StringAssert.Contains(html, "color:" + PaidColour);
        }

        [TestMethod]
        public void A_configured_symbol_with_a_zero_amount_uses_its_unpaid_style()
        {
            var html = StyledRenderer().BuildTile(Url, overlayAmount: 0m, symbolName: "B10");

            StringAssert.Contains(html, "color:" + UnpaidColour);
        }

        [TestMethod]
        public void An_unconfigured_symbol_uses_the_default_style()
        {
            var html = StyledRenderer().BuildTile(Url, overlayAmount: 1.23m, symbolName: "Ae");

            StringAssert.Contains(html, "color:" + RenderStyle.Default.Color);
        }
    }
}
