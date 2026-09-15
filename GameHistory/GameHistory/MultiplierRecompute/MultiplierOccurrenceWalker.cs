using GameHistory.Models;
using System.Collections.Generic;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// One configured multiplier symbol sitting on a specific grid cell, with its resolved (computed) amount.
    /// Produced by <see cref="SpinGrid.Occurrences"/> so the display code and the validator share one grid
    /// traversal and one common guard, then each applies its own predicate and reduction.
    /// </summary>
    public readonly struct MultiplierOccurrence
    {
        public int Reel { get; }
        public int Floor { get; }
        public string Symbol { get; }
        public MultiplierParams Params { get; }
        public decimal Amount { get; }
        public MultiplierOccurrence(int reel, int floor, string symbol, MultiplierParams p, decimal amount)
        {
            Reel = reel;
            Floor = floor;
            Symbol = symbol;
            Params = p;
            Amount = amount;
        }
    }

    public static class SpinGrid
    {
        /// <summary>
        /// Walks one spin's grid in render order (reels left-to-right, floors top-to-bottom) and yields every cell
        /// whose symbol is a configured multiplier that has a computed amount. This is the single owner of the grid
        /// traversal and its null-checks; each caller adds its own predicate (Paid / InScope / placement) and its
        /// own reduction. Reel/floor indices are advanced for every reel and floor — matching the tile render loop —
        /// so the yielded coordinates line up with what is drawn.
        /// </summary>
        public static IEnumerable<MultiplierOccurrence> Occurrences(
            SlotSymbolTableViewModel spin,
            MultiplierSymbolMapping mapping,
            IReadOnlyDictionary<string, decimal> computed)
        {
            if (spin?.Reels == null || mapping == null || computed == null) yield break;

            int reel = 0;
            foreach (var reelItem in spin.Reels)
            {
                int floor = 0;
                if (reelItem?.Floors != null)
                {
                    foreach (var floorItem in reelItem.Floors)
                    {
                        string symbol = floorItem?.SymbolName;
                        if (!string.IsNullOrEmpty(symbol)
                            && mapping.TryGet(symbol, out var p)
                            && computed.TryGetValue(symbol, out var amount))
                        {
                            yield return new MultiplierOccurrence(reel, floor, symbol, p, amount);
                        }
                        floor++;
                    }
                }
                reel++;   // advance for every reel, even a null/empty one, to stay aligned with the render loop
            }
        }
    }
}
