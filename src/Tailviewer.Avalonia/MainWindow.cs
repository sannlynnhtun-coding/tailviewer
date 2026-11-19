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
using Tailviewer.Avalonia.Views;

namespace Tailviewer.Avalonia;

public sealed class MainWindow : Window
{
    private readonly Button _open = new() { Content = "_Open file…" };
    private readonly Button _openFolder = new() { Content = "Open _folder…" };
    private readonly Button _copy = new() { Content = "_Copy selected", IsEnabled = false };
    private readonly Button _closeSource = new() { Content = "C_lose", IsEnabled = false };
    private readonly Button _closeAll = new() { Content = "Close _all", IsEnabled = false };
    private readonly Button _newGroup = new() { Content = "New _group" };
    private readonly CheckBox _follow = new() { Content = "Follow _tail", IsChecked = true };
    private readonly TextBlock _path = new() { Text = "Open a log file to begin.", TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = new() { Text = "Select lines with Shift/Ctrl • Ctrl/Cmd+C to copy" };
    private readonly ListBox _rows = new() { SelectionMode = SelectionMode.Multiple, Focusable = true };
    private readonly LogTextSelection _textSelection;
    private readonly NavigationView _navigation = new();
    private readonly EncodingView _encoding;
    private readonly QuickNavigationView _quickNavigation;
    private readonly SourcesViewModel _workspace = new();
    private AvaloniaList<DataSource> _sources => _workspace.Roots;
    private readonly TreeView _sourceTree = new();
    private readonly SourceTreeDrag _sourceDrag;
    private readonly Dictionary<DataSourceId, FolderSources> _folders = new();
    private readonly Dictionary<DataSourceId, AvaloniaList<DataSource>> _folderChildren = new();
    private readonly DefaultTaskScheduler _folderScheduler = new();
    private readonly Lazy<IFilesystem> _folderFilesystem;
    private readonly FileSourceStates _fileSourceStates;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private LogDocument? _document;
    private readonly SourceDocuments _readerOwner = new((source, encoding) => new LogDocument(source, encoding));
    private readonly Dictionary<DataSourceId, SourceDocuments.Lease> _documents = new();
    private SourceDocuments.Lease? _activeLease;
    private GroupDocument? _activeGroup;
    private long _lastGroupRefresh;
    private long _lastReaderRefresh;
    // shortcut: viewport offsets are session-only until profile save/restore in T055.
    private readonly Dictionary<DataSourceId, (LogDocument Document, long Revision, Vector Offset)> _viewStates = new();
    private (LogDocument? Document, long Request) _pendingViewRestore;
    private string? _operationError;
    private bool _closed;
    private bool _changingSourceSelection;
    private long _openRequest;
    private readonly Dictionary<DataSourceId, string?> _sourceStates = new();
    private DataSourceId _openingSource;
    private readonly Queue<string[]> _handoffs = new();
    private bool _handlingHandoffs;
    private Task _handoffProcessing = Task.CompletedTask;

    public MainWindow(string? fileName = null, ILogFileSettings? logFileSettings = null)
    {
        // shortcut: default settings stay in memory until profile startup/save wiring in T055.
        _encoding = new EncodingView(logFileSettings ?? new LogFileSettings());
        _encoding.Changed += async () =>
        {
            if (_closed || !_open.IsEnabled || _document == null) return;
            var source = _workspace.All.Concat(_folderChildren.Values.SelectMany(children => children))
                .FirstOrDefault(item => item.Id == _document.SourceId);
            if (source != null) await ActivateSourceAsync(source);
        };
        _sourceDrag = new SourceTreeDrag(_sourceTree, _workspace, async (source, target, after) =>
        {
            if (after.HasValue) await MoveSourceAsync(source, target.Id, after.Value);
            else await GroupSourcesAsync(source, target.Id);
        });
        _textSelection = new LogTextSelection(_rows, () =>
        {
            _copy.IsEnabled = _rows.Selection.SelectedIndexes.Count > 0 || _textSelection!.CanCopy;
            _follow.IsChecked = false;
        });
        _textSelection.Failed += message => _status.Text = _operationError = message;
        ToolTip.SetTip(_rows, "Alt+drag or Alt+Shift+arrows to select text; Shift/Ctrl+click to select whole lines; Escape clears text selection.");
        _folderFilesystem = new Lazy<IFilesystem>(() => new Filesystem(_folderScheduler));
        _fileSourceStates = new FileSourceStates(_folderFilesystem.Value, _folderScheduler);
        _quickNavigation = new QuickNavigationView(_workspace);
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
        AutomationProperties.SetName(_openFolder, "Open folder");
        _openFolder.Bind(IsEnabledProperty, _open.GetObservable(IsEnabledProperty));
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
            void RefreshHeading()
            {
                var updated = source.File ?? source.LogFileFolderPath ?? source.DisplayName ?? "Merged Data Source";
                var heading = SourcesViewModel.IsGroup(source) ? updated : Path.GetFileName(Path.TrimEndingDirectorySeparator(updated));
                label.Text = string.IsNullOrEmpty(heading) ? updated : heading;
                ToolTip.SetTip(label, updated);
                AutomationProperties.SetName(label, updated);
                if (label.FindAncestorOfType<TreeViewItem>() is { } item) AutomationProperties.SetName(item, updated);
            }
            var content = new Grid { Children = { label } };
            Action? beginRename = null;
            if (SourcesViewModel.IsGroup(source))
            {
                var editor = new TextBox { IsVisible = false };
                AutomationProperties.SetName(editor, "Group name");
                content.Children.Add(editor);
                void FinishRename(bool commit)
                {
                    if (!editor.IsVisible) return;
                    if (commit && !_closed)
                    {
                        try
                        {
                            _workspace.RenameGroup(source.Id, editor.Text ?? "");
                            RefreshHeading();
                            if ((_sourceTree.SelectedItem as DataSource)?.Id == source.Id)
                            {
                                _path.Text = source.DisplayName;
                                Title = $"{source.DisplayName} — Tailviewer prototype";
                            }
                        }
                        catch (ArgumentException error) { _status.Text = _operationError = "Cannot rename group: " + error.Message; }
                    }
                    editor.IsVisible = false;
                    label.IsVisible = true;
                }
                beginRename = () =>
                {
                    editor.Text = source.DisplayName;
                    label.IsVisible = false;
                    editor.IsVisible = true;
                    Dispatcher.UIThread.Post(() => { if (editor.IsVisible) { editor.Focus(); editor.SelectAll(); } }, DispatcherPriority.Loaded);
                };
                editor.KeyDown += (_, args) =>
                {
                    if (args.Key is not (Key.Enter or Key.Escape)) return;
                    args.Handled = true;
                    FinishRename(args.Key == Key.Enter);
                };
                editor.LostFocus += (_, _) => FinishRename(false);
            }
            label.ContextMenu = CreateSourceMenu(source, beginRename, RefreshHeading);
            label.AttachedToVisualTree += (_, _) =>
            {
                var item = label.FindAncestorOfType<TreeViewItem>();
                if (item != null)
                {
                    item.ContextMenu = label.ContextMenu;
                    AutomationProperties.SetName(item, source.File ?? source.LogFileFolderPath ?? source.DisplayName ?? "Merged Data Source");
                }
            };
            return new SourcesView(source, content);
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
            return new SelectableTextBlock
            {
                IsHitTestVisible = false,
                Focusable = false,
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
            Children = { brandImage, _open, _openFolder, _copy, _follow }
        };
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"),
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
        var navigationPanel = new StackPanel { Spacing = 8, Children = { _encoding, _navigation, _quickNavigation } };
        Grid.SetRow(navigationPanel, 2);
        Grid.SetColumnSpan(navigationPanel, 2);
        Grid.SetRow(sourcePanel, 3);
        Grid.SetRow(_rows, 3);
        Grid.SetColumn(_rows, 1);
        Grid.SetRow(_status, 4);
        Grid.SetColumnSpan(_status, 2);
        layout.Children.Add(_path);
        layout.Children.Add(navigationPanel);
        layout.Children.Add(sourcePanel);
        layout.Children.Add(_rows);
        layout.Children.Add(_status);
        Content = layout;

        _open.Click += async (_, _) => await PickSourcesAsync(folders: false);
        _openFolder.Click += async (_, _) => await PickSourcesAsync(folders: true);
        _copy.Click += async (_, _) => await CopyAsync();
        _navigation.LineChosen += index =>
        {
            if (_document == null || !ReferenceEquals(_document, _navigation.Document)) return;
            SelectAndRevealLine(index);
        };
        _navigation.Cancelled += () =>
        {
            if (_document != null) _rows.Focus();
            else _sourceTree.Focus();
        };
        _quickNavigation.SourceChosen += async source =>
        {
            if (_closed) return;
            if (_document?.SourceId == source.Id && _sourceTree.SelectedItem is DataSource current && current.Id == source.Id)
            {
                ++_openRequest;
                _openingSource = DataSourceId.Empty;
                _open.IsEnabled = true;
                UpdateCloseCommands();
            }
            else await ActivateSourceAsync(source);
            if (!_closed && _sourceTree.SelectedItem is DataSource active && active.Id == source.Id)
            {
                if (_document != null) _rows.Focus();
                else _sourceTree.Focus();
            }
        };
        _quickNavigation.Cancelled += () =>
        {
            if (_document != null) _rows.Focus();
            else _sourceTree.Focus();
        };
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
            _textSelection.Clear();
            _copy.IsEnabled = _rows.Selection.SelectedIndexes.Count > 0;
            if (_copy.IsEnabled)
                _follow.IsChecked = false;
        };
        _rows.AddHandler(PointerWheelChangedEvent, (_, _) => _follow.IsChecked = false, RoutingStrategies.Tunnel);
        _rows.AddHandler(PointerPressedEvent, (_, _) => _follow.IsChecked = false, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        _timer.Tick += (_, _) => { RefreshDocument(); RefreshSourceStates(); _sourceDrag.Refresh(); };
        Closed += (_, _) =>
        {
            _closed = true;
            _sourceDrag.Clear();
            _handoffs.Clear();
            ++_openRequest;
            _openingSource = DataSourceId.Empty;
            _timer.Stop();
            _activeGroup?.Dispose();
            _activeGroup = null;
            foreach (var entry in _documents.Values) entry.Dispose();
            _documents.Clear();
            _activeLease?.Dispose();
            _activeLease = null;
            _readerOwner.Dispose();
            _viewStates.Clear();
            _pendingViewRestore = default;
            _navigation.BindDocument(null);
            _textSelection.BindDocument(null);
            _quickNavigation.Dismiss();
            foreach (var folder in _folders.Values) folder.Dispose();
            _fileSourceStates.Dispose();
            _folderScheduler.Dispose();
            logo.Dispose();
        };
        _timer.Start();
        if (fileName != null)
        {
            async void OpenInitialFile(object? sender, EventArgs args)
            {
                Opened -= OpenInitialFile;
                await OpenFileAsync(fileName);
            }
            Opened += OpenInitialFile;
        }
    }

    private async Task PickSourcesAsync(bool folders)
    {
        try
        {
            if (!(folders ? StorageProvider.CanPickFolder : StorageProvider.CanOpen))
                throw new NotSupportedException(folders
                    ? "Folder picking is unavailable. Pass a local directory path on the command line."
                    : "File picking is unavailable. Pass a local file path on the command line.");
            IReadOnlyList<IStorageItem> items = folders
                ? await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Open log folder", AllowMultiple = true })
                : await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Open log file", AllowMultiple = true });
            if (items.Count == 0)
                return;
            var paths = items.Select(item => item.TryGetLocalPath() ??
                throw new NotSupportedException("Only local files and folders can be opened.")).ToArray();
            await OpenFilesAsync(paths);
        }
        catch (Exception ex)
        {
            _status.Text = _operationError = ex.Message;
        }
    }

    public Task OpenFileAsync(string fileName) => OpenSourceAsync(fileName, null);

    internal Task AcceptHandoff(string[] paths, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_closed) throw new OperationCanceledException("The Tailviewer window is closing.");
        if (_handoffs.Count >= 16) throw new IOException("Tailviewer is busy. Retry after the pending source requests finish.");
        _handoffs.Enqueue(paths);
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        if (!_handlingHandoffs) _handoffProcessing = ProcessHandoffsAsync();
        return _handoffProcessing;
    }

