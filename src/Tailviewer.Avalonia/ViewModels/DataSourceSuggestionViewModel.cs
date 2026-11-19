using Tailviewer.Settings;

namespace Tailviewer.Avalonia.ViewModels;

public sealed record DataSourceSuggestionViewModel(DataSource DataSource, string Prefix, string Midfix, string Postfix)
{
    public override string ToString() => Prefix + Midfix + Postfix;
}
