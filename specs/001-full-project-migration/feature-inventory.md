# Legacy feature parity inventory

**Baseline**: source `5e351359`, 2026-11-19. These rows inventory capability families, not evidence of implementation. Exact command/options/edge semantics must be extracted from the cited code and mapped tests in P00 before the associated phase can pass. The complete project/package inventory is in [dependency-inventory.md](dependency-inventory.md).

| ID | Legacy capability | Source evidence (brace paths denote multiple files/directories) | Requirement | Phase | Current state |
|---|---|---|---|---|---|
| F01 | Startup, arguments, single-instance handoff, file/folder open, drag/drop, close/all | src/Tailviewer/{ArgumentParser.cs,SingleApplicationHelper.cs,Ui/Menu/FileMenuViewModel.cs} | FR-002 | P03 | partial: multi-file arguments, file/folder picker, native OLE drop and close-current/all verified; native handoff/restore, restart after graceful exit/crash and unresponsive-channel error verified; remaining legacy argument modes and foreground/off-screen behavior pending |
| F02 | Source tree, folders, groups, source naming, include/exclude, selection, missing/access states | src/Tailviewer/{BusinessLogic/DataSources,Ui/DataSourceTree,Ui/SidePanel/DataSources} | FR-002 | P03 | partial: file/folder/group tree, stable IDs, membership/cycle feedback, close commands, group rename and exclusion settings verified; empty/error tree states, active-file recovery, inactive configured-file availability, first-open missing-path retention/retry and cached background folder status verified; aggregate output, inactive parser/index lifecycle and remaining group behavior pending |
| F03 | Virtualized rows, horizontal/vertical scroll, text/line selection, original copy, go-to-line | src/Tailviewer/Ui/{LogView,GoToLine,QuickNavigation} | FR-003 | P04 | partial: virtualized line/range copy, Alt+drag character selection, Ctrl+G seek and Ctrl+T chooser; keyboard range extension/drag edge autoscroll implemented; native/performance/provenance acceptance pending |
| F04 | Append, pause/follow, file rewrite, rotation, disappearance, encoding changes | src/Tailviewer.Core/Sources/Text/Streaming | FR-003 | P04 | partial: reset-generation read rejection and truncate/regrow verified; BOM encoding replacement/random access verified; full-content audits detect stable hidden middle rewrites; concurrent rewrite consistency/performance and encoding-selector UI pending |
| F05 | Search forward/back, find all, pattern options, results and navigation | src/Tailviewer/{BusinessLogic/Searches,Ui/LogView} | FR-004 | P05 | not exposed |
| F06 | Quick/text/expression/level/time filters, include/exclude combinations | src/Tailviewer/{BusinessLogic/Filters,Ui/QuickFilter,Ui/SidePanel/QuickFilters}; src/Tailviewer.Core/Filters | FR-004 | P05 | Core pieces exist; UI absent |
| F07 | Highlight rules, ordering/colors and persistence | src/Tailviewer/{BusinessLogic/Highlighters,Ui/SidePanel/Highlighters} | FR-004 | P05 | not ported |
| F08 | Merged sources, timestamp offsets, provenance, ties and missing timestamps | src/Tailviewer/BusinessLogic/DataSources/MergedDataSource.cs; src/Tailviewer.Core/Sources | FR-005 | P06 | Core pieces exist; UI absent |
| F09 | Format detection, custom formats, parser pipeline, multiline, timestamp/level, columns | src/Tailviewer/{BusinessLogic/LogFileFormats,BusinessLogic/Sources,Ui/Settings/CustomFormats}; src/Tailviewer.Core/{Formats,Parsers,Columns} | FR-006 | P06 | plain streaming text only in UI |
| F10 | Encoding selection and persistence; malformed text and BOM | src/Tailviewer/{Settings/LogFileSettings.cs,Ui/Settings/EncodingViewModel.cs} | FR-006 | P02/P04 | partial: encoding models/raw names preserved; selector/import pending |
| F11 | Bookmarks, navigation, persistence and source identity | src/Tailviewer/{BusinessLogic/Bookmarks,Settings/Bookmarks,Ui/SidePanel/Bookmarks} | FR-007 | P07 | partial: bookmark XML models ported; navigation/context/UI pending |
| F12 | Properties, issue analysis, outline and navigation | src/Tailviewer/Ui/SidePanel/{Property,Issues,Outline} | FR-007 | P07/P09 | not ported |
| F13 | Action center, progress, notifications, errors, help/about/changelog, explorer actions | src/Tailviewer/{BusinessLogic/ActionCenter,BusinessLogic/FileExplorer,Ui/ActionCenter,Ui/About} | FR-007 | P07 | status/error text only |
| F14 | Profile import/backup/conflicts, window/layout, source and view settings | src/Tailviewer/Settings; src/Tailviewer/Ui/Settings | FR-008 | P02 | partial: Core models, bounded store and verified bundle import/recovery pass; import UI pending |
| F15 | Export selected/filtered entries, options, progress/cancel, file overwrite | src/Tailviewer/{BusinessLogic/Exporter,Settings/ExportSettings.cs,Ui/ActionCenter/ExportViewModel.cs} | FR-009 | P07 | export preference model ported; exporter/UI pending |
| F16 | Plugin metadata/archive/discovery/dependencies/install/update/disable/remove | src/Tailviewer.Archiver; src/Tailviewer/BusinessLogic/Plugins; src/Tailviewer/Ui/Plugins | FR-010 | P08/P09 | partial: SDK archive/loader basics pass; lifecycle/versioned isolation pending |
| F17 | Custom source plugin and serialized source configuration | src/Tailviewer.Api/Plugins/ICustomDataSourcePlugin.cs; src/Tailviewer.Api/Ui/ICustomDataSourceViewModel.cs | FR-010 | P09 | legacy host only |
| F18 | Source parser, entry parser, format matcher and custom format creator plugins | src/Tailviewer.Api/Plugins/{ILogSourceParserPlugin,ILogEntryParserPlugin,ILogFileFormatMatcherPlugin,ICustomLogFileFormatCreatorPlugin}.cs | FR-010 | P09 | legacy host only |
| F19 | Issue analyser and outline plugins/listeners | src/Tailviewer.Api/Plugins/{ILogFileIssuesPlugin,ILogFileOutlinePlugin}.cs | FR-010 | P09 | legacy host only |
| F20 | Property presenter and custom UI/view-model/navigation extensions | src/Tailviewer.Api/Ui; src/Tailviewer.Api/Plugins/ICustomDataSourcePlugin.cs | FR-010 | P09 | WPF UI contract port needed |
| F21 | Serilog bundled format, creator utility and LogLevelPlugin example | src/Plugins; examples/Tailviewer.LogLevelPlugin | FR-011 | P09 | legacy-only |
| F22 | Archive CLI pack/inspect/test-load/metadata, author packaging and NuGet | src/Tailviewer.Archiver; src/Tailviewer.Api/Tailviewer.Api.nuspec; docs/DevelopingPlugins.md | FR-011 | P08 | partial: DLL pack/changelog CLI pass; author packaging and baseline stubs pending |
| F23 | Repository publish/list/download/icons/changes, user/token admin, remove/export/configuration | src/Tailviewer.PluginRepository; src/Tailviewer.Archiver/Repository | FR-011 | P10 | SharpRemote/IsabelDb legacy service |
| F24 | Windows service lifecycle, configuration and controlled client integration | src/Tailviewer.PluginRepository.Service | FR-011 | P10 | ServiceBase legacy-only |
| F25 | Portable EXE/ZIP, installer, upgrade/uninstall, icon/license/version | installer/Tailviewer.Avalonia.iss; src/Installer; src/Tailviewer.Avalonia/Properties/PublishProfiles | FR-012 | P12 | alpha packaging exists; full lifecycle incomplete |
| F26 | Update check/preference/channel, consent/download/verify/install/restart/recovery | src/Tailviewer/{BusinessLogic/AutoUpdates,Ui/AutoUpdateViewModel.cs,Settings/AutoUpdateSettings.cs} | FR-013/014 | P11/P12 | preference/proxy models ported; consent/update flow and endpoints pending |
| F27 | Offline operation, log privacy, archive path validation, update provenance | src/Tailviewer.Archiver; src/Tailviewer/BusinessLogic/AutoUpdates | FR-014/015 | P08/P11 | requires boundary tests |
| F28 | Keyboard/menu shortcuts, accessibility, themes, native dialogs and progress states | src/Tailviewer/Ui; src/Tailviewer.Avalonia/MainWindow.cs | FR-016 | P03-P13 | partial: native picker open/cancel and UIA row-copy verified; broader keyboard/accessibility acceptance pending |
| F29 | All shipping project builds, tests, samples, docs and required notices | dependency-inventory.md; src/*Tests; docs; assets/branding | FR-001/017/018 | P00-P13 | partial: API/Core/Avalonia/Archiver modern subset; remaining shipping projects pending |

## Inventory closure rule

Every public menu/command, shipping plugin contract, settings element and companion CLI operation must map to a row and fixture. Expand these families when code inspection finds an unlisted behavior. For each row record fixture IDs, old/new expected behavior, test command/result and platform in phase evidence; missing evidence keeps the row incomplete. No row is marked migrated by this planning document.

## Baseline and declared-surface evidence

- [T001 baseline](baseline.md): fresh modern build/tests and failed isolated legacy build attempts, with explicit limitations and raw artifact fingerprints.
- [T002 declared surface](legacy-surface.md): 106 source groups and 414 concrete command/binding/schema/CLI/interface expressions, stable fixture IDs and candidate existing tests. These source findings do not establish runtime parity.
- [T003 fixture manifest](../../src/Tailviewer.Core.Tests/Migration/Fixtures/manifest.json): 14 small/reused fixtures, 106 source-case identities and seven generated-workload recipes; byte hashes/line/XML expectations are checked by `FixtureManifestTest`.
- [T004 test ownership](test-ownership.md): all 175 legacy C# fixture/helper/metadata files have proposed modern owners; no legacy fixture is claimed ported by this mapping.
- [T006/T007 metadata boundary](metadata-boundary.md): encoded icon metadata, a portable four-test harness, and separately executed legacy WPF adapter regressions. The historical metadata harness is now folded into the full suite by T008.
- Preserve proxy/DPAPI credentials safely, delta/elapsed/empty-line/display choices, folder regex/recursive settings, and `git-get-changes` author tooling. Dormant dashboard schema must round-trip without activating playground-only features.

[T008 SDK migration evidence](archiver-sdk-migration.md) records 81 Archiver passes, 14 Avalonia passes, conditional legacy dependency isolation, metadata-only dependency inspection, owned test paths and CLI smoke results. F16/F22 remain partially migrated until later archive/security/author/repository tasks close; this does not declare plugin UI or full project parity.

## Complete desktop source checklist

The following leaf source paths allow reviewers to spot overlooked dialogs/settings/helpers; generated from the source tree, excluding build output. They are evidence pointers, not 1:1 new files to create.

### src/Tailviewer/Ui

- `src/Tailviewer/Ui/About/AboutFlyoutViewModel.cs`
- `src/Tailviewer/Ui/About/ChangelogControl.xaml.cs`
- `src/Tailviewer/Ui/About/ChangelogViewModel.cs`
- `src/Tailviewer/Ui/About/Dependency.cs`
- `src/Tailviewer/Ui/About/LicenseControl.xaml.cs`
- `src/Tailviewer/Ui/About/TailviewerControl.xaml.cs`
- `src/Tailviewer/Ui/AboutViewModel.cs`
- `src/Tailviewer/Ui/AbstractMainPanelViewModel.cs`
- `src/Tailviewer/Ui/ActionCenter/AbstractNotificationViewModel.cs`
- `src/Tailviewer/Ui/ActionCenter/ActionCenterControl.xaml.cs`
- `src/Tailviewer/Ui/ActionCenter/ActionCenterViewModel.cs`
- `src/Tailviewer/Ui/ActionCenter/BugViewModel.cs`
- `src/Tailviewer/Ui/ActionCenter/BuildViewModel.cs`
- `src/Tailviewer/Ui/ActionCenter/ChangeViewModel.cs`
- `src/Tailviewer/Ui/ActionCenter/ExportViewModel.cs`
- `src/Tailviewer/Ui/ActionCenter/INotificationViewModel.cs`
- `src/Tailviewer/Ui/ActionCenter/NotificationViewModel.cs`
- `src/Tailviewer/Ui/AutoPopup.cs`
- `src/Tailviewer/Ui/AutoUpdateControl.xaml.cs`
- `src/Tailviewer/Ui/AutoUpdateViewModel.cs`
- `src/Tailviewer/Ui/Converters/AdjustingDoubleConverter.cs`
- `src/Tailviewer/Ui/Converters/BrushType.cs`
- `src/Tailviewer/Ui/Converters/CountConverter.cs`
- `src/Tailviewer/Ui/Converters/DoubleOneToHiddenConverter.cs`
- `src/Tailviewer/Ui/Converters/FollowTailTooltipConverter.cs`
- `src/Tailviewer/Ui/Converters/LevelToggleTooltipConverter.cs`
- `src/Tailviewer/Ui/Converters/NonMergedDataSourceToVisibilityCollapsedConverter.cs`
- `src/Tailviewer/Ui/Converters/NullToNotAvailableConverter.cs`
- `src/Tailviewer/Ui/Converters/ShowAllToContentConverter.cs`
- `src/Tailviewer/Ui/Converters/SkippedDueToTimestampConverter.cs`
- `src/Tailviewer/Ui/Converters/VersionConverter.cs`
- `src/Tailviewer/Ui/Converters/ZeroToHiddenConverter.cs`
- `src/Tailviewer/Ui/DataSourceTree/CustomDataSourceViewModel.cs`
- `src/Tailviewer/Ui/DataSourceTree/DataSourceArrangeAdorner.cs`
- `src/Tailviewer/Ui/DataSourceTree/DataSourceDragAdorner.cs`
- `src/Tailviewer/Ui/DataSourceTree/DataSourceDropAdorner.cs`
- `src/Tailviewer/Ui/DataSourceTree/DataSourceDropType.cs`
- `src/Tailviewer/Ui/DataSourceTree/DataSourcesControl.cs`
- `src/Tailviewer/Ui/DataSourceTree/DataSourcesToggleButton.cs`
- `src/Tailviewer/Ui/DataSourceTree/DropInfo.cs`
- `src/Tailviewer/Ui/DataSourceTree/FileDataSourceViewModel.cs`
- `src/Tailviewer/Ui/DataSourceTree/FolderDataSourceViewModel.cs`
- `src/Tailviewer/Ui/DataSourceTree/ICustomDataSourceViewModel.cs`
- `src/Tailviewer/Ui/DataSourceTree/IDataSourceViewModel.cs`
- `src/Tailviewer/Ui/DataSourceTree/IFileDataSourceViewModel.cs`
- `src/Tailviewer/Ui/DataSourceTree/IMergedDataSourceViewModel.cs`
- `src/Tailviewer/Ui/DataSourceTree/ISingleDataSourceViewModel.cs`
- `src/Tailviewer/Ui/DataSourceTree/MergedDataSourceViewModel.cs`
- `src/Tailviewer/Ui/DataSourceTree/TreeItem.cs`
- `src/Tailviewer/Ui/DragLayer.cs`
- `src/Tailviewer/Ui/EmptyState.cs`
- `src/Tailviewer/Ui/FlatImage.cs`
- `src/Tailviewer/Ui/GoToLine/GoToLinePopup.cs`
- `src/Tailviewer/Ui/GoToLine/GoToLineViewModel.cs`
- `src/Tailviewer/Ui/IFlyoutViewModel.cs`
- `src/Tailviewer/Ui/ImageLabel.cs`
- `src/Tailviewer/Ui/IMainPanelEntry.cs`
- `src/Tailviewer/Ui/IMainPanelViewModel.cs`
- `src/Tailviewer/Ui/IMainWindowViewModel.cs`
- `src/Tailviewer/Ui/LogView/AbstractDataSourceViewModel.cs`
- `src/Tailviewer/Ui/LogView/AbstractLogColumnPresenter.cs`
- `src/Tailviewer/Ui/LogView/AbstractLogEntryValueFormatter.cs`
- `src/Tailviewer/Ui/LogView/AddCustomDataSourceViewModel.cs`
- `src/Tailviewer/Ui/LogView/Any/AnyColumnPresenter.cs`
- `src/Tailviewer/Ui/LogView/Any/AnyFormatter.cs`
- `src/Tailviewer/Ui/LogView/DataSource/DataSourceCanvas.cs`
- `src/Tailviewer/Ui/LogView/DataSourceDisplayModeToggleButton.cs`
- `src/Tailviewer/Ui/LogView/DeltaTimes/DeltaTimeColumnPresenter.cs`
- `src/Tailviewer/Ui/LogView/DeltaTimes/DeltaTimeFormatter.cs`
- `src/Tailviewer/Ui/LogView/ElapsedTime/ElapsedTimeColumnPresenter.cs`
- `src/Tailviewer/Ui/LogView/ElapsedTime/ElapsedTimeFormatter.cs`
- `src/Tailviewer/Ui/LogView/IFindAllViewModel.cs`
- `src/Tailviewer/Ui/LogView/ILogEntryValueFormatter.cs`
- `src/Tailviewer/Ui/LogView/ILogFileColumnPresenter.cs`
- `src/Tailviewer/Ui/LogView/ILogViewMainPanelViewModel.cs`
- `src/Tailviewer/Ui/LogView/ISearchViewModel.cs`
- `src/Tailviewer/Ui/LogView/LineNumbers/LineNumberFormatter.cs`
- `src/Tailviewer/Ui/LogView/LineNumbers/OriginalLineNumberColumnPresenter.cs`
- `src/Tailviewer/Ui/LogView/LogEntryListView.cs`
- `src/Tailviewer/Ui/LogView/LogLevels/LevelFormatter.cs`
- `src/Tailviewer/Ui/LogView/LogLevels/LogLevelColumnPresenter.cs`
- `src/Tailviewer/Ui/LogView/LogLevelToggleButton.cs`
- `src/Tailviewer/Ui/LogView/LogViewerControl.cs`
- `src/Tailviewer/Ui/LogView/LogViewerViewModel.cs`
- `src/Tailviewer/Ui/LogView/LogViewMainPanelViewModel.cs`
- `src/Tailviewer/Ui/LogView/Messages/MessageColumnPresenter.cs`
- `src/Tailviewer/Ui/LogView/Messages/MessageFormatter.cs`
- `src/Tailviewer/Ui/LogView/PluginDescriptionControl.xaml.cs`
- `src/Tailviewer/Ui/LogView/PresentationLogSource.cs`
- `src/Tailviewer/Ui/LogView/SelectMode.cs`
- `src/Tailviewer/Ui/LogView/TextBrushes.cs`
- `src/Tailviewer/Ui/LogView/TextCanvas.cs`
- `src/Tailviewer/Ui/LogView/TextLine.cs`
- `src/Tailviewer/Ui/LogView/TextSegment.cs`
- `src/Tailviewer/Ui/LogView/Timestamps/TimestampColumnPresenter.cs`
- `src/Tailviewer/Ui/LogView/Timestamps/TimestampFormatter.cs`
- `src/Tailviewer/Ui/LogView/ToolbarToggleButton.cs`
- `src/Tailviewer/Ui/MainWindow.xaml.cs`
- `src/Tailviewer/Ui/MainWindowViewModel.cs`
- `src/Tailviewer/Ui/Menu/AbstractMainMenuViewModel.cs`
- `src/Tailviewer/Ui/Menu/AbstractMenuViewModel.cs`
- `src/Tailviewer/Ui/Menu/CommandMenuViewModel.cs`
- `src/Tailviewer/Ui/Menu/EditMenuViewModel.cs`
- `src/Tailviewer/Ui/Menu/ExcludeAllInGroupViewModel.cs`
- `src/Tailviewer/Ui/Menu/FileMenuViewModel.cs`
- `src/Tailviewer/Ui/Menu/HelpMenuViewModel.cs`
- `src/Tailviewer/Ui/Menu/IMenu.cs`
- `src/Tailviewer/Ui/Menu/IMenuViewModel.cs`
- `src/Tailviewer/Ui/Menu/IncludeAllInGroupViewModel.cs`
- `src/Tailviewer/Ui/Menu/KeyBindingCommand.cs`
- `src/Tailviewer/Ui/Menu/MainMenu.cs`
- `src/Tailviewer/Ui/Menu/ParentMenuViewModel.cs`
- `src/Tailviewer/Ui/Menu/ToggleExcludeFromGroupViewModel.cs`
- `src/Tailviewer/Ui/Menu/ToggleMenuViewModel.cs`
- `src/Tailviewer/Ui/Menu/ViewMenuViewModel.cs`
- `src/Tailviewer/Ui/NavigationService.cs`
- `src/Tailviewer/Ui/Plugins/AvailablePluginViewModel.cs`
- `src/Tailviewer/Ui/Plugins/InstalledPluginViewModel.cs`
- `src/Tailviewer/Ui/Plugins/IPluginViewModel.cs`
- `src/Tailviewer/Ui/Plugins/PluginsMainPanelViewModel.cs`
- `src/Tailviewer/Ui/QuickFilter/FilterToggleButton.cs`
- `src/Tailviewer/Ui/QuickFilter/QuickFiltersViewModel.cs`
- `src/Tailviewer/Ui/QuickFilter/QuickFilterViewModel.cs`
- `src/Tailviewer/Ui/QuickFilterMatchTypeToggleButton.cs`
- `src/Tailviewer/Ui/QuickNavigation/DataSourceSuggestionViewModel.cs`
- `src/Tailviewer/Ui/QuickNavigation/QuickNavigationPopup.cs`
- `src/Tailviewer/Ui/QuickNavigation/QuickNavigationViewModel.cs`
- `src/Tailviewer/Ui/Settings/CustomFormats/CustomFormatPluginViewModel.cs`
- `src/Tailviewer/Ui/Settings/CustomFormats/CustomFormatsSettingsViewModel.cs`
- `src/Tailviewer/Ui/Settings/CustomFormats/CustomFormatViewModel.cs`
- `src/Tailviewer/Ui/Settings/EncodingViewModel.cs`
- `src/Tailviewer/Ui/Settings/LogLevelSettingsViewModel.cs`
- `src/Tailviewer/Ui/Settings/SettingsControl.xaml.cs`
- `src/Tailviewer/Ui/Settings/SettingsFlyoutViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/AbstractSidePanelViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Bookmarks/BookmarksViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Bookmarks/BookmarkViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/DataSources/DataSourcesViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Highlighters/HighlightersSidePanelViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Highlighters/HighlighterViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/HyperlinkRun.cs`
- `src/Tailviewer/Ui/SidePanel/ISidePanelViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Issues/IssuesSidePanelViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Issues/IssuesViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Issues/IssueViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Issues/LogSourceIssueAnalyserDummy.cs`
- `src/Tailviewer/Ui/SidePanel/Issues/NoThrowLogFileIssuePlugin.cs`
- `src/Tailviewer/Ui/SidePanel/Issues/NoThrowLogSourceIssueAnalyser.cs`
- `src/Tailviewer/Ui/SidePanel/Issues/SeverityToBrushConverter.cs`
- `src/Tailviewer/Ui/SidePanel/Issues/SeverityToIconConverter.cs`
- `src/Tailviewer/Ui/SidePanel/Outline/IInternalLogFileOutlineViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Outline/LogFileOutlineViewModelProxy.cs`
- `src/Tailviewer/Ui/SidePanel/Outline/LogFilePropertyConverter.cs`
- `src/Tailviewer/Ui/SidePanel/Outline/OutlineSidePanelViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Property/DefaultPropertyPresenter.cs`
- `src/Tailviewer/Ui/SidePanel/Property/EncodingPropertyPresenter.cs`
- `src/Tailviewer/Ui/SidePanel/Property/PropertiesSidePanelViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/Property/PropertyPresenterRegistry.cs`
- `src/Tailviewer/Ui/SidePanel/QuickFilters/QuickFiltersSidePanelViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/QuickFilters/TimeFilter/IChoseTimeRangeViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/QuickFilters/TimeFilter/SpecialTimeRangeViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/QuickFilters/TimeFilter/TimeChooserControl.xaml.cs`
- `src/Tailviewer/Ui/SidePanel/QuickFilters/TimeFilter/TimeFiltersViewModel.cs`
- `src/Tailviewer/Ui/SidePanel/SidePanelControl.cs`

### src/Tailviewer/BusinessLogic

- `src/Tailviewer/BusinessLogic/ActionCenter/ActionCenter.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/ActionCenterExtensions.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/Build.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/Change.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/ExportAction.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/ExportException.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/IActionCenter.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/IBug.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/IExportAction.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/INotification.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/IOpenFolderAction.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/Level.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/Notification.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/OpenFolderAction.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/OpenFolderException.cs`
- `src/Tailviewer/BusinessLogic/ActionCenter/UnhandledException.cs`
- `src/Tailviewer/BusinessLogic/AutoUpdates/AutoUpdater.cs`
- `src/Tailviewer/BusinessLogic/AutoUpdates/IAutoUpdater.cs`
- `src/Tailviewer/BusinessLogic/AutoUpdates/VersionInfo.cs`
- `src/Tailviewer/BusinessLogic/Bookmarks/Bookmark.cs`
- `src/Tailviewer/BusinessLogic/Bookmarks/BookmarkCollection.cs`
- `src/Tailviewer/BusinessLogic/DataSources/AbstractDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/Count.cs`
- `src/Tailviewer/BusinessLogic/DataSources/Custom/CustomDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/Custom/ICustomDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/Custom/NoThrowCustomDataSourceConfiguration.cs`
- `src/Tailviewer/BusinessLogic/DataSources/Custom/NoThrowCustomDataSourcePlugin.cs`
- `src/Tailviewer/BusinessLogic/DataSources/DataSources.cs`
- `src/Tailviewer/BusinessLogic/DataSources/FileDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/FolderDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/IDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/IDataSources.cs`
- `src/Tailviewer/BusinessLogic/DataSources/IFileDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/IFolderDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/IMergedDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/IMultiDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/ISingleDataSource.cs`
- `src/Tailviewer/BusinessLogic/DataSources/MergedDataSource.cs`
- `src/Tailviewer/BusinessLogic/Drive.cs`
- `src/Tailviewer/BusinessLogic/Exporter/ILogFileExporter.cs`
- `src/Tailviewer/BusinessLogic/Exporter/ILogFileToFileExporter.cs`
- `src/Tailviewer/BusinessLogic/Exporter/LogFileToFileExporter.cs`
- `src/Tailviewer/BusinessLogic/Exporter/LogFileToStreamExporter.cs`
- `src/Tailviewer/BusinessLogic/FileEx.cs`
- `src/Tailviewer/BusinessLogic/FileExplorer/FileExplorer.cs`
- `src/Tailviewer/BusinessLogic/FileExplorer/IFileExplorer.cs`
- `src/Tailviewer/BusinessLogic/FileExplorer/NativeMethods.cs`
- `src/Tailviewer/BusinessLogic/Filters/IQuickFilters.cs`
- `src/Tailviewer/BusinessLogic/Filters/QuickFilter.cs`
- `src/Tailviewer/BusinessLogic/Filters/QuickFilters.cs`
- `src/Tailviewer/BusinessLogic/Filters/TimeFilter.cs`
- `src/Tailviewer/BusinessLogic/Highlighters/Highlighter.cs`
- `src/Tailviewer/BusinessLogic/Highlighters/HighlighterCollection.cs`
- `src/Tailviewer/BusinessLogic/Highlighters/HighlighterId.cs`
- `src/Tailviewer/BusinessLogic/Highlighters/IHighlighters.cs`
- `src/Tailviewer/BusinessLogic/LogFileFormats/ILogFileFormatRegistry.cs`
- `src/Tailviewer/BusinessLogic/LogFileFormats/LogFileFormatRegistry.cs`
- `src/Tailviewer/BusinessLogic/NoThrowTimestampParser.cs`
- `src/Tailviewer/BusinessLogic/Plugins/IPluginUpdater.cs`
- `src/Tailviewer/BusinessLogic/Plugins/PluginUpdater.cs`
- `src/Tailviewer/BusinessLogic/Searches/DispatchedSearchResults.cs`
- `src/Tailviewer/BusinessLogic/Searches/ILogFileSearchListener.cs`
- `src/Tailviewer/BusinessLogic/Searches/ILogSourceSearch.cs`
- `src/Tailviewer/BusinessLogic/Searches/ISearchResults.cs`
- `src/Tailviewer/BusinessLogic/Searches/ISearchResultsByLogLineIndex.cs`
- `src/Tailviewer/BusinessLogic/Searches/LogFileSearchListenerCollection.cs`
- `src/Tailviewer/BusinessLogic/Searches/LogMatch.cs`
- `src/Tailviewer/BusinessLogic/Searches/LogMatchIndex.cs`
- `src/Tailviewer/BusinessLogic/Searches/LogMatchSection.cs`
- `src/Tailviewer/BusinessLogic/Searches/LogSourceSearch.cs`
- `src/Tailviewer/BusinessLogic/Searches/LogSourceSearchProxy.cs`
- `src/Tailviewer/BusinessLogic/Searches/SearchResults.cs`
- `src/Tailviewer/BusinessLogic/Searches/SearchResultsByLogLineIndex.cs`
- `src/Tailviewer/BusinessLogic/Searches/SearchResultsListener.cs`
- `src/Tailviewer/BusinessLogic/Sources/ILogSourceFactory.cs`
- `src/Tailviewer/BusinessLogic/Sources/LogEntryParserFactory.cs`
- `src/Tailviewer/BusinessLogic/Sources/LogFileFormatMatcher.cs`
- `src/Tailviewer/BusinessLogic/Sources/NoThrowLogEntryParser.cs`
- `src/Tailviewer/BusinessLogic/Sources/NoThrowLogFileFormatMatcher.cs`
- `src/Tailviewer/BusinessLogic/Sources/NoThrowLogSource.cs`
- `src/Tailviewer/BusinessLogic/Sources/ParsingLogSourceFactory.cs`
- `src/Tailviewer/BusinessLogic/Sources/PluginLogSourceFactory.cs`

### src/Tailviewer/Settings

- `src/Tailviewer/Settings/ApplicationSettings.cs`
- `src/Tailviewer/Settings/AutoUpdateSettings.cs`
- `src/Tailviewer/Settings/Bookmarks/Bookmarks.cs`
- `src/Tailviewer/Settings/Bookmarks/BookmarkSettings.cs`
- `src/Tailviewer/Settings/Bookmarks/IBookmarks.cs`
- `src/Tailviewer/Settings/ColorXmlExtensions.cs`
- `src/Tailviewer/Settings/CustomFormats/CustomFormatSettings.cs`
- `src/Tailviewer/Settings/CustomFormats/CustomLogFileFormat.cs`
- `src/Tailviewer/Settings/CustomFormats/IDataSourcesSettings.cs`
- `src/Tailviewer/Settings/Dashboard/LayoutSettings.cs`
- `src/Tailviewer/Settings/Dashboard/Widgets/WidgetSettings.cs`
- `src/Tailviewer/Settings/DataSource.cs`
- `src/Tailviewer/Settings/DataSourceDisplayMode.cs`
- `src/Tailviewer/Settings/DataSourceSettings.cs`
- `src/Tailviewer/Settings/Desktop.cs`
- `src/Tailviewer/Settings/ExportSettings.cs`
- `src/Tailviewer/Settings/IApplicationSettings.cs`
- `src/Tailviewer/Settings/IAutoUpdateSettings.cs`
- `src/Tailviewer/Settings/ICustomFormatsSettings.cs`
- `src/Tailviewer/Settings/IExportSettings.cs`
- `src/Tailviewer/Settings/ILogViewerSettings.cs`
- `src/Tailviewer/Settings/IMainWindowSettings.cs`
- `src/Tailviewer/Settings/LogFileSettings.cs`
- `src/Tailviewer/Settings/LogLevelSettings.cs`
- `src/Tailviewer/Settings/LogViewerSettings.cs`
- `src/Tailviewer/Settings/MainWindowSettings.cs`
- `src/Tailviewer/Settings/TextSettings.cs`