    private async Task ProcessHandoffsAsync()
    {
        _handlingHandoffs = true;
        try
        {
            while (!_closed && _handoffs.TryDequeue(out var paths)) await OpenFilesAsync(paths);
        }
        catch (Exception error) { ReportHandoffError(error.Message); }
        finally { _handlingHandoffs = false; }
    }

    internal void ReportHandoffError(string message)
    {
        if (!_closed) _status.Text = _operationError = "Cannot receive source requests: " + message;
    }

    private void OnDragOver(object? sender, DragEventArgs args)
    {
        var allowed = args.DragEffects;
        args.DragEffects = DragDropEffects.None;
        args.Handled = true;
        if (_closed) return;
        try
        {
            if (args.DataTransfer.Formats.Contains(DataFormat.File))
                args.DragEffects = (allowed & DragDropEffects.Link) != 0 ? DragDropEffects.Link : allowed & DragDropEffects.Copy;
        }
        catch (Exception)
        {
            // Malformed format providers remain rejected without reporting an error on every pointer move.
        }
    }

    private async void OnDrop(object? sender, DragEventArgs args)
    {
        OnDragOver(sender, args);
        if (args.DragEffects == DragDropEffects.None) return;
        try
        {
            var paths = args.DataTransfer.TryGetFiles()?.Select(item => item.TryGetLocalPath() ??
                throw new NotSupportedException("Only local files and folders can be opened.")).ToArray();
            if (paths == null || paths.Length == 0) { args.DragEffects = DragDropEffects.None; return; }
            await OpenFilesAsync(paths);
        }
        catch (Exception error)
        {
            args.DragEffects = DragDropEffects.None;
            if (!_closed) _status.Text = _operationError = "Cannot open dropped items: " + error.Message;
        }
    }

