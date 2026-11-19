using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Tailviewer.Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var channel = SingleApplicationHelper.GetChannelName();
        using var owner = SingleApplicationHelper.TryAcquire(channel);
        if (owner == null)
        {
            try
            {
                SingleApplicationPipe.SendAsync(channel, args, TimeSpan.FromSeconds(5), CancellationToken.None).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or TimeoutException or UnauthorizedAccessException or ArgumentException)
            {
                return AppBuilder.Configure(() => new App(null, "Cannot contact the existing Tailviewer instance. " + error.Message))
                    .UsePlatformDetect().StartWithClassicDesktopLifetime(Array.Empty<string>());
            }
        }
        App? app = null;
        try { return AppBuilder.Configure(() => app = new App(channel, null)).UsePlatformDetect().StartWithClassicDesktopLifetime(args); }
        finally { app?.StopHandoff(); }
    }
}

public sealed class App : Application
{
    private readonly string? _channel;
    private readonly string? _startupError;
    private CancellationTokenSource? _stopping;
    private Task? _listener;

    public App() { }
    internal App(string? channel, string? startupError) { _channel = channel; _startupError = startupError; }

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (_startupError != null)
            {
                var close = new Button { Content = "Close" };
                var error = new Window
                {
                    Title = "Tailviewer — connection error", Width = 560, Height = 220,
                    Content = new StackPanel { Margin = new Thickness(20), Spacing = 16,
                        Children = { new TextBlock { Text = _startupError, TextWrapping = TextWrapping.Wrap }, close } }
                };
                close.Click += (_, _) => error.Close();
                desktop.MainWindow = error;
                desktop.Exit += (_, args) => args.ApplicationExitCode = 1;
                base.OnFrameworkInitializationCompleted();
                return;
            }
            var window = new MainWindow();
            _stopping = new CancellationTokenSource();
            var stopping = _stopping.Token;
            desktop.Exit += (_, _) => _stopping?.Cancel();
            window.Opened += async (_, _) =>
            {
                await window.OpenFilesAsync(desktop.Args ?? Array.Empty<string>());
                if (_channel != null && !stopping.IsCancellationRequested)
                    _listener = ListenAsync(window, _channel, stopping);
            };
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static async Task ListenAsync(MainWindow window, string channel, CancellationToken stopping)
    {
        try
        {
            await SingleApplicationPipe.ListenAsync(channel, async (paths, token) =>
                await Dispatcher.UIThread.InvokeAsync(() => { _ = window.AcceptHandoff(paths, token); }, DispatcherPriority.Normal, token),
                TimeSpan.FromSeconds(5), stopping).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            if (!stopping.IsCancellationRequested)
                Dispatcher.UIThread.Post(() => { if (!stopping.IsCancellationRequested) window.ReportHandoffError(error.Message); });
        }
    }

    internal void StopHandoff()
    {
        if (_stopping == null) return;
        _stopping.Cancel();
        try { _listener?.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
        finally { _stopping.Dispose(); _stopping = null; }
    }
}
