using GameHistory.Models;
using log4net;
using System.Collections.Generic;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// One recorded scatter-kind win from a spin's Details string: the amount it paid and how many symbols the
    /// engine counted toward it (<c>NumSymbols</c>, 1 when absent). A count-scatter pay such as Cyber Cash's
    /// "13 x P1 anywhere" is recorded as a single entry (975, 13), not one entry per tile.
    ///
    /// <see cref="SharesLeft"/> is the only mutable state: the per-spin matching pool (render gate / validator)
    /// consumes it as occurrences claim the entry. It starts at <see cref="NumSymbols"/>. The reader hands out
    /// fresh instances on every read, so consuming one pool never affects another caller.
    /// </summary>
    public sealed class RecordedScatterWin
    {
        public decimal Amount { get; }
        public int NumSymbols { get; }
        public int SharesLeft { get; set; }

        public RecordedScatterWin(decimal amount, int numSymbols, int sharesLeft)
        {
            Amount = amount;
            NumSymbols = numSymbols > 0 ? numSymbols : 1;   // never 0: a win is attributed to at least one symbol
            SharesLeft = sharesLeft;
        }

        /// <summary>A fresh, unclaimed entry (all shares left).</summary>
        public RecordedScatterWin(decimal amount, int numSymbols = 1)
            : this(amount, numSymbols, numSymbols > 0 ? numSymbols : 1)
        {
        }

        /// <summary>True while nothing has claimed any part of this entry.</summary>
        public bool IsUntouched => SharesLeft == NumSymbols;

        /// <summary>An independent copy carrying the same claim state.</summary>
        public RecordedScatterWin Copy() => new RecordedScatterWin(Amount, NumSymbols, SharesLeft);
    }

    /// <summary>
    /// Claim operations over one spin's pool of recorded scatter wins. Shared by the render gate
    /// (<see cref="SpinOverlay"/>) and the Phase 1 validator so both consume the pool by the same rule.
    /// </summary>
    internal static class RecordedScatterWinPool
    {
        private static readonly ILog sLog = LogManager.GetLogger(typeof(RecordedScatterWinPool));

        // The strategy exempted from amount matching (see TryClaim). One definition, shared with the strategy
        // resolver, so the two can't drift apart.
        internal const string TotalScatterWinStrategyName = "TotalScatterWin";

        /// <summary>
        /// Claims all or part of a recorded win for one occurrence. This is the single entry point the render gate
        /// and the validator use, so both always claim by the same rule:
        /// <list type="bullet">
        /// <item><c>TotalScatterWin</c> strategy: no amount matching. Under that strategy's documented contract (one
        /// recorded scatter win, one spin, sole paid group; see MultiplierConfigSchema.md, Strategies notes) every
        /// scatter win in the spin is its win, so they are all consumed and the claim succeeds iff the spin recorded
        /// any. Its record may carry NumSymbols &gt; 1 (several trigger tiles), which is why it bypasses the whole rule.</item>
        /// <item><see cref="MultiplierClaims.Shared"/>: <see cref="TryClaimShared"/>.</item>
        /// <item>otherwise: <see cref="TryClaimWhole"/>.</item>
        /// </list>
        /// A null <paramref name="p"/> falls back to a whole claim (the historical behaviour) rather than throwing.
        /// </summary>
        internal static bool TryClaim(this List<RecordedScatterWin> pool, MultiplierParams p, decimal amount)
        {
            if (p?.Strategy?.Type == TotalScatterWinStrategyName)
            {
                // Nothing recorded this spin -> did not pay. (Computed carries the ROUND total, so in a multi-spin
                // round the tile appears on every spin; without this a scatter-less spin would be styled as paid.)
                if (pool == null || pool.Count == 0) return false;

                foreach (var win in pool) win.SharesLeft = 0;   // sole-group contract: every scatter win is its own
                return true;
            }

            return p?.Claims == MultiplierClaims.Shared ? pool.TryClaimShared(amount) : pool.TryClaimWhole(amount);
        }

        /// <summary>
        /// WHOLE claim: the first entry that is still untouched, was recorded over exactly ONE symbol
        /// (<see cref="RecordedScatterWin.NumSymbols"/> == 1) and whose full <see cref="RecordedScatterWin.Amount"/>
        /// equals <paramref name="amount"/> is consumed entirely. Returns false when nothing matches.
        ///
        /// ASSUMPTION (NumSymbols == 1): a whole claim is one occurrence's own win, and every pulled located-scatter
        /// game (LaughingDragon, NorseLegend, RedEclipseRiches, SweetChilli, TreasureSpiritsDragon, EagleDollar) records
        /// each PAYING scatter entry with NumSymbols = 1. An entry over several symbols is a count win (claim="shared")
        /// and must not be swallowed whole by a single tile of equal value — e.g. a 100 whole tile taking the (100, 2)
        /// record of two 50 shared tiles. TotalScatterWin (whose wheel record can carry NumSymbols &gt; 1) never reaches
        /// here: TryClaim exempts it. A rejection is logged so a game breaking the assumption shows up in the logs.
        /// </summary>
        internal static bool TryClaimWhole(this List<RecordedScatterWin> pool, decimal amount)
        {
            if (pool == null) return false;
            var win = pool.Find(w => w.IsUntouched && w.NumSymbols == 1 && w.Amount == amount);
            if (win != null)
            {
                win.SharesLeft = 0;
                return true;
            }

            // Not claimable. If the only reason is the NumSymbols rule, say so: either a count win correctly kept from
            // a whole claim, or a game whose single-symbol wins record NumSymbols > 1 (the assumption above broken).
            var multiSymbol = pool.Find(w => w.IsUntouched && w.NumSymbols > 1 && w.Amount == amount);
            if (multiSymbol != null)
            {
                sLog.WarnFormat(
                    "Whole claim of {0} rejected: the matching recorded win is counted over {1} symbols (whole claims " +
                    "require NumSymbols == 1). Expected for a count win (claim=\"shared\"); otherwise this game breaks " +
                    "the single-symbol assumption.",
                    amount, multiSymbol.NumSymbols);
            }
            return false;
        }

        /// <summary>
        /// SHARED claim — for count-scatter wins recorded as one entry over several symbols: the first entry with a
        /// share left whose amount is exactly <paramref name="amount"/> × <see cref="RecordedScatterWin.NumSymbols"/>
        /// gives up one share (e.g. a 75 tile against (975, 13)). Compared by multiplication so no division rounding
        /// is involved. For a NumSymbols = 1 entry a share IS the whole amount. Returns false when nothing matches.
        /// </summary>
        internal static bool TryClaimShared(this List<RecordedScatterWin> pool, decimal amount)
        {
            if (pool == null) return false;
            var win = pool.Find(w => w.SharesLeft > 0 && w.Amount == amount * w.NumSymbols);
            if (win == null) return false;
            win.SharesLeft -= 1;
            return true;
        }
    }

    /// <summary>
    /// Reads the parts of a pulled Game History round (the deserialized <see cref="GameHistoryGameInfoModel"/>)
    /// that the multiplier-recompute feature needs: the total bet, the per-spin grids and detail entries, and the
    /// recorded located-scatter win amounts. This is the single surface that knows the model's shape and the
    /// semi-structured "Details" format, so no other code has to navigate the DTO or parse that string.
    /// </summary>
    public interface ISlotRoundReader
    {
        /// <summary>The round's total bet, or null when it cannot be parsed.</summary>
        decimal? GetTotalBet();

        /// <summary>Per-spin recorded scatter wins (one inner list per spin; empty when a spin has none). Paying
        /// entries only (zero markers dropped); fresh, unclaimed instances on every call.</summary>
        List<List<RecordedScatterWin>> GetScatterWins();

        /// <summary>Recorded scatter wins (amount + NumSymbols) parsed from a single spin's Details string. Paying
        /// entries only; fresh, unclaimed instances on every call.</summary>
        List<RecordedScatterWin> GetOneSpinScatterWins(string details);

        /// <summary>Sum of every recorded scatter win across the round.</summary>
        decimal GetScatterWinsTotal();

        /// <summary>The per-spin grid stop positions.</summary>
        List<SlotUserPositionKeyValuePair> GetUserPositionDict();

        /// <summary>The per-spin detail entries (bet, outcome, Details string, ...).</summary>
        List<GameHistorySlotPositionDetailModel> GetSlotDetails();

        /// <summary>The round-level slot model (bet, game name, times, ...).</summary>
        GameHistoryGameInfoSlotModel GetSlotModel();

        /// <summary>The game name, or null when unavailable.</summary>
        string GetGameName();
    }
}
