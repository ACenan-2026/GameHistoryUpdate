using System.Text;
using System.Xml.Linq;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests
{
    [TestClass]
    public class RenderStyleTests
    {
        [TestMethod]
        public void Default_reproduces_the_historical_look()
        {
            Assert.AreEqual("#FFFFFF", RenderStyle.Default.Color);
            Assert.AreEqual(18, RenderStyle.Default.Size.Value);
            Assert.AreEqual("bold", RenderStyle.Default.Weight);
            Assert.AreEqual("#000000", RenderStyle.Default.OutlineColor);
        }

        [TestMethod]
        public void OverrideOnto_takes_this_styles_set_fields_and_inherits_the_null_ones()
        {
            var baseStyle = RenderStyle.Default;
            // A delta that only sets colour (the other fields are null => inherit).
            var delta = new RenderStyle(color: "#FF0000", font: null, size: null, weight: null, outlineColor: null);

            var merged = delta.OverrideOnto(baseStyle);

            Assert.AreEqual("#FF0000", merged.Color);        // overridden by the delta
            Assert.AreEqual(baseStyle.Font, merged.Font);    // inherited from the base
            Assert.AreEqual(18, merged.Size.Value);          // inherited
            Assert.AreEqual("bold", merged.Weight);          // inherited
        }

        [TestMethod]
        public void AppendCss_emits_complete_typographic_declarations()
        {
            var sb = new StringBuilder();

            RenderStyle.Default.AppendCss(sb);
            string css = sb.ToString();

            StringAssert.Contains(css, "color:#FFFFFF");
            StringAssert.Contains(css, "font-size:18px");
            StringAssert.Contains(css, "font-weight:bold");
            StringAssert.Contains(css, "text-shadow:");   // the outline
        }

        [TestMethod]
        public void AppendCss_drops_the_outline_when_it_is_none()
        {
            var style = new RenderStyle(color: "#FFFFFF", font: "Arial", size: 18, weight: "bold", outlineColor: "none");
            var sb = new StringBuilder();

            style.AppendCss(sb);

            Assert.IsFalse(sb.ToString().Contains("text-shadow"));
        }

        [TestMethod]
        public void Parse_valid_color_when_hex_color_used()
        {
            string renderStyle = "<renderStyle color=\"#0000FF\" />";
            XElement renderStyleElement = XElement.Parse(renderStyle);
            var style = RenderStyle.Parse(renderStyleElement, "");
            Assert.AreEqual("#0000FF", style.Color);
        }

        [TestMethod]
        public void Parse_valid_color_when_plaintext_color_used()
        {
            string renderStyle = "<renderStyle color=\"blue\" />";
            XElement renderStyleElement = XElement.Parse(renderStyle);
            var style = RenderStyle.Parse(renderStyleElement, "");
            Assert.AreEqual("blue", style.Color);
        }

        [TestMethod]
        public void Parse_null_color_when_plaintext_color_is_invalid()
        {
            string renderStyle = "<renderStyle color=\"not-a-color\" />";
            XElement renderStyleElement= XElement.Parse(renderStyle);
            var style = RenderStyle.Parse(renderStyleElement, "");
            Assert.IsNull(style.Color);
        }

        [TestMethod]
        public void Parse_null_color_when_hex_color_is_invalid()
        {
            string renderStyle = "<renderStyle color=\"#RGBRGB\" />";
            XElement renderStyleElement = XElement.Parse(renderStyle);
            var style = RenderStyle.Parse(renderStyleElement, "");
            Assert.IsNull(style.Color);
        }
    }

}
