using System.Globalization;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// Small parsing helpers shared by the reader and the strategies. Kept here (rather than in the strategies
    /// file) so it is easy to find and reuse; culture-invariant so monetary values parse the same regardless of
    /// the server's locale.
    /// </summary>
    internal static class ComputationHelpers
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
}
