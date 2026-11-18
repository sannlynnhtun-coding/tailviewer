# Project and direct dependency inventory

**Snapshot**: 2026-11-19, source baseline `5e351359`; generated from every tracked-layout project/package manifest (excluding build outputs). Direct declarations only: transitive restore graph, vulnerabilities and runtime compatibility require the phase checks.

The fresh [T005 resolved dependency baseline](dependency-baseline.md) records six modern target graphs, native/RID declarations, advisory feed provenance and actual legacy source usage. It does not establish old-package runtime compatibility.

## Every project

| Project | Current target | Scope | Project references |
|---|---|---|---|
| `examples/Tailviewer.LogLevelPlugin/Tailviewer.LogLevelPlugin.csproj` | v4.7.1 | example | ../../src/Tailviewer.Api/Tailviewer.Api.csproj, ../../src/Tailviewer.Core/Tailviewer.Core.csproj |
| `playground/ControlPlayground/ControlPlayground.csproj` | v4.5.2 | experimental | none |
| `playground/FileLogger/FileLogger.csproj` | v4.5 | experimental | ../LoggerCore/LoggerCore.csproj |
| `playground/LogFile2Playground/LogFile2Playground.csproj` | v4.5.2 | experimental | ../../src/Tailviewer.Api/Tailviewer.Api.csproj |
| `playground/Logger/Logger.csproj` | v4.5 | experimental | ../../src/Tailviewer.Test/Tailviewer.Test.csproj |
| `playground/LoggerCore/LoggerCore.csproj` | v4.5 | experimental | none |
| `playground/NewDesign/NewDesign.csproj` | v4.5.2 | experimental | none |
| `playground/SQLiteLogger/SQLiteLogger.csproj` | v4.5 | experimental | ../LoggerCore/LoggerCore.csproj |
| `playground/TablePlayground/TablePlayground.csproj` | v4.5.2 | experimental | ../../src/Tailviewer.Api/Tailviewer.Api.csproj, ../../src/Tailviewer.Core/Tailviewer.Core.csproj, ../../src/Tailviewer/Tailviewer.csproj |
| `src/Installer/Installer.csproj` | v4.7.1 | shipping | ../Tailviewer.Api/Tailviewer.Api.csproj, ../Tailviewer.Archiver/Tailviewer.Archiver.csproj, ../Tailviewer.Core/Tailviewer.Core.csproj, ../Tailviewer/Tailviewer.csproj |
| `src/Plugins/Tailviewer.Formats.Serilog/Tailviewer.Formats.Serilog.csproj` | v4.7.1 | shipping | ../../Tailviewer.Api/Tailviewer.Api.csproj, ../../Tailviewer.Core/Tailviewer.Core.csproj |
| `src/Plugins/Tailviewer.Formats.Serilog.Tests/Tailviewer.Formats.Serilog.Tests.csproj` | v4.7.2 | test | ../../Tailviewer.Api/Tailviewer.Api.csproj, ../../Tailviewer.Archiver/Tailviewer.Archiver.csproj, ../../Tailviewer.Core/Tailviewer.Core.csproj, ../../Tailviewer/Tailviewer.csproj, ../Tailviewer.Formats.Serilog/Tailviewer.Formats.Serilog.csproj |
| `src/Plugins/Tailviewer.PluginCreator/Tailviewer.PluginCreator.csproj` | v4.7.1 | shipping | ../../Tailviewer.Api/Tailviewer.Api.csproj |
| `src/Tailviewer/Tailviewer.csproj` | v4.7.1 | shipping | ../Tailviewer.Api/Tailviewer.Api.csproj, ../Tailviewer.Archiver/Tailviewer.Archiver.csproj, ../Tailviewer.Core/Tailviewer.Core.csproj |
| `src/Tailviewer.Acceptance.Tests/Tailviewer.Acceptance.Tests.csproj` | v4.7.1 | test | ../Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj, ../Tailviewer.Api/Tailviewer.Api.csproj, ../Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj, ../Tailviewer.Core/Tailviewer.Core.csproj, ../Tailviewer.Tests/Tailviewer.Tests.csproj, ../Tailviewer/Tailviewer.csproj |
| `src/Tailviewer.Api/Tailviewer.Api.csproj` | net45;net10.0 | shipping | none |
| `src/Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj` | net471;net10.0 | test | ../Tailviewer.Api/Tailviewer.Api.csproj |
| `src/Tailviewer.Archiver/Tailviewer.Archiver.csproj` | v4.5.2 | shipping | ../Tailviewer.Api/Tailviewer.Api.csproj, ../Tailviewer.Core/Tailviewer.Core.csproj |
| `src/Tailviewer.Archiver.Tests/Tailviewer.Archiver.Tests.csproj` | v4.7.1 | test | ../Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj, ../Tailviewer.Api/Tailviewer.Api.csproj, ../Tailviewer.Archiver/Tailviewer.Archiver.csproj, ../Tailviewer.Tests/Tailviewer.Tests.csproj, ../Tailviewer/Tailviewer.csproj |
| `src/Tailviewer.Avalonia/Tailviewer.Avalonia.csproj` | net10.0 | shipping | ../Tailviewer.Core/Tailviewer.Core.csproj |
| `src/Tailviewer.Avalonia.Tests/Tailviewer.Avalonia.Tests.csproj` | net10.0 | test | ../Tailviewer.Avalonia/Tailviewer.Avalonia.csproj |
| `src/Tailviewer.Core/Tailviewer.Core.csproj` | net45;net10.0 | shipping | ../Tailviewer.Api/Tailviewer.Api.csproj |
| `src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj` | net471;net10.0 | test | ../Tailviewer.Api/Tailviewer.Api.csproj, ../Tailviewer.Core/Tailviewer.Core.csproj, ../Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj |
| `src/Tailviewer.PluginRepository/Tailviewer.PluginRepository.csproj` | v4.5.2 | shipping | ../Tailviewer.Archiver/Tailviewer.Archiver.csproj, ../Tailviewer.Core/Tailviewer.Core.csproj |
| `src/Tailviewer.PluginRepository.Service/Tailviewer.PluginRepository.Service.csproj` | v4.7.1 | shipping | ../Tailviewer.Archiver/Tailviewer.Archiver.csproj, ../Tailviewer.PluginRepository/Tailviewer.PluginRepository.csproj |
| `src/Tailviewer.PluginRepository.Tests/Tailviewer.PluginRepository.Tests.csproj` | v4.7.1 | test | ../Tailviewer.Archiver/Tailviewer.Archiver.csproj, ../Tailviewer.PluginRepository/Tailviewer.PluginRepository.csproj |
| `src/Tailviewer.System.Tests/Tailviewer.System.Tests.csproj` | v4.7.1 | test | ../Tailviewer.Api/Tailviewer.Api.csproj, ../Tailviewer/Tailviewer.csproj |
| `src/Tailviewer.Tests/Tailviewer.Tests.csproj` | v4.7.1 | test | ../Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj, ../Tailviewer.Api/Tailviewer.Api.csproj, ../Tailviewer.Archiver/Tailviewer.Archiver.csproj, ../Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj, ../Tailviewer.Core/Tailviewer.Core.csproj, ../Tailviewer/Tailviewer.csproj |

