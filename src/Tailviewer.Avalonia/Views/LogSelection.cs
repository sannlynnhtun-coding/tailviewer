namespace Tailviewer.Avalonia.Views;

// Columns are UTF-16 offsets, matching Avalonia text selection positions.
public readonly record struct LogSelection(int AnchorLine, int AnchorColumn, int ActiveLine, int ActiveColumn);
