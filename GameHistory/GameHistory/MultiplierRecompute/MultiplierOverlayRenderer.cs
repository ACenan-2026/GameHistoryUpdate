using GameHistory.Models;
using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// Round-scoped orchestrator for the multiplier-amount overlay. Owns the feature's decision to run at all
    /// (config resolution, amount computation, the log-only Phase 1 validation) and hands out a per-spin
    /// <see cref="SpinOverlay"/> that builds the outcome tiles. Lifting this out of the controller keeps the
    /// HTTP action thin and makes the overlay logic unit-testable on its own (the only web dependency, resolving
    /// app-relative config paths, is injected as a <c>mapPath</c> delegate).
    ///
    /// Create it once per game round via <see cref="TryCreate"/>; a null return means "render the plain symbols
    /// as before" (feature disabled, no/empty config, or a non-fatal failure).
    /// </summary>
    public sealed class MultiplierOverlayRenderer
    {
        private static readonly ILog sLog = LogManager.GetLogger(typeof(MultiplierOverlayRenderer));

        private readonly MultiplierOverlayContext _ctx;

        private MultiplierOverlayRenderer(MultiplierOverlayContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>
        /// Multiplier-recompute feature entry point for the details view. Resolves this game's history config
        /// (&lt;GameConfigRoot&gt;\&lt;GameName&gt;\&lt;GameName&gt;_reels.xml), computes the finalised multiplier
        /// amounts, runs the LOG-ONLY Phase 1 validation (cross-checks computed vs. recorded located-scatter wins
        /// and logs any divergence), and returns a renderer the tile loop uses to overlay those amounts. Wrapped so
        /// any failure is non-fatal to the history page: on error/disabled/no-config it returns null and the tiles
        /// render exactly as before.
        ///
        /// Gated by the "MultiplierRecompute.Enabled" appSetting (off unless explicitly set to true).
        /// With "MultiplierRecompute.GateOverlayOnRecordedWin" off (default) every configured multiplier symbol is
        /// overlaid — paid (B) and statically-unpaid (TB) alike — told apart by the paid vs unpaid render style;
        /// with it on, only occurrences the recorded located-scatter outcome confirms paid are overlaid.
        /// </summary>
        /// <param name="slotRoundReader">Reader over the pulled game round.</param>
        /// <param name="mapPath">
        /// Resolver for app-relative ("~/...") paths — pass the controller's <c>Server.MapPath</c>. Only invoked
        /// when the configured GameConfig root is app-relative or absent; an absolute configured root never needs it.
        /// </param>
        public static MultiplierOverlayRenderer TryCreate(ISlotRoundReader slotRoundReader, Func<string, string> mapPath)
        {
            MultiplierOverlayContext ctx = PrepareContext(slotRoundReader, mapPath);
            return ctx == null ? null : new MultiplierOverlayRenderer(ctx);
        }

        /// <summary>
        /// Begins one spin: resolves the "once" placement winning cells and the recorded-outcome paid/unpaid gate a
        /// single time, then returns a <see cref="SpinOverlay"/> whose <see cref="SpinOverlay.BuildTile"/> reuses
        /// them across every tile in the spin.
        /// </summary>
        public SpinOverlay BeginSpin(SlotSymbolTableViewModel spin, string spinDetails, ISlotRoundReader slotRoundReader)
        {
            return new SpinOverlay(_ctx, spin, spinDetails, slotRoundReader);
        }

        private static MultiplierOverlayContext PrepareContext(ISlotRoundReader slotRoundReader, Func<string, string> mapPath)
        {
            try
            {
                bool.TryParse(ConfigurationManager.AppSettings["MultiplierRecompute.Enabled"], out bool enabled);
                if (!enabled)
                {
                    return null;
                }

                string gameName = slotRoundReader.GetGameName();
                if (string.IsNullOrEmpty(gameName))
                {
                    return null;
                }

                // Resolve the GameConfig root. Prefer the explicit "MultiplierRecompute.GameConfigRoot" appSetting
                // (an absolute path, or an app-relative "~/..." path) so it works whether the app runs from the
                // deployed wwwroot copy or straight from the source project. If unset, fall back to the historical
                // assumption that GameConfig is a sibling of the app root (...\wwwroot\GameConfig for ...\wwwroot\GameHistory).
                string gameConfigRoot = ResolveGameConfigRoot(mapPath);
                if (gameConfigRoot == null)
                {
                    return null;
                }
                string configPath = Path.Combine(gameConfigRoot, gameName, gameName + "_reels.xml");
                if (!System.IO.File.Exists(configPath))
                {
                    if (sLog.IsDebugEnabled)
                    {
                        sLog.DebugFormat("No multiplier config for game '{0}' at {1}; skipping overlay/validation.", gameName, configPath);
                    }
                    return null;
                }

                IMultiplierConfigParser parser = new MultiplierConfigParser(configPath);
                MultiplierSymbolMapping mapping = parser.GetMultiplierParams();

                // Early return when the config carries no multiplier symbols. This happens when the
                // <GameName>_reels.xml has no GameHistoryConfig element (or an empty multiplierGroups):
                // there is nothing to overlay, so the tiles must fall back to the original plain-symbol
                // rendering. Returning null here makes that fallback explicit at the source, and also
                // skips the log-only validator (which would otherwise WARN about every recorded
                // located-scatter win having no computed match for a game that was never configured).
                if (mapping == null || mapping.Mappings.Count == 0)
                {
                    if (sLog.IsDebugEnabled)
                    {
                        sLog.DebugFormat("Config for game '{0}' at {1} has no multiplier symbols (missing GameHistoryConfig/multiplierGroups); rendering plain symbols.", gameName, configPath);
                    }
                    return null;
                }

                IReadOnlyDictionary<string, decimal> computed = new WonAmountsComputer().ComputeWonAmounts(slotRoundReader, mapping);

                if (sLog.IsDebugEnabled)
                {
                    foreach (var kv in computed)
                    {
                        sLog.DebugFormat("Computed multiplier {0} -> {1} for game '{2}'.", kv.Key, kv.Value, gameName);
                    }
                }

                // Phase 1 validation stays log-only; it never alters what the tile loop renders.
                new MultiplierComputationValidator().ValidateRound(slotRoundReader, mapping, computed);

                // Web.Config GameHistory Settings
                bool.TryParse(ConfigurationManager.AppSettings["MultiplierRecompute.GateOverlayOnRecordedWin"], out bool gateOnRecordedWin);

                return new MultiplierOverlayContext
                {
                    GateOnRecordedWin = gateOnRecordedWin,
                    Mapping = mapping,
                    Computed = computed
                };
            }
            catch (Exception ex)
            {
                // The multiplier feature must never take down the history page; degrade to plain symbols.
                sLog.ErrorFormat("Multiplier overlay/validation failed (non-fatal): {0}", ex);
                return null;
            }
        }

        /// <summary>
        /// Resolves the absolute folder that contains the per-game history configs
        /// (&lt;root&gt;\&lt;GameName&gt;\&lt;GameName&gt;_reels.xml). Order of preference:
        ///  1. The "MultiplierRecompute.GameConfigRoot" appSetting — an absolute path (e.g. C:\inetpub\wwwroot\GameConfig)
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
    }

    /// <summary>
    /// Spin-scoped tile builder produced by <see cref="MultiplierOverlayRenderer.BeginSpin"/>. Resolves the spin's
    /// "once" winning cells and recorded-outcome gate once at construction, then <see cref="BuildTile"/> draws each
    /// outcome tile against them.
    /// </summary>
    public sealed class SpinOverlay
    {
        private static readonly ILog sLog = LogManager.GetLogger(typeof(SpinOverlay));

        private readonly MultiplierOverlayContext _ctx;
        private readonly Dictionary<string, GridCell> _onceOverlayCells;
        private readonly RecordedOverlayGate _recordedGate;

        internal SpinOverlay(MultiplierOverlayContext ctx, SlotSymbolTableViewModel spin, string spinDetails, ISlotRoundReader slotRoundReader)
        {
            _ctx = ctx;
            // For "once" placement groups, resolve up-front the single winning cell (per group) that should carry
            // the overlay for THIS spin; every other in-group occurrence renders plain. Empty for the common case
            // of only "all" placement groups.
            _onceOverlayCells = ResolveOnceOverlayCells(spin, ctx);
            // Resolve up-front which occurrences this spin actually paid (matched against the recorded located-
            // scatter wins). Computed for EVERY round: the tile builder uses it to pick the paid vs unpaid render
            // style, and — only when GateOnRecordedWin is set — to suppress the non-payers entirely. null means the
            // outcome could not be read for this spin => the tile builder fails open (paid look, never suppressed).
            _recordedGate = ResolveRecordedOverlayGate(spin, spinDetails, ctx, slotRoundReader);
        }

        /// <summary>
        /// Builds the HTML for one outcome tile at (<paramref name="reelIndex"/>, <paramref name="floorIndex"/>).
        /// For a configured multiplier symbol in scope the finalised amount is overlaid on the artwork; otherwise
        /// the plain symbol image is returned unchanged. <paramref name="symbolUrl"/> must already be resolved via
        /// Url.Content.
        /// </summary>
        public string BuildTile(string symbolUrl, string symbolName, int reelIndex, int floorIndex)
        {
            return BuildMultiplierTile(symbolUrl, symbolName, _ctx, reelIndex, floorIndex, _onceOverlayCells, _recordedGate);
        }

        /// <summary>The plain (no-overlay) tile markup — a bare symbol image. The single definition of that markup,
        /// used both here and by callers rendering with the feature off.</summary>
        public static string PlainTile(string symbolUrl)
        {
            return "<img src=\"" + symbolUrl + "\" >";
        }

        /// <summary>
        /// Builds the per-spin recorded-outcome gate: decides which PAID-class multiplier occurrences actually paid by
        /// matching each occurrence's computed amount against the spin's recorded located-scatter wins as a MULTISET
        /// — the very reconcile the Phase 1 validator does for logging, here promoted to a display decision. An
        /// occurrence whose amount finds an as-yet-unclaimed recorded win is "matched"; the rest did not pay. The grid
        /// is walked in render order so duplicate amounts are consumed the same way the tiles are drawn (a tie between
        /// two equal-amount cells resolves to the earlier one, matching how the loop paints). Only paid-class symbols
        /// are candidates: a statically-unpaid (TB) symbol shares an equal-value paid symbol's amount and must not be
        /// able to claim its win. Recorded zeros are not match targets (they are non-paying located scatters).
        ///
        /// Computed for every round. The result drives the paid vs unpaid render style always, and additionally
        /// suppresses non-payers when "MultiplierRecompute.GateOverlayOnRecordedWin" is on. Returns null if the
        /// recorded outcome cannot be read for this spin, so the caller FAILS OPEN (treats occurrences as paid)
        /// rather than dimming or hiding a win we merely failed to parse. An empty (non-null) gate is different: it
        /// means the spin genuinely recorded no paying located scatter, so nothing is matched.
        /// </summary>
        private static RecordedOverlayGate ResolveRecordedOverlayGate(
            SlotSymbolTableViewModel spin, string spinDetails, MultiplierOverlayContext ctx, ISlotRoundReader slotRoundReader)
        {
            if (ctx == null || spin?.Reels == null) return null;
            try
            {
                // Recorded located-scatter wins for this spin, read through the single reader. The reader already
                // drops non-paying zero markers, so these are all real amounts and valid match targets.
                var pool = slotRoundReader.GetOneSpinScatterWins(spinDetails);

                var gate = new RecordedOverlayGate();
                HashSet<string> onceSeen = null;

                foreach (var occ in SpinGrid.Occurrences(spin, ctx.Mapping, ctx.Computed))
                {
                    var p = occ.Params;
                    if (!p.Paid) continue;

                    if (p.Placement == MultiplierOverlayPlacement.OnceOnLastOccurrence)
                    {
                        // One overlay per group per spin: consume the recorded win once for the group,
                        // not once per in-group tile, mirroring the validator's once-group dedup.
                        string groupKey = p.GroupName ?? occ.Symbol;
                        if (onceSeen == null) onceSeen = new HashSet<string>();
                        if (onceSeen.Add(groupKey))
                        {
                            int gi = pool.IndexOf(occ.Amount);
                            if (gi >= 0) { pool.RemoveAt(gi); gate.MatchedOnceGroups.Add(groupKey); }
                        }
                    }
                    else
                    {
                        int gi = pool.IndexOf(occ.Amount);
                        if (gi >= 0) { pool.RemoveAt(gi); gate.MatchedCells.Add(new GridCell(occ.Reel, occ.Floor)); }
                    }
                }
                return gate;
            }
            catch (Exception ex)
            {
                // A single unparseable spin must not dim or hide overlays for the round; fail open (null gate =>
                // callers treat every occurrence as paid: paid style, never suppressed).
                sLog.WarnFormat("Recorded-outcome overlay gate failed for a spin (treating occurrences as paid): {0}", ex);
                return null;
            }
        }

        /// <summary>
        /// For each "once" placement group present in this spin, resolves the single cell that should carry the
        /// overlay: the LAST in-group occurrence in render order (reels left-to-right, floors top-to-bottom), keyed
        /// by group name. Only symbols that are in scope (see <see cref="MultiplierOverlayContext.InScope"/>) and have a
        /// computed amount are considered, so a group whose win did not resolve this spin contributes nothing.
        /// Returns an empty dictionary when there is no context or no "once" group occurs — the common path.
        /// The iteration order here mirrors the tile render loop so the chosen cell matches what is drawn.
        /// </summary>
        private static Dictionary<string, GridCell> ResolveOnceOverlayCells(SlotSymbolTableViewModel spin, MultiplierOverlayContext ctx)
        {
            var winners = new Dictionary<string, GridCell>();
            if (ctx == null || spin?.Reels == null) return winners;

            foreach (var occ in SpinGrid.Occurrences(spin, ctx.Mapping, ctx.Computed))
            {
                var p = occ.Params;
                if (p.Placement == MultiplierOverlayPlacement.OnceOnLastOccurrence && ctx.InScope(p))
                {
                    // Last assignment wins => the last in-group occurrence in render order.
                    winners[p.GroupName ?? occ.Symbol] = new GridCell(occ.Reel, occ.Floor);
                }
            }
            return winners;
        }

        /// <summary>
        /// Builds the HTML for a single outcome tile. For a configured multiplier symbol that is in scope
        /// (see <see cref="MultiplierOverlayContext.InScope"/>) and has a computed amount, the finalised amount is
        /// overlaid on top of the symbol artwork; otherwise the plain symbol image is returned unchanged.
        /// For a "once" placement group the overlay is drawn on a single cell per spin (see
        /// <see cref="ResolveOnceOverlayCells"/>); other in-group occurrences render plain.
        /// The overlay is styled by whether this occurrence paid this spin (from <paramref name="recordedGate"/>):
        /// a payer takes the group's paid style, a non-payer (a TB, or a paid-class symbol the recorded outcome did
        /// not confirm) takes the unpaid style. When <see cref="MultiplierOverlayContext.GateOnRecordedWin"/> is on,
        /// a non-payer is instead suppressed to the plain image, so only the paid style is ever drawn.
        /// <paramref name="symbolUrl"/> must already be resolved via Url.Content.
        /// <paramref name="reelIndex"/>/<paramref name="floorIndex"/> locate this tile in the spin grid.
        /// </summary>
        private static string BuildMultiplierTile(
            string symbolUrl,
            string symbolName,
            MultiplierOverlayContext ctx,
            int reelIndex,
            int floorIndex,
            Dictionary<string, GridCell> onceOverlayCells,
            RecordedOverlayGate recordedGate)
        {
            // Fallback in case the symbol is not in the mapping or has no computed amount: render the plain symbol image.
            decimal amount;
            if (ctx == null
                || string.IsNullOrEmpty(symbolName)
                || !ctx.Mapping.TryGet(symbolName, out MultiplierParams p)
                || !ctx.InScope(p)
                || !ctx.Computed.TryGetValue(symbolName, out amount))
            {
                return PlainTile(symbolUrl);
            }

            // "Once" placement: draw the overlay only on the resolved winning cell for this group; every other
            // in-group occurrence (e.g. the two trigger 'Wh' symbols) renders as the plain symbol image.
            if (p.Placement == MultiplierOverlayPlacement.OnceOnLastOccurrence)
            {
                if (onceOverlayCells == null
                    || !onceOverlayCells.TryGetValue(p.GroupName ?? symbolName, out var winner)
                    || winner.Reel != reelIndex
                    || winner.Floor != floorIndex)
                {
                    return PlainTile(symbolUrl);
                }
            }

            // Did THIS occurrence pay this spin? A statically-unpaid (TB) symbol never does. A paid-class symbol
            // does when the recorded located-scatter outcome confirms it (its cell, or its group for a "once"
            // placement, was matched by ResolveRecordedOverlayGate). A null gate means the outcome could not be
            // read -> fail open: treat as paid (paid look, and never suppressed) rather than dim/hide a possibly
            // -real win.
            bool paidThisSpin;
            if (recordedGate == null)
            {
                paidThisSpin = true;
            }
            else if (p.Placement == MultiplierOverlayPlacement.OnceOnLastOccurrence)
            {
                paidThisSpin = p.Paid && recordedGate.MatchedOnceGroups.Contains(p.GroupName ?? symbolName);
            }
            else
            {
                paidThisSpin = p.Paid && recordedGate.MatchedCells.Contains(new GridCell(reelIndex, floorIndex));
            }

            // Recorded-outcome gate (global "MultiplierRecompute.GateOverlayOnRecordedWin"). When ON, a non-paying
            // occurrence is suppressed entirely (plain image) and only payers render — so the unpaid style is never
            // reached in this mode. When OFF, nothing is suppressed here: both payers and non-payers render, and are
            // told apart below by the paid vs unpaid style.
            if (ctx.GateOnRecordedWin && !paidThisSpin)
            {
                return PlainTile(symbolUrl);
            }

            // Pick the render style by whether this occurrence paid. Both styles are fully resolved on the params
            // (unpaid falls back to paid unless the config supplied a distinct unpaid delta), so an un-styled config
            // yields the historical look for every tile.
            RenderStyle style = paidThisSpin ? p.PaidStyle : p.UnpaidStyle;

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

        /// <summary>
        /// Formats a finalised multiplier amount for display on a tile: no currency symbol, trailing zeros trimmed
        /// (e.g. 200, 25, 5.5), invariant culture for a stable decimal point.
        /// </summary>
        private static string FormatOverlayAmount(decimal amount)
        {
            return amount.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Holds everything the tile loop needs to overlay finalised multiplier amounts onto the outcome tiles:
    /// the symbol -> params mapping (for the paid flag) and the symbol -> computed_amount map. Produced once
    /// per game round by <see cref="MultiplierOverlayRenderer.TryCreate"/>.
    /// </summary>
    internal sealed class MultiplierOverlayContext
    {
        public bool GateOnRecordedWin { get; set; }
        public MultiplierSymbolMapping Mapping { get; set; }
        public IReadOnlyDictionary<string, decimal> Computed { get; set; }

        /// <summary>
        /// Whether a symbol's overlay is in scope for DISPLAY (i.e. a number is drawn at all). With gating OFF
        /// every configured multiplier symbol is in scope — both paid (B) and statically-unpaid (TB) — and the
        /// paid vs unpaid render STYLE (decided in BuildMultiplierTile from the recorded-outcome gate) tells the
        /// two apart. With gating ON only paid (B) symbols are in scope; a TB — which by definition did not pay —
        /// is never displayed, and non-paying paid-class occurrences are then suppressed by the gate itself.
        /// Recorded-gate CANDIDACY is deliberately not routed through here — only paid-class symbols may claim a
        /// recorded win (see ResolveRecordedOverlayGate), so a TB cannot steal an equal-value paid symbol's win.
        /// Used by tile build and once-cell resolution so those two visibility decisions agree.
        /// </summary>
        public bool InScope(MultiplierParams p) => p.Paid || !GateOnRecordedWin;
    }

    /// <summary>
    /// A single grid position (reel/column index, floor/row index) within one spin. Used to pin a
    /// "once" placement overlay to exactly one cell.
    /// </summary>
    internal struct GridCell : IEquatable<GridCell>
    {
        public int Reel { get; }
        public int Floor { get; }
        public GridCell(int reel, int floor) { Reel = reel; Floor = floor; }

        public bool Equals(GridCell other) => Reel == other.Reel && Floor == other.Floor;
        public override bool Equals(object obj) => obj is GridCell other && Equals(other);
        public override int GetHashCode() => (Reel * 397) ^ Floor;
    }

    /// <summary>
    /// Per-spin outcome of matching computed multiplier amounts against the recorded located-scatter wins.
    /// Resolved every round, independently of the Web.config flag. A NON-null (possibly empty) instance means "the
    /// recorded outcome was read": a listed occurrence paid this spin, and one NOT listed did not. What "did not
    /// pay" then looks like depends on <see cref="MultiplierOverlayContext.GateOnRecordedWin"/>: off (default) it is
    /// drawn with the unpaid render style; on it is suppressed to the plain symbol image. A null gate (returned on a
    /// read failure) means "could not be determined" and callers FAIL OPEN — every occurrence is treated as paid
    /// (paid style, never suppressed) rather than dimming or hiding a possibly-real win.
    ///  - <see cref="MatchedCells"/>: "all" placement occurrences whose computed amount matched a recorded win.
    ///  - <see cref="MatchedOnceGroups"/>: "once" placement groups that recorded a matching win this spin; the
    ///    single displayed cell is still chosen by the once-cell resolution.
    /// </summary>
    internal sealed class RecordedOverlayGate
    {
        public HashSet<GridCell> MatchedCells { get; } = new HashSet<GridCell>();
        public HashSet<string> MatchedOnceGroups { get; } = new HashSet<string>();
    }
}
