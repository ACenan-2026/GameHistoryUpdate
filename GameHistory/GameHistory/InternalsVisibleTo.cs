using System.Runtime.CompilerServices;

// Lets the unit-test assembly use internal overlay members (the MultiplierOverlayRenderer constructor) so tile
// rendering can be tested directly from an in-memory config.
[assembly: InternalsVisibleTo("GameHistory.Tests")]
