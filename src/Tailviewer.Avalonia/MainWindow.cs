using Avalonia;
using Avalonia.Collections;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Tailviewer.Api;
using Tailviewer.Core;
using Tailviewer.Settings;
using Tailviewer.Avalonia.ViewModels;
using System.Xml;
using Avalonia.VisualTree;

namespace Tailviewer.Avalonia;

public sealed class MainWindow : Window
{
    private readonly Button _open = new() { Content = "_Open file…" };
    private readonly Button _copy = new() { Content = "_Copy selected", IsEnabled = false };
    private readonly Button _closeSource = new() { Content = "C_lose", IsEnabled = false };
    private readonly Button _closeAll = new() { Content = "Close _all", IsEnabled = false };
    private readonly Button _newGroup = new() { Content = "New _group" };
    private readonly CheckBox _follow = new() { Content = "Follow _tail", IsChecked = true };
    private readonly TextBlock _path = new() { Text = "Open a log file to begin.", TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = new() { Text = "Select lines with Shift/Ctrl • Ctrl/Cmd+C to copy" };
    private readonly ListBox _rows = new() { SelectionMode = SelectionMode.Multiple };
    private readonly SourcesViewModel _workspace = new();
    private AvaloniaList<DataSource> _sources => _workspace.Roots;
    private readonly TreeView _sourceTree = new();
    private readonly Dictionary<DataSourceId, FolderSources> _folders = new();
    private readonly Dictionary<DataSourceId, AvaloniaList<DataSource>> _folderChildren = new();
    private readonly DefaultTaskScheduler _folderScheduler = new();
    private readonly Lazy<IFilesystem> _folderFilesystem;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private LogDocument? _document;
    private string? _operationError;
    private bool _closed;
    private bool _changingSourceSelection;
    private long _openRequest;

    public MainWindow(string? fileName = null)
    {
        _folderFilesystem = new Lazy<IFilesystem>(() => new Filesystem(_folderScheduler));
        DataContext = _workspace;
        Title = "Tailviewer — Avalonia prototype";
        using var logoStream = AssetLoader.Open(new Uri("avares://Tailviewer.Avalonia/Assets/Tailviewer.png"));
        var logo = new Bitmap(logoStream);
        using var iconStream = AssetLoader.Open(new Uri("avares://Tailviewer.Avalonia/Assets/Tailviewer.png"));
        Icon = new WindowIcon(iconStream);
        var brandImage = new Image { Source = logo, Width = 28, Height = 28, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(brandImage, "Tailviewer logo");
        Width = 1100;
        Height = 700;
        MinWidth = 640;
        MinHeight = 360;
        AutomationProperties.SetName(_rows, "Log lines");
        AutomationProperties.SetName(_sourceTree, "Sources");
        AutomationProperties.SetName(_closeSource, "Close current source");
        AutomationProperties.SetName(_closeAll, "Close all sources");
        AutomationProperties.SetName(_newGroup, "New group");
        _sourceTree.ItemsSource = _sources;
        // shortcut: groups expose members; timestamp-aware aggregate views remain US2.
        _sourceTree.ItemTemplate = new FuncTreeDataTemplate<DataSource>((source, _) =>
        {
            var path = source.File ?? source.LogFileFolderPath ?? source.DisplayName ?? "Merged Data Source";
            var name = SourcesViewModel.IsGroup(source) ? path : Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            var label = new TextBlock { Text = string.IsNullOrEmpty(name) ? path : name, TextTrimming = TextTrimming.CharacterEllipsis };
            ToolTip.SetTip(label, path);
            AutomationProperties.SetName(label, path);
            label.ContextMenu = CreateSourceMenu(source);
            label.AttachedToVisualTree += (_, _) =>
            {
                var item = label.FindAncestorOfType<TreeViewItem>();
                if (item != null) item.ContextMenu = label.ContextMenu;
            };
            return label;
        }, source => _folderChildren.TryGetValue(source.Id, out var children) ? children : _workspace.GetChildren(source.Id));
        _sourceTree.ContainerPrepared += (_, args) =>
        {
            if (args.Container is TreeViewItem item) item.IsExpanded = true;
        };
        _sourceTree.AddHandler(InputElement.ContextRequestedEvent, (_, args) =>
        {
            var owner = (args.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>()
                .FirstOrDefault(control => control.ContextMenu?.Tag is Action);
            (owner?.ContextMenu?.Tag as Action)?.Invoke();
        }, RoutingStrategies.Tunnel);
        ScrollViewer.SetHorizontalScrollBarVisibility(_rows, ScrollBarVisibility.Auto);
        _rows.ItemTemplate = new FuncDataTemplate<int>((index, _) =>
        {
            var row = _document?.GetLine(index);
            return new TextBlock
            {
                DataContext = row,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,monospace"),
                FontSize = 14,
                [!TextBlock.TextProperty] = new Binding(nameof(LogLine.DisplayText))
            };
        });
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { brandImage, _open, _copy, _follow }
        };
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("200,*"),
            Margin = new Thickness(12), RowSpacing = 8, ColumnSpacing = 8
        };
        Grid.SetColumnSpan(toolbar, 2);
        layout.Children.Add(toolbar);
        Grid.SetRow(_path, 1);
        Grid.SetColumnSpan(_path, 2);
        var sourcePanel = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 8 };
        sourcePanel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, Children = { _closeSource, _closeAll }
        });
        Grid.SetRow(_newGroup, 1);
        sourcePanel.Children.Add(_newGroup);
        Grid.SetRow(_sourceTree, 2);
        sourcePanel.Children.Add(_sourceTree);
        Grid.SetRow(sourcePanel, 2);
        Grid.SetRow(_rows, 2);
        Grid.SetColumn(_rows, 1);
        Grid.SetRow(_status, 3);
        Grid.SetColumnSpan(_status, 2);
        layout.Children.Add(_path);
        layout.Children.Add(sourcePanel);
        layout.Children.Add(_rows);
        layout.Children.Add(_status);
        Content = layout;

        _open.Click += async (_, _) => await PickFileAsync();
        _copy.Click += async (_, _) => await CopyAsync();
        _closeSource.Click += async (_, _) => await CloseCurrentSourceAsync();
        _closeAll.Click += (_, _) => CloseAllSources();
        _newGroup.Click += async (_, _) =>
        {
            if (!_closed) await ActivateSourceAsync(_workspace.AddGroup());
        };
        _sourceTree.SelectionChanged += async (_, _) =>
        {
            UpdateCloseCommands();
            if (!_changingSourceSelection && _sourceTree.SelectedItem is DataSource source && source.Id != _document?.SourceId)
                await ActivateSourceAsync(source);
        };
        _follow.IsCheckedChanged += (_, _) => ScrollToTail();
        _rows.SelectionChanged += (_, _) =>
        {
            _copy.IsEnabled = _rows.Selection.SelectedIndexes.Count > 0;
            if (_copy.IsEnabled)
                _follow.IsChecked = false;
        };
        _rows.AddHandler(PointerWheelChangedEvent, (_, _) => _follow.IsChecked = false, RoutingStrategies.Tunnel);
        _rows.AddHandler(PointerPressedEvent, (_, _) => _follow.IsChecked = false, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _timer.Tick += (_, _) => RefreshDocument();
        Closed += (_, _) =>
        {
            _closed = true;
            ++_openRequest;
            _timer.Stop();
            _document?.Dispose();
            foreach (var folder in _folders.Values) folder.Dispose();
            _folderScheduler.Dispose();
            logo.Dispose();
        };
        _timer.Start();
        if (fileName != null)
            Opened += async (_, _) => await OpenFileAsync(fileName);
    }

    private async Task PickFileAsync()
    {
        try
        {
            if (!StorageProvider.CanOpen)
                throw new NotSupportedException("File picking is unavailable. Pass a local file path on the command line.");
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open log file", AllowMultiple = false
            });
            if (files.Count == 0)
                return;
            var path = files[0].TryGetLocalPath();
            if (path == null)
                throw new NotSupportedException("This prototype opens local files only.");
            await OpenFileAsync(path);
        }
        catch (Exception ex)
        {
            _status.Text = _operationError = ex.Message;
        }
    }

    public Task OpenFileAsync(string fileName) => OpenSourceAsync(fileName, null);

    private async Task ActivateSourceAsync(DataSource source)
    {
        if (_closed) return;
        if (!SourcesViewModel.IsGroup(source))
        {
            await OpenSourceAsync(source.File ?? source.LogFileFolderPath, source);
            return;
        }
        ++_openRequest;
        _open.IsEnabled = true;
        SaveFollowState();
        ClearDocument();
        SelectSource(source);
        _path.Text = source.DisplayName;
        Title = $"{source.DisplayName} — Tailviewer prototype";
        RefreshDocument();
    }

    private ContextMenu CreateSourceMenu(DataSource source)
    {
        var menu = new ContextMenu();
        void RefreshMenu()
        {
            menu.Items.Clear();
            if (!_workspace.Contains(source.Id)) return;
            var add = new MenuItem { Header = "Add to group", IsEnabled = source.ParentId == DataSourceId.Empty };
            foreach (var group in _workspace.All.Where(SourcesViewModel.IsGroup))
            {
                var target = new MenuItem { Header = group.DisplayName, Tag = group.Id };
                target.Click += async (_, _) => await ChangeGroupAsync(source, () => _workspace.AddToGroup(source.Id, group.Id));
                add.Items.Add(target);
            }
            add.IsEnabled &= add.Items.Count > 0;
            menu.Items.Add(add);
            if (source.ParentId != DataSourceId.Empty)
            {
                var remove = new MenuItem { Header = "Remove from group" };
                remove.Click += async (_, _) => await ChangeGroupAsync(source, () => _workspace.Ungroup(source.Id));
                menu.Items.Add(remove);
            }
        }
        RefreshMenu();
        // Refresh before context routing opens menus created before a group existed.
        menu.Tag = (Action)RefreshMenu;
        return menu;
    }

    private async Task ChangeGroupAsync(DataSource source, Action change)
    {
        if (_closed) return;
        try
        {
            change();
            await ActivateSourceAsync(source);
        }
        catch (Exception error) when (error is XmlException || error is ArgumentException)
        {
            _status.Text = _operationError = "Cannot change group: " + error.Message;
        }
    }

    private async Task OpenSourceAsync(string fileName, DataSource? selected)
    {
        var request = ++_openRequest;
        _open.IsEnabled = false;
        UpdateCloseCommands();
        try
        {
            if (_closed) return;
            var path = Path.GetFullPath(fileName);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var source = selected ?? _workspace.All.FirstOrDefault(item => string.Equals(item.File ?? item.LogFileFolderPath, path, comparison));
            var isFolder = source?.LogFileFolderPath != null ||
                (source == null && await Task.Run(() => Directory.Exists(path)));
            if (_closed || request != _openRequest) return;
            if (isFolder)
            {
                if (source == null)
                {
                    var defaults = new DataSourceSettings();
                    source = new DataSource
                    {
                        Id = DataSourceId.CreateNew(), LogFileFolderPath = path,
                        LogFileSearchPattern = defaults.FolderDataSourcePattern, Recursive = defaults.FolderDataSourceRecursive,
                        FollowTail = true
                    };
                }
                if (!_folders.ContainsKey(source.Id))
                {
                    var folder = await Task.Run(() => new FolderSources(_folderFilesystem.Value, source));
                    if (_closed || request != _openRequest)
                    {
                        folder.Dispose();
                        return;
                    }
                    _folders.Add(source.Id, folder);
                    _folderChildren.Add(source.Id, new AvaloniaList<DataSource>());
                    var id = source.Id;
                    folder.Changed += () => Dispatcher.UIThread.Post(() => RefreshFolder(id));
                    RefreshFolder(id);
                }
                SaveFollowState();
                _rows.ItemsSource = null;
                _document?.Dispose();
                _document = null;
                if (!_sources.Contains(source)) _sources.Add(source);
                SelectSource(source);
                _operationError = null;
                _path.Text = path;
                Title = $"{Path.GetFileName(path)} — Tailviewer prototype";
                RefreshDocument();
                return;
            }
            source ??= new DataSource(path) { Id = DataSourceId.CreateNew(), FollowTail = true };
            var document = await Task.Run(() => new LogDocument(source));
            if (_closed || request != _openRequest)
            {
                document.Dispose();
                return;
            }
            SaveFollowState();
            _rows.ItemsSource = null;
            _document?.Dispose();
            _document = document;
            if (source.ParentId == DataSourceId.Empty && !_sources.Contains(source)) _sources.Add(source);
            SelectSource(source);
            _operationError = null;
            _rows.ItemsSource = document.Lines;
            _follow.IsChecked = source.FollowTail;
            _path.Text = document.FileName;
            Title = $"{Path.GetFileName(document.FileName)} — Tailviewer prototype";
            RefreshDocument();
        }
        catch (Exception ex)
        {
            if (!_closed && request == _openRequest)
                _status.Text = _operationError = $"Cannot open file: {ex.Message}";
        }
        finally
        {
            if (request == _openRequest)
            {
                _open.IsEnabled = !_closed;
                UpdateCloseCommands();
            }
        }
    }

    private void SelectSource(DataSource? source)
    {
        _changingSourceSelection = true;
        try { _sourceTree.SelectedItem = source; }
        finally { _changingSourceSelection = false; }
        UpdateCloseCommands();
    }

    private void UpdateCloseCommands()
    {
        _closeSource.IsEnabled = !_closed && _sourceTree.SelectedItem is DataSource selected && _workspace.Contains(selected.Id);
        _closeAll.IsEnabled = !_closed && (_sources.Count > 0 || !_open.IsEnabled);
        ToolTip.SetTip(_closeSource, _sourceTree.SelectedItem is DataSource child && !_workspace.Contains(child.Id)
            ? "Close the parent folder to stop watching this file." : "Remove this source from the workspace.");
    }

    private void ClearDocument()
    {
        _rows.ItemsSource = null;
        _document?.Dispose();
        _document = null;
        _copy.IsEnabled = false;
        _operationError = null;
        _path.Text = "Open a log file to begin.";
        _status.Text = "Select lines with Shift/Ctrl • Ctrl/Cmd+C to copy";
        Title = "Tailviewer — Avalonia prototype";
    }

    private async Task CloseCurrentSourceAsync()
    {
        if (_closed || _sourceTree.SelectedItem is not DataSource source || !_workspace.Contains(source.Id)) return;
        ++_openRequest;
        _open.IsEnabled = true;
        SaveFollowState();
        ClearDocument();
        SelectSource(null);
        if (_folders.Remove(source.Id, out var folder)) folder.Dispose();
        _folderChildren.Remove(source.Id);
        _workspace.Remove(source.Id);
        UpdateCloseCommands();
        if (_sources.Count > 0)
        {
            var next = _sources[0];
            SelectSource(next);
            await ActivateSourceAsync(next);
        }
    }

    private void CloseAllSources()
    {
        if (_closed) return;
        ++_openRequest;
        _open.IsEnabled = true;
        ClearDocument();
        SelectSource(null);
        foreach (var folder in _folders.Values) folder.Dispose();
        _folders.Clear();
        _folderChildren.Clear();
        _workspace.Clear();
        UpdateCloseCommands();
    }

    private void RefreshDocument()
    {
        if (_document == null)
        {
            if (_operationError != null)
            {
                _status.Text = _operationError;
                return;
            }
            if (_sourceTree.SelectedItem is DataSource selected && _folders.TryGetValue(selected.Id, out var folder))
                _status.Text = folder.Error ?? (_folderChildren[selected.Id].Count == 0 ? "No matching files." : "Select a file in this folder.");
            else if (_sourceTree.SelectedItem is DataSource group && SourcesViewModel.IsGroup(group))
                _status.Text = _workspace.GetChildren(group.Id).Any() ? "Select a source in this group." : "No sources in this group.";
            return;
        }
        if (_document.Refresh())
            ScrollToTail();
        _status.Text = _operationError ?? _document.Error ?? $"{_document.Lines.Count:N0} lines • {(_follow.IsChecked == true ? "Following tail" : "Paused — enable Follow tail to jump to the end")}";
    }

    private void SaveFollowState()
    {
        if (_document == null) return;
        var previous = _workspace.All.Concat(_folderChildren.Values.SelectMany(children => children))
            .FirstOrDefault(source => source.Id == _document.SourceId);
        if (previous != null) previous.FollowTail = _follow.IsChecked == true;
    }

    private void RefreshFolder(DataSourceId id)
    {
        if (_closed || !_folders.TryGetValue(id, out var folder) || !_folderChildren.TryGetValue(id, out var children)) return;
        var desired = folder.Sources;
        var ids = desired.Select(source => source.Id).ToHashSet();
        foreach (var removed in children.Where(source => !ids.Contains(source.Id)).ToArray()) children.Remove(removed);
        var existing = children.ToDictionary(source => source.Id);
        for (var index = 0; index < desired.Count; ++index)
        {
            if (index < children.Count && children[index].Id == desired[index].Id) continue;
            if (existing.TryGetValue(desired[index].Id, out var item)) children.Remove(item);
            else item = desired[index];
            children.Insert(index, item);
        }
    }

    private void ScrollToTail()
    {
        if (_follow.IsChecked == true && _document?.Lines.Count > 0)
            _rows.ScrollIntoView(_document.Lines.Count - 1);
    }

    private async Task CopyAsync()
    {
        var document = _document;
        var indices = _rows.Selection.SelectedIndexes.ToArray();
        if (document == null || indices.Length == 0)
            return;
        try
        {
            var text = await document.CopyTextAsync(indices);
            if (document != _document || _closed)
                return;
            if (Clipboard == null)
                throw new NotSupportedException("Clipboard is unavailable.");
            await Clipboard.SetTextAsync(text);
            await Clipboard.FlushAsync();
            _operationError = null;
        }
        catch (Exception ex)
        {
            if (document == _document && !_closed)
                _status.Text = _operationError = $"Cannot copy: {ex.Message}";
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && e.Key is Key.O or Key.C)
        {
            e.Handled = true;
            if (e.Key == Key.O && _open.IsEnabled)
                await PickFileAsync();
            else if (e.Key == Key.C)
                await CopyAsync();
        }
        else if (_rows.IsKeyboardFocusWithin && e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
        {
            _follow.IsChecked = false;
        }
    }
}
