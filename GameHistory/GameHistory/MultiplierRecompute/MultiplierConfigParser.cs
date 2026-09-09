using log4net;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System;

namespace GameHistory.MultiplierRecompute
{

    /// <summary>
    /// Encapsulates the strategy-based parameters needed to compute the base value for a multiplier symbol.
    /// </summary>
    public sealed class StrategySpec
    {
        public string Type { get;  }
        public IReadOnlyDictionary<string, string> Attributes { get; }

        public StrategySpec(string type, IReadOnlyDictionary<string, string> attributes)
        {
            Type = type;
            Attributes = attributes;
        }
    }


    /// <summary>
    /// Controls how many of a multiplier symbol's on-grid occurrences carry the finalised-amount overlay.
    ///  - <see cref="All"/>: overlay every occurrence (the default). Correct when each occurrence is an
    ///    independent win, e.g. located scatters where B01, B10, B01 each paid their own amount.
    ///  - <see cref="OnceOnLastOccurrence"/>: overlay a single occurrence per spin, on the LAST (right-most
    ///    reel, then lowest floor) in-group tile. Use it when several tiles share one recorded win that was
    ///    only won once — e.g. a wheel feature triggered by three 'Wh' symbols where only the last (result)
    ///    symbol carries the multiplier and the recorded located-scatter win is a single amount. Painting it
    ///    on every 'Wh' would imply the amount was won three times.
    /// </summary>
    public enum MultiplierOverlayPlacement
    {
        All,
        OnceOnLastOccurrence
    }

    /// <summary>
    /// Represents the parameters associated with a multiplier symbol, including its multiplier value,
    /// the strategy type used to compute its base value, and whether the symbol is considered "paid" or not.
    /// <see cref="GroupName"/> and <see cref="Placement"/> are group-level display settings shared by every
    /// symbol in the same &lt;group&gt;; <see cref="Placement"/> decides how many occurrences are overlaid and
    /// <see cref="GroupName"/> lets the "once" placement dedupe across all members of a group (which may carry
    /// different codes, e.g. Wh / Wh2 / Wh3).
    /// All of these fields are set via an XML configuration file and are immutable once the object is created.
    /// </summary>
    public sealed class MultiplierParams
    {
        public int Multiplier {  get; }
        public bool Paid { get; }
        public StrategySpec Strategy { get; }
        public string GroupName { get; }
        public MultiplierOverlayPlacement Placement { get; }

        public MultiplierParams(
            int multiplier,
            bool paid,
            StrategySpec strategy,
            string groupName = null,
            MultiplierOverlayPlacement placement = MultiplierOverlayPlacement.All)
        {
            Multiplier = multiplier;
            Paid = paid;
            Strategy = strategy;
            GroupName = groupName;
            Placement = placement;
        }

    }

    /// <summary>
    /// Maps multiplier symbol names to their corresponding params (see <cref cref="MultiplierParams" />.
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
        /// Parses the multiplier configuration XML and returns a mapping of symbols to their corresponding multiplier parameters.
        /// Returns a MultiplierSymbolMapping object containing the mappings currently detailing the multiplier value, strategy type, 
        /// and whether the symbol is paid or not.
        /// If the XML structure is invalid or missing required attributes, those entries will be skipped.
        /// 
        /// Consider reading the corresponding documentation to understand the expected XML schema and attributes for proper configuration.
        /// </summary>
        /// <returns><see cref="MultiplierSymbolMapping"/>MultiplierSymbolMapping</returns>
        MultiplierSymbolMapping GetMultiplierParams();
    }

    /// <summary>
    /// Parses the multiplier configuration XML file to extract multiplier parameters for each symbol
    /// </summary>
    public class MultiplierConfigParser : IMultiplierConfigParser 
    {
        private static readonly ILog sLog = LogManager.GetLogger(typeof(MultiplierConfigParser));

        private readonly XDocument doc;
        public MultiplierConfigParser(string path)
        {
            doc = XDocument.Load(path);
        }


        public MultiplierSymbolMapping GetMultiplierParams()
        {
            var multiplierMap = new MultiplierSymbolMapping();

            var groups = doc.Root?.Element("GameHistoryConfig")?.Element("multiplierGroups")?.Elements("group")
                         ?? Enumerable.Empty<XElement>();

            foreach (var groupElement in groups)
            {

                string groupName = groupElement.Attribute("name")?.Value ?? "(unnamed)";

                // creating a dictionary out of the current groupElement's attributes
                string strategy = groupElement.Attribute("strategy")?.Value;

                var attrs = groupElement.Attributes()
                        .ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.OrdinalIgnoreCase);
                var spec = new StrategySpec(strategy, attrs);

                if (string.IsNullOrEmpty(strategy))
                {
                    sLog.WarnFormat("Multiplier group '{0}' has no strategy; its symbols will not resolve a base value.", groupName);
                }

                MultiplierOverlayPlacement placement = ParsePlacement(groupElement.Attribute("overlay")?.Value, groupName);

                foreach (var symbolElement in groupElement.Elements("symbol"))
                {
                    string symbol = symbolElement.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(symbol))
                    {
                        sLog.WarnFormat("Skipping a symbol with a missing 'name' attribute in multiplier group '{0}'.", groupName);
                        continue;
                    }
                    int multiplier = int.TryParse(symbolElement.Attribute("value")?.Value, out var m) ? m : 1000000007;        // if your multiplier is 109, you are probably missing a value attribute in the XML
                    bool paid = bool.TryParse(symbolElement.Attribute("paid")?.Value, out var p) && p;

                    if (!multiplierMap.Insert(symbol, new MultiplierParams(multiplier, paid, spec, groupName, placement)))
                    {
                        sLog.WarnFormat("Duplicate multiplier symbol '{0}' in group '{1}' ignored; first definition kept.", symbol, groupName);
                    }
                }
            }
            return multiplierMap;
        }

        /// <summary>
        /// Parses the optional group-level "overlay" attribute into a <see cref="MultiplierOverlayPlacement"/>.
        /// Absent/empty or "all" -> <see cref="MultiplierOverlayPlacement.All"/> (the default, every occurrence
        /// overlaid). "once"/"onceLast"/"onceOnLastOccurrence" -> overlay a single occurrence per spin on the
        /// last in-group tile. An unrecognised value is treated as All and warned, so a typo degrades to the
        /// safe, historical behaviour rather than silently suppressing overlays.
        /// </summary>
        private static MultiplierOverlayPlacement ParsePlacement(string raw, string groupName)
        {
            if (string.IsNullOrWhiteSpace(raw)) return MultiplierOverlayPlacement.All;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "all":
                    return MultiplierOverlayPlacement.All;
                case "once":
                case "oncelast":
                case "onceonlastoccurrence":
                    return MultiplierOverlayPlacement.OnceOnLastOccurrence;
                default:
                    sLog.WarnFormat("Multiplier group '{0}' has unrecognised overlay '{1}'; defaulting to 'all'.", groupName, raw);
                    return MultiplierOverlayPlacement.All;
            }
        }
    }
}