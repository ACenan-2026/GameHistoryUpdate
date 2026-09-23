using GameHistory.Models;
using log4net;
using System.Collections.Generic;
using System.Globalization;



namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// Defines the interface for multiplier strategies. Each strategy computes the finalised amount to
    /// overlay on a multiplier symbol, given the full game-round model and that symbol's config params.
    /// Most strategies compute base × params.Multiplier from round-level data (the total bet); some
    /// (e.g. <see cref="TotalScatterWinStrategy"/>) instead read the recorded outcome from the round. The full
    /// <see cref="GameHistoryGameInfoModel"/> is passed so a strategy can reach either.
    /// Returns null when the amount cannot be determined, so the caller can fall back to rendering the
    /// plain multiplier symbol rather than a wrong (or missing) value.
    /// </summary>
    public interface IMultiplierBaseStrategy
    {
        decimal? GetWonAmount(ISlotRoundReader slotRoundReader, MultiplierParams multiplierParams);
    }


    /// <summary>
    /// A concrete implementation of the IMultiplierBaseStrategy interface that calculates the base value for multipliers
    /// Uses the total bet amount from the game history slot model as the base value.
    /// </summary>
    public sealed class TotalBetStrategy : IMultiplierBaseStrategy
    {
        /// <summary>
        /// Shared stateless instance. TotalBetStrategy holds no per-game state, so a single instance can be reused.
        /// </summary>
        public static readonly TotalBetStrategy Instance = new TotalBetStrategy();

        public decimal? GetWonAmount(ISlotRoundReader slotRoundReader, MultiplierParams multiplierParams) =>
            slotRoundReader.GetTotalBet() * multiplierParams.Multiplier;
    }

    /// <summary>
    /// Computes base = total_bet * numLines / staticBetMultiplier (multiply before divide, so an unreduced
    /// ratio like 50/75 is as exact as 2/3). This is the line-bet total expressed as a fixed fraction of the
    /// total bet, using the game's own constants.
    ///
    /// ASSUMES the ratio is constant for the game. It is fed the game constants numLines/staticBetMultiplier
    /// ("LineBetWithStaticMult"). Do NOT use for games where the line count (or the bet multiplier) can vary
    /// per spin: there the ratio is not constant and total_bet alone cannot recover the base.
    /// </summary>
    public sealed class LineBetWithStaticMultStrategy : IMultiplierBaseStrategy
    {
        // The fixed game constants that define the line-bet-total : total-bet ratio.
        private readonly decimal _numLines;
        private readonly decimal _staticBetMultiplier;

        public LineBetWithStaticMultStrategy(decimal numLines, decimal staticBetMultiplier)
        {
            _numLines = numLines;
            _staticBetMultiplier = staticBetMultiplier;
        }

        public decimal? GetWonAmount(ISlotRoundReader slotRoundReader, MultiplierParams multiplierParams) =>
            slotRoundReader.GetTotalBet() is decimal totalBet
                ? decimal.Round(totalBet * _numLines / _staticBetMultiplier, 2, System.MidpointRounding.AwayFromZero) * multiplierParams.Multiplier
                : (decimal?)null;
    }

    /// <summary>
    /// Reads the finalised amount straight from the recorded outcome instead of computing it. Use this
    /// for games where the multiplier is resolved by a mechanic that history does not fully record — for
    /// example a fortune wheel that can land a base×wheel multiplier OR a fixed jackpot (Mini/Minor/
    /// Major/Grand) — but that always records the resulting located-scatter win.
    ///
    /// It returns the scatter-win WinAmount(s) recorded in the spin Details, which isolates the
    /// multiplier win from other wins: each Details entry carries a "Type" — a payline win is "Type: Payline",
    /// a scatter-kind win is "Type: Basic_Scatter" — and the entry that actually PAID carries a non-empty
    /// WinAmount. Which "Type" tokens count is a code-owned concern (see
    /// <see cref="SlotRoundReader.IsScatterWinCategory"/>), so the game config never has to name a database
    /// token. When no paying scatter win is recorded — e.g. a spin where the Wh symbol appeared but the wheel did
    /// not trigger and only a payline paid — it returns null so the tile renders plain rather than showing an
    /// unrelated win. It deliberately does NOT fall back to the round's total Won, which would include payline wins.
    /// It is stateless — no config values are needed; the config only lists which symbol(s) are the target.
    ///
    /// ASSUMES one located pay per record, shown on the one visible overlay symbol. If a record ever
    /// carried multiple located pays this returns their sum, which is only meaningful for a single tile.
    /// Returns null when no located-scatter amount is recorded, so the caller renders the plain symbol.
    /// </summary>
    public sealed class TotalScatterWinStrategy : IMultiplierBaseStrategy
    {
        /// <summary>Shared stateless instance — the strategy reads only the recorded outcome.</summary>
        public static readonly TotalScatterWinStrategy Instance = new TotalScatterWinStrategy();
        public decimal? GetWonAmount(ISlotRoundReader slotRoundReader, MultiplierParams multiplierParams)
        {
            // Overlay ONLY a paying scatter-kind win. A spin can show a multiplier symbol without the wheel
            // feature triggering — e.g. only a payline win, with no paying scatter. The Details entries
            // distinguish these by "Type" (a payline win is "Type: Payline"; a scatter-kind win is
            // "Type: Basic_Scatter" — see SlotRoundReader.IsScatterWinCategory) plus a non-empty WinAmount, so
            // a payline win is ignored and we return null → the Wh tile renders plain rather than greedily showing
            // an unrelated win. Do NOT fall back to the round's total Won: that total includes payline wins and
            // would be painted onto an uninvolved tile.
            var total = slotRoundReader.GetScatterWinsTotal();
            return total > 0m ? total : (decimal?)null;
        }
    }


    /// <summary>
    /// A fixed prize attached to the symbol itself — a jackpot tier (Mini/Minor/Major/Grand) whose award
    /// does NOT depend on the bet. There is nothing to compute from the round: the configured amount IS the
    /// win, so this returns <see cref="MultiplierParams.Multiplier"/> as-is (the round is ignored). Returns
    /// null when no amount is configured, so the tile renders plain rather than a zero.
    ///
    /// IMPORTANT — units. The whole recompute pipeline works in money (large-denomination) figures:
    /// <see cref="ISlotRoundReader.GetTotalBet"/> is the money bet and every other strategy returns a money
    /// amount. So the config 'value' for a FixedAmount symbol must ALSO be the money (large-denomination)
    /// figure the paytable shows — e.g. Sweet Chilli's Mini = 40 (dollars), not its 1000-credit weight.
    /// The round model carries NO denomination, so a fixed CREDIT figure cannot be converted to money here;
    /// if a game's paytable value is in credits (a multi-denom game), the config must pre-convert it to the
    /// intended denomination's money value (or the model must start recording the denom). See
    /// MultiplierConfigSchema.md.
    /// </summary>
    public sealed class FixedAmountStrategy : IMultiplierBaseStrategy
    {
        /// <summary>Shared stateless instance — the amount comes entirely from the per-symbol config value.</summary>
        public static readonly FixedAmountStrategy Instance = new FixedAmountStrategy();

        public decimal? GetWonAmount(ISlotRoundReader slotRoundReader, MultiplierParams multiplierParams) =>
            multiplierParams?.Multiplier is int amount ? (decimal?)amount : null;
    }


    /// <summary>
    /// Factory class to resolve the appropriate multiplier base strategy based on a given type.
    /// This allows for easy extension and addition of new strategies in the future.
    /// Returns null for an unknown / not-yet-implemented strategy type so the caller can degrade gracefully;
    /// the caller should log the null so a misconfigured strategy name surfaces.
    /// </summary>
    public static class MultiplierBaseStrategyResolver
    {

        private static readonly ILog sLog = LogManager.GetLogger(typeof(MultiplierBaseStrategyResolver));

        /// <summary>
        /// Given a dictionary, attempts to get the integer value assoicated with a specified key.
        /// Returns true if the key exists and the value can be parsed as an integer; otherwise, returns false.
        /// </summary>
        /// <param name="attrs">The dictionary to read from</param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool TryGetInt(IReadOnlyDictionary<string, string> attrs, string key, out int value)
        {
            value = 0;
            return attrs != null
                && attrs.TryGetValue(key, out var raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static IMultiplierBaseStrategy Resolve(string type, IReadOnlyDictionary<string, string> attributes)
        {
            switch(type)
            {
                case "TotalBet":
                    return TotalBetStrategy.Instance;
                case "LineBetWithStaticMult":
                    // Self-documenting, game-constant form: base = total_bet * numLines / staticBetMultiplier.
                    // Assumes both are fixed for the game (see LineBetWithStaticMultStrategy); not for variable-line games.
                    if (!TryGetInt(attributes, "numLines", out int lines) ||
                        !TryGetInt(attributes, "staticBetMultiplier", out int staticMult) ||
                        staticMult == 0)
                    {
                        sLog.WarnFormat("LineBetWithStaticMult strategy requires 'numLines' and non-zero 'staticBetMultiplier' attributes.");
                        return null;
                    }
                    return new LineBetWithStaticMultStrategy(lines, staticMult);
                case "TotalScatterWin":
                    // Reads the finalised amount straight from the recorded located-scatter win — for
                    // wheel/jackpot games where the amount can't be reconstructed from config values.
                    return TotalScatterWinStrategy.Instance;
                case "FixedAmount":
                    // A fixed prize carried by the symbol itself (a jackpot tier): the config 'value' is the
                    // money amount and is rendered as-is, independent of the bet. See FixedAmountStrategy.
                    // DO NOT USE YET -- TWO DIFFERENT CURRENCIES WILL REQUIRE TWO DIFFERENT VALUES WHICH THE CONFIG FILE CANNOT 
                    // KNOW AHEAD OF TIME
                    sLog.WarnFormat("FixedAmount strategy is not yet supported due to currency issues.");
                    return null;
                    // return FixedAmountStrategy.Instance;
                default:
                    return null;
            }
        }
    }

}
