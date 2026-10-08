using log4net;
using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// Round-scoped builder for the multiplier-amount overlay on the details view. The amount itself is supplied
    /// by the caller (the RGS owns the maths); this class only decides HOW a tile is drawn: the plain symbol image,
    /// or the image with the amount overlaid in the symbol's configured render style.
    ///
    /// Create it once per game round via <see cref="TryCreate"/>. A null return means the feature is switched off
    /// and every tile should render plain (<see cref="PlainTile"/>).
    /// </summary>
    public sealed class MultiplierOverlayRenderer
    {
        private static readonly ILog sLog = LogManager.GetLogger(typeof(MultiplierOverlayRenderer));

        // Per-symbol render styles from the game's config. Null when there is no usable config, in which case
        // every overlay is drawn in RenderStyle.Default.
        private readonly MultiplierSymbolMapping _mapping;

        // Internal (not private) so tests can build a renderer from an in-memory mapping.
        internal MultiplierOverlayRenderer(MultiplierSymbolMapping mapping)
        {
            _mapping = mapping;
        }

        /// <summary>
        /// Overlay entry point for the details view. Gated by the "MultiplierRecompute.Enabled" appSetting (off
        /// unless explicitly set to true): when off, returns null and the caller renders plain tiles.
        ///
        /// When on, it always returns a renderer. It loads this game's history config
        /// (&lt;GameConfigRoot&gt;\&lt;GameName&gt;\&lt;GameName&gt;_reels.xml) for the per-symbol render styles.
        /// A missing, empty or unreadable config is not fatal: the renderer still overlays, using the default style.
        /// </summary>
        /// <param name="slotRoundReader">Reader over the pulled game round (only the game name is used).</param>
        /// <param name="mapPath">
        /// Resolver for app-relative ("~/...") paths; pass the controller's <c>Server.MapPath</c>. Only invoked
        /// when the configured GameConfig root is app-relative or absent; an absolute configured root never needs it.
        /// </param>
        public static MultiplierOverlayRenderer TryCreate(ISlotRoundReader slotRoundReader, Func<string, string> mapPath)
        {
            bool.TryParse(ConfigurationManager.AppSettings["MultiplierRecompute.Enabled"], out bool enabled);
            if (!enabled)
            {
                return null;
            }

            return new MultiplierOverlayRenderer(LoadMapping(slotRoundReader, mapPath));
        }

        /// <summary>
        /// Loads the per-symbol render styles for this round's game. Returns null when the game name, config root
        /// or config file is missing, when the config has no multiplier symbols, or on any error; the renderer
        /// then falls back to <see cref="RenderStyle.Default"/> for every tile.
        /// </summary>
        private static MultiplierSymbolMapping LoadMapping(ISlotRoundReader slotRoundReader, Func<string, string> mapPath)
        {
            try
            {
                string gameName = slotRoundReader.GetGameName();
                if (string.IsNullOrEmpty(gameName))
                {
                    return null;
                }

                string gameConfigRoot = ResolveGameConfigRoot(mapPath);
                if (gameConfigRoot == null)
                {
                    return null;
                }

                string configPath = Path.Combine(gameConfigRoot, gameName, gameName + "_reels.xml");
                if (!File.Exists(configPath))
                {
                    if (sLog.IsDebugEnabled)
                    {
                        sLog.DebugFormat("No multiplier config for game '{0}' at {1}; overlaying with the default style.", gameName, configPath);
                    }
                    return null;
                }

                MultiplierSymbolMapping mapping = new MultiplierConfigParser(configPath).GetMultiplierParams();
                if (mapping.Mappings.Count == 0)
                {
                    if (sLog.IsDebugEnabled)
                    {
                        sLog.DebugFormat("Config for game '{0}' at {1} has no multiplier symbols; overlaying with the default style.", gameName, configPath);
                    }
                    return null;
                }

                return mapping;
            }
            catch (Exception ex)
            {
                // A bad config must never take down the history page; fall back to the default style.
                sLog.ErrorFormat("Loading the multiplier overlay config failed (non-fatal, using the default style): {0}", ex);
                return null;
            }
        }

        /// <summary>
        /// Resolves the absolute folder that contains the per-game history configs
        /// (&lt;root&gt;\&lt;GameName&gt;\&lt;GameName&gt;_reels.xml). Order of preference:
        ///  1. The "MultiplierRecompute.GameConfigRoot" appSetting: an absolute path (e.g. C:\inetpub\wwwroot\GameConfig)
        ///     or an app-relative "~/..." path (resolved via <paramref name="mapPath"/>). Use this whenever the app
        ///     does not run from the deployed wwwroot copy (e.g. IIS Express / VS debugging against the source project).
        ///  2. Fallback: GameConfig as a sibling of the app root (works for the deployed ...\wwwroot\GameHistory app).
        /// Returns null if neither can be resolved.
        /// </summary>
        private static string ResolveGameConfigRoot(Func<string, string> mapPath)
        {
            string configured = ConfigurationManager.AppSettings["MultiplierRecompute.GameConfigRoot"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                configured = configured.Trim();
                // Allow an app-relative path, though GameConfig normally lives outside the app.
                return configured.StartsWith("~") ? mapPath(configured) : configured;
            }

            string appRoot = mapPath("~");                              // ...\wwwroot\GameHistory (when deployed)
            string parent = Directory.GetParent(appRoot)?.FullName;     // ...\wwwroot
            return parent == null ? null : Path.Combine(parent, "GameConfig");
        }

        /// <summary>
        /// Builds the HTML for one outcome tile. When <paramref name="renderOverlay"/> is false the plain symbol
        /// image is returned. Otherwise <paramref name="overlayAmount"/> (null is treated as 0) is drawn over the
        /// artwork. If <paramref name="symbolName"/> is configured, a non-zero amount uses its paid style and a zero
        /// amount its unpaid style; any other symbol uses <see cref="RenderStyle.Default"/>.
        /// <paramref name="symbolUrl"/> must already be resolved via Url.Content.
        /// </summary>
        public string BuildTile(string symbolUrl, decimal? overlayAmount = null, bool renderOverlay = true, string symbolName = "")
        {
            if (!renderOverlay)
            {
                return PlainTile(symbolUrl);
            }

            decimal amount = overlayAmount ?? 0m;

            RenderStyle style = RenderStyle.Default;
            if (_mapping != null && !string.IsNullOrEmpty(symbolName) && _mapping.TryGet(symbolName, out MultiplierParams p))
            {
                style = amount == 0 ? p.UnpaidStyle : p.PaidStyle;
            }

            string text = HttpUtility.HtmlEncode(FormatOverlayAmount(amount));
            var sb = new StringBuilder();
            sb.Append("<span style=\"position:relative; display:inline-block; line-height:0;\">");
            sb.Append("<img src=\"").Append(symbolUrl).Append("\" >");
            sb.Append("<span style=\"position:absolute; top:50%; left:50%; transform:translate(-50%,-50%); ");
            style.AppendCss(sb);
            sb.Append("white-space:nowrap; pointer-events:none;\">");
            sb.Append(text);
            sb.Append("</span></span>");
            return sb.ToString();
        }

        /// <summary>The plain (no-overlay) tile markup: a bare symbol image. The single definition of that markup,
        /// used both here and by callers rendering with the feature off.</summary>
        public static string PlainTile(string symbolUrl)
        {
            return "<img src=\"" + symbolUrl + "\" >";
        }

        /// <summary>
        /// Formats an overlay amount for display on a tile: no currency symbol, trailing zeros trimmed
        /// (e.g. 200, 25, 5.5), invariant culture for a stable decimal point.
        /// </summary>
        private static string FormatOverlayAmount(decimal amount)
        {
            return amount.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