All shipping projects, example/plugin-author tooling and test projects are in migration scope. Playground projects are excluded from release migration unless a shipping dependency is discovered; their references still appear above. The WPF desktop project is a behavioral baseline to retire after its behavior is ported, not a second modern desktop product.

## Every declared package/version

| Package | Declared version | Consumers/manifests |
|---|---|---|
| Avalonia.Desktop | 12.1.6 | `src/Tailviewer.Avalonia/Tailviewer.Avalonia.csproj` |
| Avalonia.Headless.NUnit | 12.1.6 | `src/Tailviewer.Avalonia.Tests/Tailviewer.Avalonia.Tests.csproj` |
| Avalonia.Themes.Fluent | 12.1.6 | `src/Tailviewer.Avalonia/Tailviewer.Avalonia.csproj` |
| Castle.Core | 4.1.1 | `src/Plugins/Tailviewer.Formats.Serilog.Tests/packages.config`<br>`src/Tailviewer.Acceptance.Tests/packages.config`<br>`src/Tailviewer.Archiver.Tests/packages.config`<br>`src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.Tests/packages.config` |
| CommandLineParser | 2.5.0 | `src/Tailviewer.Archiver/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config` |
| EntityFramework | 6.2.0 | `src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config` |
| Extended.Wpf.Toolkit | 4.0.1 | `src/Tailviewer/packages.config` |
| FluentAssertions | 5.6.0 | `src/Plugins/Tailviewer.Formats.Serilog.Tests/packages.config`<br>`src/Tailviewer.Acceptance.Tests/packages.config`<br>`src/Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj`<br>`src/Tailviewer.Archiver.Tests/packages.config`<br>`src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj`<br>`src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.System.Tests/packages.config`<br>`src/Tailviewer.Tests/packages.config` |
| FluentAssertions | 7.2.2 | `src/Tailviewer.Avalonia.Tests/Tailviewer.Avalonia.Tests.csproj` |
| FluentAssertions.Extensions | 1.0.18 | `src/Plugins/Tailviewer.Formats.Serilog.Tests/packages.config`<br>`src/Tailviewer.Acceptance.Tests/packages.config`<br>`src/Tailviewer.Tests/packages.config` |
| IsabelDb | 0.6.21 | `src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config` |
| MMQ | 1.0.15 | `src/Tailviewer/packages.config` |
| Metrolib | 0.3.0.116 | `playground/ControlPlayground/packages.config`<br>`playground/LogFile2Playground/packages.config`<br>`playground/NewDesign/packages.config`<br>`playground/TablePlayground/packages.config` |
| Metrolib | 0.3.0.162 | `src/Installer/packages.config`<br>`src/Tailviewer.Acceptance.Tests/packages.config`<br>`src/Tailviewer.Archiver/packages.config`<br>`src/Tailviewer.Tests/packages.config`<br>`src/Tailviewer/packages.config` |
| Microsoft.NET.Test.Sdk | 17.14.1 | `src/Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj`<br>`src/Tailviewer.Avalonia.Tests/Tailviewer.Avalonia.Tests.csproj`<br>`src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj` |
| Moq | 4.7.127 | `src/Plugins/Tailviewer.Formats.Serilog.Tests/packages.config`<br>`src/Tailviewer.Acceptance.Tests/packages.config`<br>`src/Tailviewer.Archiver.Tests/packages.config`<br>`src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj`<br>`src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.Tests/packages.config` |
| NUnit | 3.12.0 | `src/Plugins/Tailviewer.Formats.Serilog.Tests/packages.config`<br>`src/Tailviewer.Acceptance.Tests/packages.config`<br>`src/Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj`<br>`src/Tailviewer.Archiver.Tests/packages.config`<br>`src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj`<br>`src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.System.Tests/packages.config`<br>`src/Tailviewer.Tests/packages.config` |
| NUnit | 4.5.1 | `src/Tailviewer.Avalonia.Tests/Tailviewer.Avalonia.Tests.csproj` |
| NUnit3TestAdapter | 5.0.0 | `src/Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj`<br>`src/Tailviewer.Avalonia.Tests/Tailviewer.Avalonia.Tests.csproj`<br>`src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj` |
| Ookii.Dialogs.Wpf | 1.1.0 | `src/Tailviewer/packages.config` |
| PE | 0.1.13 | `src/Tailviewer.Archiver/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config` |
| SharpRemote | 0.6.184 | `src/Tailviewer.Archiver.Tests/packages.config`<br>`src/Tailviewer.Archiver/packages.config`<br>`src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config`<br>`src/Tailviewer/packages.config` |
| System.Data.SQLite | 1.0.111.0 | `src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config` |
| System.Data.SQLite.Core | 1.0.105.2 | `playground/SQLiteLogger/packages.config` |
| System.Data.SQLite.Core | 1.0.111.0 | `src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config` |
| System.Data.SQLite.EF6 | 1.0.111.0 | `src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config` |
| System.Data.SQLite.Linq | 1.0.111.0 | `src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config` |
| System.Diagnostics.EventLog | 10.0.0 | `src/Tailviewer.Core/Tailviewer.Core.csproj` |
| System.Runtime.CompilerServices.Unsafe | 4.5.2 | `src/Tailviewer.Acceptance.Tests/packages.config`<br>`src/Tailviewer.Archiver.Tests/packages.config` |
| System.Threading.Extensions | 1.1.39 | `playground/LogFile2Playground/packages.config`<br>`playground/LoggerCore/packages.config`<br>`playground/TablePlayground/packages.config` |
| System.Threading.Extensions | 2.0.72 | `src/Plugins/Tailviewer.Formats.Serilog.Tests/packages.config`<br>`src/Tailviewer.Acceptance.Tests/packages.config`<br>`src/Tailviewer.Api/Tailviewer.Api.csproj`<br>`src/Tailviewer.Archiver.Tests/packages.config`<br>`src/Tailviewer.Archiver/packages.config`<br>`src/Tailviewer.Core/Tailviewer.Core.csproj`<br>`src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config`<br>`src/Tailviewer.Tests/packages.config`<br>`src/Tailviewer/packages.config` |
| WpfUnit | 0.1.0.14-alpha | `src/Tailviewer.Acceptance.Tests/packages.config`<br>`src/Tailviewer.Tests/packages.config` |
| log4net | 2.0.12 | `playground/FileLogger/packages.config`<br>`playground/LogFile2Playground/packages.config`<br>`playground/Logger/packages.config`<br>`playground/LoggerCore/packages.config`<br>`playground/SQLiteLogger/packages.config`<br>`playground/TablePlayground/packages.config`<br>`src/Installer/packages.config`<br>`src/Plugins/Tailviewer.Formats.Serilog.Tests/packages.config`<br>`src/Plugins/Tailviewer.Formats.Serilog/packages.config`<br>`src/Tailviewer.Acceptance.Tests/packages.config`<br>`src/Tailviewer.Api/Tailviewer.Api.csproj`<br>`src/Tailviewer.Archiver.Tests/packages.config`<br>`src/Tailviewer.Archiver/packages.config`<br>`src/Tailviewer.Core/Tailviewer.Core.csproj`<br>`src/Tailviewer.PluginRepository.Service/packages.config`<br>`src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config`<br>`src/Tailviewer.Tests/packages.config`<br>`src/Tailviewer/packages.config` |
| log4net | 3.6.0 | `src/Tailviewer.Api/Tailviewer.Api.csproj`<br>`src/Tailviewer.Core/Tailviewer.Core.csproj` |
| protobuf-net | 2.4.0 | `src/Tailviewer.PluginRepository.Tests/packages.config`<br>`src/Tailviewer.PluginRepository/packages.config` |

## Framework/native integration checks

- Scan explicit assembly references and APIs as well as packages: WPF PresentationCore/PresentationFramework/WindowsBase, System.Windows.Forms, System.Drawing, System.Web/Services, System.ServiceProcess, Registry/PInvoke, AssemblyResolve/LoadFrom and reflection emit.
- Check embedded resources, nuspec/build targets, archive CLI output names, service scripts and packaging/signing assumptions after each target conversion.
- Keep legacy target dependencies separate until their last shipping consumer is migrated; do not let old net45-only references leak into modern runtime assets.
