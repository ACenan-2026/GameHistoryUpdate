using GameHistory.Models;
using System.Globalization;



namespace GameHistory.MultiplierRecompute
{
    internal class ComputationHelpers
    {
        /// <summary>
        /// Tries to parse a string representation of a monetary value into a decimal.
        /// The method uses the invariant culture to ensure consistent parsing regardless of the system's locale settings.
        /// Assumes no currency symbols are present in the string.
        /// </summary>
        /// <param name="s">The string representation of the monetary value.</param>
        /// <param name="value">The parsed decimal value.</param>
        /// <returns>true if the parsing was successful; otherwise, false.</returns>
        internal static bool TryParseMoney(string s, out decimal value)
        {

            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }


    /// <summary>
    /// Defines the interface for multiplier base strategies. Each strategy will implement this interface to provide a
    /// specific way to calculate the base value for multipliers based on the game history slot model.
    /// Returns null when the base cannot be determined, so the caller can fall back to rendering the plain
    /// multiplier symbol rather than a wrong (or missing) value.
    /// </summary>
    public interface IMultiplierBaseStrategy
    {
        decimal? GetBase(GameHistoryGameInfoSlotModel gameInfoSlotModel);
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

        public decimal? GetBase(GameHistoryGameInfoSlotModel s) =>
            ComputationHelpers.TryParseMoney(s?.Bet, out decimal value) ? value : (decimal?)null;
    }

    /// <summary>
    /// Factory class to resolve the appropriate multiplier base strategy based on a given type.
    /// This allows for easy extension and addition of new strategies in the future.
    /// Returns null for an unknown / not-yet-implemented strategy type so the caller can degrade gracefully;
    /// the caller should log the null so a misconfigured strategy name surfaces.
    /// </summary>
    public static class MultiplierBaseStrategyResolver
    {
        public static IMultiplierBaseStrategy Resolve(string type)
        {
            switch(type)
            {
                case "TotalBet":
                    return TotalBetStrategy.Instance;
                default:
                    return null;
            }
        }
    }

}
