using System.Runtime.CompilerServices;

// Lets the unit-test assembly exercise internal overlay types (the SpinOverlay constructor and
// MultiplierOverlayContext) so the tile fallback-to-plain-image behaviour can be tested directly.
[assembly: InternalsVisibleTo("GameHistory.Tests")]