    public async Task OpenFilesAsync(IEnumerable<string> fileNames)
    {
        var errors = new List<string>();
        foreach (var fileName in fileNames.ToArray())
        {
            if (_closed) return;
            var request = _openRequest + 1;
            await OpenFileAsync(fileName);
            if (_closed || request != _openRequest) return;
            if (_operationError != null) errors.Add(_operationError);
        }
        if (errors.Count > 0) _status.Text = _operationError = string.Join(Environment.NewLine, errors);
    }

    private async Task ActivateSourceAsync(DataSource source)
    {
        if (_closed) return;
        if (!SourcesViewModel.IsGroup(source))
        {
            await OpenSourceAsync(source.File ?? source.LogFileFolderPath, source);
            return;
        }
        await OpenGroupAsync(source, ++_openRequest);
    }

    private async Task OpenGroupAsync(DataSource source, long request)
    {
        _openingSource = source.Id;
        _lastGroupRefresh = Environment.TickCount64;
        GroupDocument? pending = null;
        try
        {
            if (!ConfigureReaders()) return;
            pending = await GroupDocument.OpenAsync(source, _readerOwner, GroupChildren);
            if (_closed || request != _openRequest) return;
            SaveViewState();
            ClearDocument();
            _activeGroup = pending;
            pending = null;
            _document = _activeGroup.Document;
            _navigation.BindDocument(_document);
            _textSelection.BindDocument(_document);
            SelectSource(source);
            _document.Refresh();
            _rows.ItemsSource = _document.Lines;
            _follow.IsChecked = source.FollowTail;
            _path.Text = source.LogFileFolderPath ?? source.DisplayName;
            var name = source.LogFileFolderPath != null ? Path.GetFileName(Path.TrimEndingDirectorySeparator(source.LogFileFolderPath)) : source.DisplayName;
            Title = $"{name} — Tailviewer prototype";
            RefreshDocument();
        }
        catch (Exception error)
        {
            if (!_closed && request == _openRequest) _status.Text = _operationError = "Cannot open group: " + error.Message;
        }
        finally
        {
            pending?.Dispose();
            if (request == _openRequest)
            {
                _openingSource = DataSourceId.Empty;
                _open.IsEnabled = true;
                UpdateCloseCommands();
            }
        }
    }

