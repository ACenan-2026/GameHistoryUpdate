using log4net;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// The overlay look for one configured multiplier symbol. Both styles are group-level settings shared by every
    /// symbol in the same &lt;group&gt;. <see cref="PaidStyle"/> is used for a non-zero overlay amount and
    /// <see cref="UnpaidStyle"/> for a zero amount. Both are fully resolved (no null fields) so the renderer can
    /// emit them directly. Set from the XML configuration file and immutable once created.
    /// </summary>
    public sealed class MultiplierParams
    {
        public RenderStyle PaidStyle { get; }
        public RenderStyle UnpaidStyle { get; }

        public MultiplierParams(RenderStyle paidStyle = null, RenderStyle unpaidStyle = null)
        {
            // Resolve against the code default so these are never null and an un-styled group keeps the
            // historical look. UnpaidStyle falls back to PaidStyle (not Default) when no unpaid delta was
            // given, so paid and unpaid look identical unless the config asks for a distinction.
            PaidStyle = (paidStyle ?? new RenderStyle(null, null, null, null, null)).OverrideOnto(RenderStyle.Default);
            UnpaidStyle = (unpaidStyle ?? new RenderStyle(null, null, null, null, null)).OverrideOnto(PaidStyle);
        }
    }

    /// <summary>
    /// Maps multiplier symbol names to their corresponding params (see <see cref="MultiplierParams"/>).
    /// </summary>
    public class MultiplierSymbolMapping
    {
        private readonly Dictionary<string, MultiplierParams> _mappings =
            new Dictionary<string, MultiplierParams>();

        public IReadOnlyDictionary<string, MultiplierParams> Mappings => _mappings;

        public bool TryGet(string symbol, out MultiplierParams p) => _mappings.TryGetValue(symbol, out p);

        // First-wins: an already-present symbol is kept and the caller is told (via false) so it can
        // log with the group context it has. Keeps this type free of any logging dependency.
        public bool Insert(string symbol, MultiplierParams multiplierParams)
        {
            if (_mappings.ContainsKey(symbol))
            {
                return false;
            }
            _mappings[symbol] = multiplierParams;
            return true;
        }
    }

    public interface IMultiplierConfigParser
    {
        /// <summary>
        /// Parses the multiplier configuration XML and returns a mapping of symbols to their render styles
        /// (see <see cref="MultiplierParams"/>). Symbols with no name are skipped.
        ///
        /// See MultiplierConfigSchema.md for the expected XML schema.
        /// </summary>
        MultiplierSymbolMapping GetMultiplierParams();
    }

    /// <summary>
    /// Parses the multiplier configuration XML file to extract the render styles for each symbol.
    /// </summary>
    public class MultiplierConfigParser : IMultiplierConfigParser
    {
        private static readonly ILog sLog = LogManager.GetLogger(typeof(MultiplierConfigParser));

        private readonly XDocument _doc;
        public MultiplierConfigParser(string path)
        {
            _doc = XDocument.Load(path);
        }

        public MultiplierSymbolMapping GetMultiplierParams()
        {
            var multiplierMap = new MultiplierSymbolMapping();

            var groups = _doc.Root?.Element("GameHistoryConfig")?.Element("multiplierGroups")?.Elements("group")
                         ?? Enumerable.Empty<XElement>();

            foreach (var groupElement in groups)
            {
                string groupName = groupElement.Attribute("name")?.Value ?? "(unnamed)";

                ParseGroupStyles(groupElement, groupName, out RenderStyle paidStyleDelta, out RenderStyle unpaidStyleDelta);
                var mParams = new MultiplierParams(paidStyleDelta, unpaidStyleDelta);

                foreach (var symbolElement in groupElement.Elements("symbol"))
                {
                    string symbol = symbolElement.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(symbol))
                    {
                        sLog.WarnFormat("Skipping a symbol with a missing 'name' attribute in multiplier group '{0}'.", groupName);
                        continue;
                    }

                    if (!multiplierMap.Insert(symbol, mParams))
                    {
                        sLog.WarnFormat("Duplicate multiplier symbol '{0}' in group '{1}' ignored; first definition kept.", symbol, groupName);
                    }
                }
            }
            return multiplierMap;
        }

        /// <summary>
        /// Reads a group's optional &lt;renderStyle&gt; children into two style DELTAS: the base/paid look
        /// (a &lt;renderStyle&gt; with no <c>state</c>, or <c>state="paid"</c>) and the unpaid look
        /// (<c>state="unpaid"</c>). Both are returned as sparse deltas (unset attributes are null); the
        /// concrete styles are resolved later in <see cref="MultiplierParams"/> (paid over the code default,
        /// unpaid over paid). A group with no &lt;renderStyle&gt; yields empty deltas, i.e. the historical look.
        /// First definition wins if a state is declared more than once, matching the symbol first-wins rule.
        /// </summary>
        private static void ParseGroupStyles(XElement groupElement, string groupName, out RenderStyle paidDelta, out RenderStyle unpaidDelta)
        {
            paidDelta = null;
            unpaidDelta = null;

            foreach (var styleElement in groupElement.Elements("renderStyle"))
            {
                string state = (styleElement.Attribute("state")?.Value ?? "paid").Trim().ToLowerInvariant();
                string context = "multiplier group '" + groupName + "'";

                switch (state)
                {
                    case "":
                    case "paid":
                        if (paidDelta == null) paidDelta = RenderStyle.Parse(styleElement, context + " (paid)");
                        else sLog.WarnFormat("Multiplier group '{0}' has more than one paid renderStyle; first kept.", groupName);
                        break;
                    case "unpaid":
                        if (unpaidDelta == null) unpaidDelta = RenderStyle.Parse(styleElement, context + " (unpaid)");
                        else sLog.WarnFormat("Multiplier group '{0}' has more than one unpaid renderStyle; first kept.", groupName);
                        break;
                    default:
                        sLog.WarnFormat("Multiplier group '{0}' has renderStyle with unrecognised state '{1}'; expected 'paid' or 'unpaid'. Ignored.", groupName, state);
                        break;
                }
            }
        }
    }
}
