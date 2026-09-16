using System.Text;
using System.Xml.Linq;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests
{
    // Field-by-field validation for RenderStyle.Parse (font / size / weight / outline), the outline emit, and the
    // merge/parse edge cases. The colour validators and the basic AppendCss/OverrideOnto happy paths are in
    // RenderStyleTests; this file fills the remaining branches. A rejected value must degrade to null (inherit),
    // never corrupt the inline style, so most assertions check for null.
    [TestClass]
    public class RenderStyleValidationTests
    {
        private static RenderStyle Parse(string attrs) =>
            RenderStyle.Parse(XElement.Parse("<renderStyle " + attrs + " />"), "test");

        // ----- font --------------------------------------------------------------------------------

        [TestMethod]
        public void Parse_accepts_a_plain_font_family_list()
        {
            var style = Parse("font=\"Arial, Helvetica, sans-serif\"");
            Assert.AreEqual("Arial, Helvetica, sans-serif", style.Font);
        }

        [TestMethod]
        public void Parse_rejects_a_font_with_style_breaking_characters()
        {
            // A ';' (or quotes/braces) could escape the inline style attribute, so the value is dropped to null.
            Assert.IsNull(Parse("font=\"Arial; color:red\"").Font);
        }

        // ----- size --------------------------------------------------------------------------------

        [TestMethod]
        public void Parse_accepts_a_size_within_range()
        {
            Assert.AreEqual(24, Parse("size=\"24\"").Size);
        }

        [DataTestMethod]
        [DataRow("0")]      // below the 1..200 range
        [DataRow("201")]    // above the range
        [DataRow("12px")]   // not a bare integer
        [DataRow("abc")]
        public void Parse_rejects_an_out_of_range_or_non_integer_size(string raw)
        {
            Assert.IsNull(Parse("size=\"" + raw + "\"").Size);
        }

        [DataTestMethod]
        [DataRow("1")]
        [DataRow("200")]
        public void Parse_accepts_the_size_boundaries(string raw)
        {
            Assert.IsNotNull(Parse("size=\"" + raw + "\"").Size);
        }

        // ----- weight ------------------------------------------------------------------------------

        [DataTestMethod]
        [DataRow("normal", "normal")]
        [DataRow("bold", "bold")]
        [DataRow("BOLD", "bold")]   // normalised to lower-case
        public void Parse_accepts_valid_weights_case_insensitively(string raw, string expected)
        {
            Assert.AreEqual(expected, Parse("weight=\"" + raw + "\"").Weight);
        }

        [TestMethod]
        public void Parse_rejects_an_unknown_weight()
        {
            Assert.IsNull(Parse("weight=\"heavy\"").Weight);
        }

        // ----- outline -----------------------------------------------------------------------------

        [TestMethod]
        public void Parse_accepts_none_as_the_outline()
        {
            Assert.AreEqual("none", Parse("outline=\"none\"").OutlineColor);
        }

        [TestMethod]
        public void Parse_accepts_a_hex_outline_colour()
        {
            Assert.AreEqual("#123456", Parse("outline=\"#123456\"").OutlineColor);
        }

        [TestMethod]
        public void Parse_rejects_an_invalid_outline()
        {
            Assert.IsNull(Parse("outline=\"not-a-colour\"").OutlineColor);
        }

        [TestMethod]
        public void Parse_accepts_a_three_digit_hex_colour()
        {
            Assert.AreEqual("#FFF", Parse("color=\"#FFF\"").Color);
        }

        // ----- outline emit ------------------------------------------------------------------------

        [TestMethod]
        public void AppendCss_emits_a_text_shadow_for_a_hex_outline()
        {
            var style = new RenderStyle("#FFFFFF", "Arial", 18, "bold", "#000000");
            var sb = new StringBuilder();

            style.AppendCss(sb);
            string css = sb.ToString();

            StringAssert.Contains(css, "text-shadow:");
            StringAssert.Contains(css, "#000000");
        }

        [TestMethod]
        public void AppendCss_emits_the_normal_weight_when_set()
        {
            var style = new RenderStyle("#FFFFFF", "Arial", 18, "normal", "none");
            var sb = new StringBuilder();

            style.AppendCss(sb);

            StringAssert.Contains(sb.ToString(), "font-weight:normal");
        }

        [TestMethod]
        public void AppendCss_fills_null_fields_from_the_default()
        {
            // An all-null style still emits a complete, valid declaration (every field falls back to Default).
            var style = new RenderStyle(null, null, null, null, null);
            var sb = new StringBuilder();

            style.AppendCss(sb);
            string css = sb.ToString();

            StringAssert.Contains(css, "color:" + RenderStyle.Default.Color);
            StringAssert.Contains(css, "font-size:18px");
            StringAssert.Contains(css, "font-weight:bold");
        }

        // ----- parse / merge edge cases ------------------------------------------------------------

        [TestMethod]
        public void Parse_of_a_null_element_yields_an_all_null_style()
        {
            var style = RenderStyle.Parse(null, "test");

            Assert.IsNull(style.Color);
            Assert.IsNull(style.Font);
            Assert.IsNull(style.Size);
            Assert.IsNull(style.Weight);
            Assert.IsNull(style.OutlineColor);
        }

        [TestMethod]
        public void OverrideOnto_a_null_base_returns_this_instance()
        {
            var style = new RenderStyle("#FF0000", null, null, null, null);

            Assert.AreSame(style, style.OverrideOnto(null));
        }
    }
}