    private IReadOnlyList<DataSource> GroupChildren(DataSource source) =>
        source.LogFileFolderPath != null ? _folderChildren.GetValueOrDefault(source.Id)?.ToArray() ?? Array.Empty<DataSource>() :
        _workspace.GetChildren(source.Id).ToArray();

    private ContextMenu CreateSourceMenu(DataSource source, Action? beginRename, Action refreshHeading)
    {
        var menu = new ContextMenu();
        void AddExclusion(DataSource parent)
        {
            var folderMember = parent.LogFileFolderPath != null;
            var excluded = parent.ExcludedDataSources.Contains(source.Id);
            var containerName = folderMember ? "folder" : "group";
            var toggle = new MenuItem { Header = excluded ? "Include in " + containerName : "Exclude from " + containerName };
            toggle.Click += (_, _) =>
            {
                if (_closed || source.ParentId != parent.Id || !_workspace.Contains(parent.Id)) return;
                var currentMember = folderMember
                    ? _folderChildren.TryGetValue(parent.Id, out var children) && children.Contains(source)
                    : _workspace.GetChildren(parent.Id).Contains(source);
                if (!currentMember) return;
                try
                {
                    if (!folderMember) _workspace.SetExcluded(source.Id, !excluded);
                    else if (!excluded) parent.ExcludedDataSources.Add(source.Id);
                    else parent.ExcludedDataSources.Remove(source.Id);
                }
                catch (ArgumentException error) { _status.Text = _operationError = "Cannot change source inclusion: " + error.Message; }
            };
            menu.Items.Add(toggle);
        }
        void RefreshMenu()
        {
            menu.Items.Clear();
            if (!_workspace.Contains(source.Id))
            {
                var folder = _workspace.All.FirstOrDefault(parent => parent.Id == source.ParentId && parent.LogFileFolderPath != null);
                if (folder != null && _folderChildren.TryGetValue(folder.Id, out var children) && children.Contains(source)) AddExclusion(folder);
                return;
            }
            var siblings = (source.ParentId == DataSourceId.Empty ? _sources : _workspace.GetChildren(source.ParentId)).ToArray();
            var index = Array.FindIndex(siblings, item => item.Id == source.Id);
            foreach (var offset in new[] { -1, 1 })
            {
                var next = index + offset;
                var move = new MenuItem { Header = offset < 0 ? "Move up" : "Move down", IsEnabled = next >= 0 && next < siblings.Length };
                if (move.IsEnabled)
                    move.Click += async (_, _) => await MoveSourceAsync(source, siblings[next].Id, offset > 0);
                menu.Items.Add(move);
            }
            if (beginRename != null)
            {
                var rename = new MenuItem { Header = "Rename group" };
                rename.Click += (_, _) => beginRename();
                menu.Items.Add(rename);
            }
            if (source.LogFileFolderPath != null)
            {
                var options = new MenuItem { Header = "Folder options…" };
                options.Click += async (_, _) => await EditFolderAsync(source, refreshHeading);
                menu.Items.Add(options);
            }
            if (SourcesViewModel.IsGroup(source) || source.LogFileFolderPath != null)
            {
                foreach (var excluded in new[] { false, true })
                {
                    var all = new MenuItem { Header = excluded ? "Exclude all" : "Include all" };
                    all.Click += (_, _) =>
                    {
                        if (_closed || !_workspace.Contains(source.Id)) return;
                        IEnumerable<DataSource> children = _folderChildren.TryGetValue(source.Id, out var files)
                            ? files : _workspace.GetChildren(source.Id);
                        var members = children.Where(member => !SourcesViewModel.IsGroup(member) && member.LogFileFolderPath == null)
                            .Select(member => member.Id);
                        if (excluded) source.ExcludedDataSources.UnionWith(members);
                        else source.ExcludedDataSources.ExceptWith(members);
                    };
                    menu.Items.Add(all);
                }
            }
            var add = new MenuItem { Header = "Add to group" };
            foreach (var group in _workspace.All.Where(SourcesViewModel.IsGroup))
            {
                var target = new MenuItem { Header = group.DisplayName, Tag = group.Id };
                target.Click += async (_, _) => await ChangeGroupAsync(source, () => _workspace.AddToGroup(source.Id, group.Id));
                add.Items.Add(target);
            }
            add.IsEnabled &= add.Items.Count > 0;
            menu.Items.Add(add);
            if (!SourcesViewModel.IsGroup(source) && source.LogFileFolderPath == null)
            {
                var pair = new MenuItem { Header = "Group with" };
                foreach (var targetSource in _sources.Where(item => item.Id != source.Id && !SourcesViewModel.IsGroup(item) && item.LogFileFolderPath == null))
                {
                    var target = new MenuItem { Header = targetSource.File ?? targetSource.DisplayName ?? "Custom source", Tag = targetSource.Id };
                    target.Click += async (_, _) => await GroupSourcesAsync(source, targetSource.Id);
                    pair.Items.Add(target);
                }
                pair.IsEnabled = pair.Items.Count > 0;
                menu.Items.Add(pair);
            }
            if (source.ParentId != DataSourceId.Empty)
            {
                var parent = _workspace.All.Single(item => item.Id == source.ParentId);
                AddExclusion(parent);
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

    private async Task EditFolderAsync(DataSource source, Action refreshHeading)
    {
        if (_closed || !_workspace.Contains(source.Id) || !_folders.TryGetValue(source.Id, out var original)) return;
        var path = new TextBox { Text = source.LogFileFolderPath };
        var browse = new Button { Content = "Browse…", Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(browse, Dock.Right);
        var pattern = new TextBox { Text = source.LogFileSearchPattern };
        var recursive = new CheckBox { Content = "Include subfolders", IsChecked = source.Recursive };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var apply = new Button { Content = "Apply", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        AutomationProperties.SetName(path, "Folder path");
        AutomationProperties.SetName(browse, "Choose monitored folder");
        AutomationProperties.SetName(pattern, "Folder file patterns");
        AutomationProperties.SetName(status, "Folder options status");
        var dialog = new Window
        {
            Title = "Folder options", Width = 620, Height = 340, MinWidth = 420, MinHeight = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
            Content = new StackPanel
            {
                Margin = new Thickness(16), Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Folder path" }, new DockPanel { Children = { browse, path } },
                    new TextBlock { Text = "File patterns (for example *.log;*.txt)" }, pattern, recursive, status,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { apply, cancel } }
                }
            }
        };
        browse.Click += async (_, _) =>
        {
            if (!browse.IsEnabled) return;
            browse.IsEnabled = false;
            try
            {
                var provider = dialog.StorageProvider;
                if (!provider.CanPickFolder) throw new NotSupportedException("Folder picking is unavailable. Enter the folder path directly.");
                using var start = Uri.TryCreate(path.Text, UriKind.Absolute, out var uri) && uri.IsFile
                    ? await provider.TryGetFolderFromPathAsync(uri) : null;
                if (_closed || !dialog.IsVisible) return;
                var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Choose folder to monitor", AllowMultiple = false, SuggestedStartLocation = start
                });
                try
                {
                    if (_closed || !dialog.IsVisible || folders.Count == 0) return;
                    path.Text = folders[0].TryGetLocalPath() ?? throw new NotSupportedException("Only local folders can be monitored.");
                    status.Text = "";
                }
                finally { foreach (var folder in folders) folder.Dispose(); }
            }
            catch (Exception error) { if (dialog.IsVisible) status.Text = "Cannot choose folder: " + error.Message; }
            finally { browse.IsEnabled = true; }
        };
        cancel.Click += (_, _) => dialog.Close();
        apply.Click += async (_, _) =>
        {
            if (!apply.IsEnabled) return;
            apply.IsEnabled = false;
            FolderSources? staged = null;
            try
            {
                var candidate = source.Clone();
                candidate.LogFileFolderPath = Path.GetFullPath(path.Text ?? "");
                candidate.LogFileSearchPattern = pattern.Text ?? "";
                candidate.Recursive = recursive.IsChecked == true;
                staged = await Task.Run(() => new FolderSources(_folderFilesystem.Value, _folderScheduler, candidate));
                if (_closed || !dialog.IsVisible || !_workspace.Contains(source.Id) ||
                    !_folders.TryGetValue(source.Id, out var current) || !ReferenceEquals(current, original)) return;
                source.LogFileFolderPath = candidate.LogFileFolderPath;
                source.LogFileSearchPattern = candidate.LogFileSearchPattern;
                source.Recursive = candidate.Recursive;
                _folders[source.Id] = staged;
                staged.Changed += () => Dispatcher.UIThread.Post(() => RefreshFolder(source.Id));
                staged = null;
                original.Dispose();
                RefreshFolder(source.Id);
                refreshHeading();
                if (_sourceTree.SelectedItem is DataSource selected && selected.Id == source.Id)
                {
                    _path.Text = source.LogFileFolderPath;
                    Title = $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(source.LogFileFolderPath))} — Tailviewer prototype";
                }
                RefreshSourceStates();
                RefreshDocument();
                dialog.Close();
            }
            catch (Exception error) { if (dialog.IsVisible) status.Text = "Cannot update folder: " + error.Message; }
            finally
            {
                staged?.Dispose();
                apply.IsEnabled = true;
            }
        };
        await dialog.ShowDialog(this);
    }

    private async Task GroupSourcesAsync(DataSource source, DataSourceId target)
    {
        if (_closed) return;
        try { await ActivateSourceAsync(_workspace.GroupWith(source.Id, target)); }
        catch (Exception error) when (error is XmlException || error is ArgumentException)
        { _status.Text = _operationError = "Cannot group sources: " + error.Message; }
    }

    private async Task MoveSourceAsync(DataSource source, DataSourceId target, bool after)
    {
        if (_closed) return;
        try
        {
            _workspace.MoveRelative(source.Id, target, after);
            if (_document?.SourceId == source.Id || (_sourceTree.SelectedItem as DataSource)?.Id == source.Id) SelectSource(source);
            else await ActivateSourceAsync(source);
        }
        catch (Exception error) when (error is XmlException || error is ArgumentException)
        {
            _status.Text = _operationError = "Cannot reorder source: " + error.Message;
        }
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
        SourceDocuments.Lease? pendingLease = null;
        _openingSource = selected?.Id ?? DataSourceId.Empty;
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
                    var folder = await Task.Run(() => new FolderSources(_folderFilesystem.Value, _folderScheduler, source));
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
                if (source.ParentId == DataSourceId.Empty && !_sources.Contains(source)) _sources.Add(source);
                await OpenGroupAsync(source, request);
                return;
            }
            source ??= new DataSource(path) { Id = DataSourceId.CreateNew(), FollowTail = true };
            // Retain configured identity even when the first read cannot open the file.
            if (source.ParentId == DataSourceId.Empty && !_sources.Contains(source)) _sources.Add(source);
            selected = source;
            _openingSource = source.Id;
            if (!ConfigureReaders()) return;
            try { pendingLease = await _readerOwner.AcquireAsync(source.Id); }
            catch (OperationCanceledException) when (!_closed && request == _openRequest)
            {
                await OpenSourceAsync(fileName, source);
                return;
            }
            var document = pendingLease.Document;
            if (_closed || request != _openRequest)
            {
                return;
            }
            if (!Equals(pendingLease.Encoding, _encoding.Settings.DefaultEncoding))
            {
                pendingLease.Dispose();
                pendingLease = null;
                await OpenSourceAsync(fileName, source);
                return;
            }
            SaveViewState();
            _rows.ItemsSource = null;
            var lease = TrackDocument(source, pendingLease);
            pendingLease = null;
            ReleaseActiveLease();
            _activeLease = lease;
            _document = document;
            _navigation.BindDocument(document);
            _textSelection.BindDocument(document);
            _quickNavigation.Dismiss();
            if (source.ParentId == DataSourceId.Empty && !_sources.Contains(source)) _sources.Add(source);
            SelectSource(source);
            _operationError = null;
            document.Refresh();
            _rows.ItemsSource = document.Lines;
            var restore = _viewStates.TryGetValue(source.Id, out var view) &&
                ReferenceEquals(view.Document, document) && view.Revision == document.Revision;
            if (restore)
            {
                _rows.Selection.BeginBatchUpdate();
                try
                {
                    foreach (var index in source.SelectedLogLines.OrderBy(index => (int)index))
                        if (index >= 0 && index < document.Lines.Count) _rows.Selection.Select((int)index);
                }
                finally { _rows.Selection.EndBatchUpdate(); }
            }
            _follow.IsChecked = source.FollowTail;
            _path.Text = document.FileName;
            Title = $"{Path.GetFileName(document.FileName)} — Tailviewer prototype";
            RefreshDocument();
            if (restore)
            {
                // Run after the control's default-priority selected-item scroll, which would overwrite this offset.
                _pendingViewRestore = (document, request);
                Dispatcher.UIThread.Post(() =>
                {
                    if (_pendingViewRestore.Request == request) _pendingViewRestore = default;
                    if (_closed || request != _openRequest || !ReferenceEquals(_document, document) ||
                        document.Revision != view.Revision) return;
                    _rows.UpdateLayout();
                    if (_rows.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } scroll)
                    {
                        scroll.Offset = new Vector(view.Offset.X, _follow.IsChecked == true
                            ? Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height) : view.Offset.Y);
                    }
                }, DispatcherPriority.Default);
            }
            else ScrollToTail();
            PruneDocuments();
        }
        catch (Exception ex)
        {
            if (!_closed && request == _openRequest)
            {
                _status.Text = _operationError = $"Cannot open file: {ex.Message}";
                if (selected != null) _sourceStates[selected.Id] = _operationError;
            }
        }
        finally
        {
            pendingLease?.Dispose();
            if (request == _openRequest)
            {
                _openingSource = DataSourceId.Empty;
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
        ReleaseActiveLease();
        _document = null;
        _pendingViewRestore = default;
        PruneDocuments();
        _navigation.BindDocument(null);
        _textSelection.BindDocument(null);
        _quickNavigation.Dismiss();
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
        _openingSource = DataSourceId.Empty;
        _open.IsEnabled = true;
        SaveViewState();
        ClearDocument();
        SelectSource(null);
        if (_folders.Remove(source.Id, out var folder)) folder.Dispose();
        if (_folderChildren.Remove(source.Id, out var children))
            foreach (var child in children) _sourceStates.Remove(child.Id);
        _workspace.Remove(source.Id);
        PruneDocuments();
        ConfigureReaders();
        _sourceStates.Remove(source.Id);
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
        _sourceDrag.Clear();
        _handoffs.Clear();
        ++_openRequest;
        _openingSource = DataSourceId.Empty;
        _open.IsEnabled = true;
        ClearDocument();
        SelectSource(null);
        foreach (var folder in _folders.Values) folder.Dispose();
        _folders.Clear();
        _folderChildren.Clear();
        _workspace.Clear();
        PruneDocuments();
        ConfigureReaders();
        _sourceStates.Clear();
        UpdateCloseCommands();
    }

    private void RefreshDocument()
    {
        _textSelection.Refresh();
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
        var folderStatus = _sourceTree.SelectedItem is DataSource current && _folders.TryGetValue(current.Id, out var activeFolder)
            ? activeFolder.Error ?? (_folderChildren[current.Id].Count == 0 ? "No matching files." : null) : null;
        _status.Text = _operationError ?? folderStatus ?? _activeGroup?.Error ?? _document.Error ?? $"{_document.Lines.Count:N0} lines • {(_follow.IsChecked == true ? "Following tail" : "Paused — enable Follow tail to jump to the end")} • Ctrl/Cmd+G: go to line";
    }

    private void RefreshSourceStates()
    {
        if (_closed) return;
        var valid = Environment.TickCount64 - _lastReaderRefresh >= 500 ? ConfigureReaders() : UpdateFileSourceTracking();
        var pendingReaders = new HashSet<DataSourceId>();
        if (valid)
            foreach (var source in _workspace.All.Concat(_folderChildren.Values.SelectMany(children => children)).Where(source => source.File != null))
            {
                if (_readerOwner.TryAcquire(source.Id, out var lease))
                {
                    var ready = TrackDocument(source, lease!);
                    if (source.Id == _document?.SourceId && !ReferenceEquals(_document, ready.Document) && _openingSource == DataSourceId.Empty)
                        _ = OpenSourceAsync(source.File, source);
                }
                else pendingReaders.Add(source.Id);
            }
        foreach (var (id, entry) in _documents)
        {
            if (ReferenceEquals(entry.Document, _document)) continue;
            entry.Document.Refresh();
            _sourceStates[id] = entry.Document.Error ?? (entry.Document.IsLoading ? "Loading…" :
                entry.Document.Lines.Count == 0 ? "No log lines." : null);
        }
        if (_document != null && _document.SourceId != _openingSource)
            _sourceStates[_document.SourceId] = _document.Error ?? (_document.IsLoading ? "Loading…" :
                _document.Lines.Count == 0 ? "No log lines." : null);
        foreach (var view in _sourceTree.GetVisualDescendants().OfType<SourcesView>())
        {
            var source = view.Source;
            var status = _sourceStates.GetValueOrDefault(source.Id);
            var readerError = source.File != null ? _readerOwner.GetError(source.Id) : null;
            if (readerError != null) status = "Cannot open file: " + readerError;
            else if (pendingReaders.Contains(source.Id)) status = "Loading…";
            if (source.Id == _openingSource) status = "Loading…";
            else if (_folders.TryGetValue(source.Id, out var folder))
                status = folder.Error ?? (_folderChildren[source.Id].Count == 0 ? "No matching files." : null);
            else if (SourcesViewModel.IsGroup(source))
                status = _workspace.GetChildren(source.Id).Any() ? null : "No sources in this group.";
            else if (source.File != null && source.Id != _document?.SourceId && _fileSourceStates.TryGetStatus(source.Id, out var observed) &&
                     (observed != null || !_documents.ContainsKey(source.Id)))
                status = observed;
            view.UpdateStatus(status);
            view.UpdateIndexedCount(!pendingReaders.Contains(source.Id) && readerError == null && _documents.TryGetValue(source.Id, out var indexed)
                ? indexed.Document.Lines.Count : null);
        }
        if (_open.IsEnabled && _sourceTree.SelectedItem is DataSource selected && (SourcesViewModel.IsGroup(selected) || selected.LogFileFolderPath != null) &&
            _openingSource == DataSourceId.Empty && Environment.TickCount64 - _lastGroupRefresh >= 500 &&
            (_activeGroup == null || !_activeGroup.IsCurrent(_readerOwner, GroupChildren)))
            _ = ActivateSourceAsync(selected);
    }

    private bool ConfigureReaders()
    {
        _lastReaderRefresh = Environment.TickCount64;
        if (_closed || !UpdateFileSourceTracking()) return false;
        try
        {
            _readerOwner.Configure(_workspace.All.Concat(_folderChildren.Values.SelectMany(children => children)), _encoding.Settings.DefaultEncoding);
            return true;
        }
        catch (Exception error)
        {
            _status.Text = _operationError = "Cannot prepare source readers: " + error.Message;
            return false;
        }
    }

    private SourceDocuments.Lease TrackDocument(DataSource source, SourceDocuments.Lease lease)
    {
        var document = lease.Document;
        if (_viewStates.TryGetValue(source.Id, out var view) && !ReferenceEquals(view.Document, document))
        {
            _viewStates.Remove(source.Id);
            source.SelectedLogLines.Clear();
        }
        if (_documents.TryGetValue(source.Id, out var previous))
        {
            if (ReferenceEquals(previous.Document, document))
            {
                if (!ReferenceEquals(previous, lease)) lease.Dispose();
                return previous;
            }
            if (!ReferenceEquals(previous, _activeLease)) previous.Dispose();
        }
        _documents[source.Id] = lease;
        document.Lines.CollectionChanged += (_, args) =>
        {
            if (args.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Reset or
                System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                _viewStates.Remove(source.Id);
                source.SelectedLogLines.Clear();
            }
        };
        return lease;
    }

    private void ReleaseActiveLease()
    {
        // shortcut: group views rebuild on activation; restore their viewport/selection snapshots in T031.
        if (_activeGroup != null)
        {
            _viewStates.Remove(_activeGroup.Document.SourceId);
            _activeGroup.Dispose();
        }
        _activeGroup = null;
        if (_activeLease != null && !_documents.Values.Contains(_activeLease)) _activeLease.Dispose();
        _activeLease = null;
    }

    private bool UpdateFileSourceTracking()
    {
        try
        {
            _fileSourceStates.SetSources(_workspace.All.Concat(_folderChildren.Values.SelectMany(children => children)));
            return true;
        }
        catch (XmlException error)
        {
            _status.Text = _operationError = "Cannot monitor source availability: " + error.Message;
            return false;
        }
    }

    private void SaveViewState()
    {
        if (_document == null) return;
        if (ReferenceEquals(_pendingViewRestore.Document, _document)) return;
        var previous = _workspace.All.Concat(_folderChildren.Values.SelectMany(children => children))
            .FirstOrDefault(source => source.Id == _document.SourceId);
        if (previous == null) return;
        _document.Refresh();
        previous.FollowTail = _follow.IsChecked == true;
        previous.SelectedLogLines = _rows.Selection.SelectedIndexes.Select(index => new LogLineIndex(index)).ToHashSet();
        var offset = _rows.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.Offset ?? default;
        previous.HorizontalOffset = offset.X;
        _viewStates[previous.Id] = (_document, _document.Revision, offset);
    }

    private void PruneDocuments()
    {
        var retained = _workspace.All.Concat(_folderChildren.Values.SelectMany(children => children))
            .Select(source => source.Id).ToHashSet();
        foreach (var (id, entry) in _documents.ToArray())
            if (id != _document?.SourceId && !retained.Contains(id))
            {
                _documents.Remove(id);
                _viewStates.Remove(id);
                if (!ReferenceEquals(entry, _activeLease)) entry.Dispose();
            }
    }

    private void RefreshFolder(DataSourceId id)
    {
        if (_closed || !_folders.TryGetValue(id, out var folder) || !_folderChildren.TryGetValue(id, out var children)) return;
        var desired = folder.Sources;
        var ids = desired.Select(source => source.Id).ToHashSet();
        foreach (var removed in children.Where(source => !ids.Contains(source.Id)).ToArray())
        {
            children.Remove(removed);
            _sourceStates.Remove(removed.Id);
        }
        var existing = children.ToDictionary(source => source.Id);
        for (var index = 0; index < desired.Count; ++index)
        {
            if (index < children.Count && children[index].Id == desired[index].Id) continue;
            if (existing.TryGetValue(desired[index].Id, out var item)) children.Remove(item);
            else item = desired[index];
            children.Insert(index, item);
        }
        PruneDocuments();
        ConfigureReaders();
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
        if (document == null || (indices.Length == 0 && !_textSelection.CanCopy))
            return;
        try
        {
            var text = await (_textSelection.CopyTextAsync() ?? document.CopyTextAsync(indices));
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

    private void SelectAndRevealLine(int index)
    {
        if (_document == null || index < 0 || index >= _document.Lines.Count) return;
        _follow.IsChecked = false;
        _textSelection.Clear();
        _rows.Selection.Clear();
        _rows.Selection.Select(index);
        _rows.ScrollIntoView(index);
        _operationError = null;
        _rows.Focus();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _sourceDrag.IsDragging)
        {
            _sourceDrag.Clear();
            e.Handled = true;
            return;
        }
        if (_rows.IsKeyboardFocusWithin && e.KeyModifiers == KeyModifiers.Control &&
            e.Key is Key.Home or Key.End && _document?.Lines.Count > 0)
        {
            e.Handled = true;
            SelectAndRevealLine(e.Key == Key.Home ? 0 : _document.Lines.Count - 1);
            return;
        }
        if (e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.Control or (KeyModifiers.Control | KeyModifiers.Shift))
        {
            e.Handled = true;
            if (_sourceTree.SelectedItem is DataSource current &&
                _workspace.GetAdjacentSource(current.Id, (e.KeyModifiers & KeyModifiers.Shift) != 0) is DataSource next && next.Id != current.Id)
            {
                await ActivateSourceAsync(next);
                if (!_closed && _sourceTree.SelectedItem is DataSource active && active.Id == next.Id)
                {
                    if (_document != null) _rows.Focus();
                    else _sourceTree.Focus();
                }
            }
            return;
        }
        if (e.Key == Key.C && e.Source is Visual origin && origin.GetSelfAndVisualAncestors().Any(control => control is TextBox))
            return;
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && e.Key is Key.O or Key.C or Key.G or Key.T)
        {
            e.Handled = true;
            if (e.Key == Key.O && _open.IsEnabled)
                await PickSourcesAsync(folders: false);
            else if (e.Key == Key.C)
                await CopyAsync();
            else if (e.Key == Key.G)
            {
                _quickNavigation.Dismiss();
                _navigation.BeginNavigation();
            }
            else if (e.Key == Key.T)
            {
                _navigation.IsVisible = false;
                _quickNavigation.BeginNavigation();
            }
        }
        else if (_rows.IsKeyboardFocusWithin && e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
        {
            _follow.IsChecked = false;
        }
    }
}
