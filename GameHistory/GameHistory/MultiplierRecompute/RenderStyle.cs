using log4net;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// Immutable text-render style for a multiplier-amount overlay: colour, font, size, weight and outline.
    ///
    /// Design notes (flexibility is deliberate):
    ///  - Every field is NULLABLE. A null field means "not specified at this level" and inherits from the
    ///    level below (unpaid delta -> paid base -> code <see cref="Default"/>). This is what lets a group
    ///    specify a base style and a <c>state="unpaid"</c> block override only the fields that differ
    ///    (e.g. just <see cref="Color"/>).
    ///  - <see cref="Default"/> reproduces the historical hard-coded overlay look, so a config with no
    ///    <renderStyle> renders exactly as before.
    ///  - Adding a property later (e.g. an outline WIDTH, italic, opacity) is a localised change: add one
    ///    nullable field, one parse+validate line in <see cref="Parse"/>, and one emit line in
    ///    <see cref="AppendCss"/> (an outline width would only touch <see cref="AppendOutline"/>). Merge and
    ///    emit are field-driven, so nothing else has to change.
    ///
    /// Validation rejects anything that could break out of the inline <c>style="..."</c> it is emitted into
    /// (no ';', quotes, braces or angle brackets survive a validator); a rejected value degrades to null so
    /// the field inherits rather than corrupting the markup.
    /// </summary>
    public sealed class RenderStyle
    {
        private static readonly ILog sLog = LogManager.GetLogger(typeof(RenderStyle));

        public string Color { get; }
        public string Font { get; }
        public int? Size { get; }            // px
        public string Weight { get; }        // "normal" | "bold"
        public string OutlineColor { get; }  // hex colour, or "none" to drop the outline

        public RenderStyle(string color, string font, int? size, string weight, string outlineColor)
        {
            Color = color;
            Font = font;
            Size = size;
            Weight = weight;
            OutlineColor = outlineColor;
        }

        /// <summary>
        /// The historical hard-coded overlay look. Every resolve starts from this, so any field left
        /// unspecified everywhere still has a concrete value and an un-styled config is unchanged.
        /// </summary>
        public static readonly RenderStyle Default = new RenderStyle(
            color: "#FFFFFF",
            font: "Arial, Helvetica, sans-serif",
            size: 18,
            weight: "bold",
            outlineColor: "#000000");

        /// <summary>
        /// Returns a new style in which each of THIS instance's non-null fields overrides the corresponding
        /// field of <paramref name="baseStyle"/>; null fields inherit. Used to layer a delta (e.g. the
        /// unpaid block) on top of an already-resolved base.
        /// </summary>
        public RenderStyle OverrideOnto(RenderStyle baseStyle)
        {
            if (baseStyle == null) return this;
            return new RenderStyle(
                color: Color ?? baseStyle.Color,
                font: Font ?? baseStyle.Font,
                size: Size ?? baseStyle.Size,
                weight: Weight ?? baseStyle.Weight,
                outlineColor: OutlineColor ?? baseStyle.OutlineColor);
        }

        /// <summary>
        /// Parses a <renderStyle></renderStyle> element into a style whose fields are set only for the attributes
        /// present and valid; every other field is null (inherits). An invalid value is dropped to null with
        /// a warning so a typo degrades to the inherited/default value rather than breaking the overlay.
        /// <paramref name="context"/> is used only to make the warnings locatable (e.g. the group name).
        /// </summary>
        public static RenderStyle Parse(XElement element, string context)
        {
            if (element == null) return new RenderStyle(null, null, null, null, null);

            string color = ValidateColor(element.Attribute("color")?.Value, "color", context);
            string font = ValidateFont(element.Attribute("font")?.Value, context);
            int? size = ValidateSize(element.Attribute("size")?.Value, context);
            string weight = ValidateWeight(element.Attribute("weight")?.Value, context);
            string outline = ValidateOutline(element.Attribute("outline")?.Value, context);

            return new RenderStyle(color, font, size, weight, outline);
        }

        /// <summary>
        /// Appends the typographic CSS declarations (font-family, font-weight, font-size, color, text-shadow)
        /// for this style to <paramref name="sb"/>. Layout declarations (positioning, white-space) stay with
        /// the caller. Any null field falls back to <see cref="Default"/> so the output is always complete.
        /// </summary>
        public void AppendCss(StringBuilder sb)
        {
            string font = Font ?? Default.Font;
            string weight = Weight ?? Default.Weight;
            int size = Size ?? Default.Size.Value;
            string color = Color ?? Default.Color;
            string outline = OutlineColor ?? Default.OutlineColor;

            sb.Append("font-family:").Append(font).Append("; ");
            sb.Append("font-weight:").Append(weight).Append("; ");
            sb.Append("font-size:").Append(size.ToString(CultureInfo.InvariantCulture)).Append("px; ");
            sb.Append("color:").Append(color).Append("; ");
            AppendOutline(sb, outline);
        }

        /// <summary>
        /// Emits the outline as a text-shadow. The shadow SHAPE (four 1px hard offsets plus a 3px soft glow)
        /// lives only here, so it is the single place a future outline WIDTH would change. "none" drops the
        /// outline entirely.
        /// </summary>
        private static void AppendOutline(StringBuilder sb, string outlineColor)
        {
            if (string.IsNullOrEmpty(outlineColor)
                || outlineColor.Equals("none", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string c = outlineColor;
            sb.Append("text-shadow:")
              .Append("-1px -1px 0 ").Append(c).Append(",")
              .Append("1px -1px 0 ").Append(c).Append(",")
              .Append("-1px 1px 0 ").Append(c).Append(",")
              .Append("1px 1px 0 ").Append(c).Append(",")
              .Append("0 0 3px ").Append(c).Append("; ");
        }

        // ---- validators: each returns the normalised value, or null (inherit) on a bad/absent value ----

        private static readonly Regex HexColor = new Regex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.Compiled);
        // Font family: letters, digits, spaces, commas, hyphens and single quotes only. Deliberately excludes
        // ';', '"', ':', '{', '}', '<', '>' so a value can never escape the inline style attribute.
        private static readonly Regex FontName = new Regex("^[A-Za-z0-9 ,'\\-]+$", RegexOptions.Compiled);

        private static string ValidateColor(string raw, string fieldName, string context)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string v = raw.Trim();
            if (HexColor.IsMatch(v)) return v;
            sLog.WarnFormat("renderStyle {0} '{1}' in {2} is not a #RGB/#RRGGBB hex colour; ignoring (inheriting).", fieldName, raw, context);
            return null;
        }

        private static string ValidateFont(string raw, string context)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string v = raw.Trim();
            if (FontName.IsMatch(v)) return v;
            sLog.WarnFormat("renderStyle font '{0}' in {1} contains unsupported characters; ignoring (inheriting).", raw, context);
            return null;
        }

        private static int? ValidateSize(string raw, string context)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int px)
                && px >= 1 && px <= 200)
            {
                return px;
            }
            sLog.WarnFormat("renderStyle size '{0}' in {1} is not an integer in 1..200 (px); ignoring (inheriting).", raw, context);
            return null;
        }

        private static string ValidateWeight(string raw, string context)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string v = raw.Trim().ToLowerInvariant();
            if (v == "normal" || v == "bold") return v;
            sLog.WarnFormat("renderStyle weight '{0}' in {1} is not 'normal' or 'bold'; ignoring (inheriting).", raw, context);
            return null;
        }

        private static string ValidateOutline(string raw, string context)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string v = raw.Trim();
            if (v.Equals("none", System.StringComparison.OrdinalIgnoreCase)) return "none";
            if (HexColor.IsMatch(v)) return v;
            sLog.WarnFormat("renderStyle outline '{0}' in {1} is not a hex colour or 'none'; ignoring (inheriting).", raw, context);
            return null;
        }
    }
}
