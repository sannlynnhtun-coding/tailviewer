# Tailviewer — .NET 10 + Avalonia ပြောင်းရွှေ့ရေးအစီအစဉ်

စစ်ဆေးသည့်ရက်: 2026-11-18

ဆုံးဖြတ်ချက်: Desktop application ကို `net10.0` + Avalonia သို့ ပြောင်းမည်။ လက်ရှိ log engine၊ filters၊ search၊ merge၊ bookmarks နှင့် plugin လုပ်ဆောင်ချက်များကို ထိန်းမည်။ ပထမဆုံး Windows တွင် feature parity ရပြီးနောက် Linux/macOS ကို OS တစ်ခုချင်းစီတွင် အတည်ပြုမည်။ Mobile/Web ကို ဤ migration တွင် မထည့်ပါ။

## ပထမ implementation အဆင့် — 2026-11-18

- `global.json` တွင် SDK `10.0.302` ကို pin လုပ်ထားသည်။ Patch updates သာ roll forward လုပ်မည်။
- Api/Core နှင့် သက်ဆိုင်ရာ test projects ကို SDK-style/PackageReference သို့ ပြောင်းထားသည်။ Api/Core က `net45;net10.0`၊ tests က `net471;net10.0` ကို ယာယီတွဲထောက်ပံ့သည်။ Legacy binaries ကို မူလ `bin/` တွင်၊ modern binaries ကို project အောက်ရှိ framework-specific output တွင် ထုတ်သည်။
- Core မှ Metrolib ကိုဖြုတ်ထားသည်။ `Properties.Size` သည် nullable byte count `long?` ဖြစ်ပြီး WPF application က display size သို့ပြန်ပြောင်းသည်။ `IEmptyReason.Icon` သည် string identifier ဖြစ်ပြီး WPF host က geometry သို့ map လုပ်သည်။ ဤ public contract များ ပြောင်းသဖြင့် external plugins/consumers ကို rebuild လုပ်ရန်လိုသည်။
- WPF color XML persistence ကို application ဘက်သို့ ရွှေ့ထားသည်။ Settings XML ၏ GUID၊ enum၊ boolean နှင့် DateTime ticks အဟောင်းတို့ကို ထိန်းထားပြီး regression tests ရှိသည်။
- Portable UI plugin methods သည် controls ကို `object` အဖြစ် ပေးသည်။ Legacy target မှာ မူလ WPF signature ထိန်းထားသည်။ Modern custom-data-source/outline interface versions ကို 2/3 သို့ တိုးထားသည်။ Avalonia host ပြောင်းချိန် control type ကို စစ်ရမည်။ API/Core တွင် Avalonia dependency အသစ် မထည့်ထားပါ။
- Windows Event Log ကို platform attribute/guard ဖြင့် Windows သီးသန့် သတ်မှတ်ထားသည်။ Assembly location နှင့် partial stream reads ကို modern runtime နှင့်လိုက်ဖက်အောင် ပြင်ထားသည်။ Core က build-date PE timestamp ကို ဆက်သုံးနေသောကြောင့် မူလ nondeterministic compilation ကို ထိန်းထားသည်။
- API `.nuspec` နှင့် `pack.cmd` ကို modern assets/dependency groups ပါအောင် ပြင်ထားသည်။ Complete package ထုတ်ရန် မူလ archiver build artifact လိုနေဆဲဖြစ်ပြီး package publish မလုပ်ထားပါ။

ပြန်စမ်းရန်:

```powershell
dotnet --version
dotnet build src/Tailviewer.Core/Tailviewer.Core.csproj --configuration Release
dotnet test src/Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj --framework net10.0
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj --framework net10.0
dotnet test src/Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj --framework net471
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj --framework net471
dotnet list src/Tailviewer.Core/Tailviewer.Core.csproj package --framework net10.0 --vulnerable --include-transitive
```

စစ်ဆေးပြီး: Api/Core Debug/Release builds သည် targets နှစ်မျိုးလုံးတွင် အောင်သည်။ Modern Core tests 6,641 passed / 5 existing skips; modern API tests 116 passed။ Legacy Core tests 6,640 passed / 5 existing skips; legacy API tests 115 passed။ WPF size presenter နှင့် ရွှေ့ထားသည့် color XML code ကို source files link လုပ်ထားသော temporary `net471` test project ဖြင့် 12 tests အောင်သည်။

ကျန်ရှိမှု: Full WPF application build သည် စက်တွင် .NET Framework 4.7.1 targeting pack မရှိသဖြင့် `MSB3644` ဖြစ်သည်။ Linux/macOS execution၊ complete package creation နှင့် Avalonia UI ကို မစမ်းရသေးပါ။ `net10.0` dependency graph တွင် WPF/Metrolib မပါဘဲ log4net 3.6.0 သုံးပြီး NuGet vulnerability scan တွင် known vulnerabilities မတွေ့ပါ။ Legacy `net45` target မှာ log4net 2.0.12 ၏ `GHSA-4f7c-pmjv-c25w` advisory ကျန်ရှိသည်။ Multi-target restore က advisory ကို targets အားလုံးအတွက် ပြနိုင်သည်။ Legacy/outer restore တွင် `NU1902` ကို warning အဖြစ်သာထားပြီး scan ကို မပိတ်ထားပါ။ Legacy host ကိုပြောင်းချိန် dependency နှင့် warning exception ကို အတူဖယ်ရမည်။

## စစ်ဆေးမှုအဆင့်နှင့် အထောက်အထား

- `src/**/packages.config` ရှိ package ID/version ၂၃ မျိုးလုံး၏ NuGet archive များကို download လုပ်ပြီး `lib/`, `ref/`, `runtimes/`, `build/` target များ စစ်ထားသည်။ Repository ထဲတွင် package files မသိမ်းထားပါ။
- `.csproj` references၊ application startup၊ custom log rendering၊ plugin loader/updater၊ repository service၊ installer၊ tests နှင့် API NuGet manifest ကို စစ်ထားသည်။
- ဤသည် metadata/source compatibility audit ဖြစ်သည်။ Restore/build/run ဖြင့် compatibility အတည်ပြုထားခြင်း မဟုတ်ပါ။ .NET Standard asset ရှိခြင်းသည် OS အားလုံးတွင် runtime အလုပ်လုပ်ကြောင်း အာမခံမဟုတ်ပါ။
- စက်တွင် .NET SDK `10.0.302` ရှိသည်။ Default SDK က `11.0.100` ဖြစ်နေသောကြောင့် implementation စချိန် `global.json` ဖြင့် .NET 10 SDK ကို pin လုပ်ရန်လိုသည်။
- Project root တွင် `rules.md`, `.specify/` မရှိပါ။ ဤဖိုင်သည် ပုံမှန် migration plan ဖြစ်ပြီး Spec Kit generated specification မဟုတ်ပါ။

## Dependency compatibility

“ဆက်သုံးနိုင်ဖွယ်” ဆိုသည်မှာ သက်ဆိုင်ရာ compatible asset တွေ့ထားခြင်းသာဖြစ်ပြီး release approval မဟုတ်ပါ။ Exact version ကို restore၊ transitive dependency၊ vulnerability နှင့် runtime စစ်ပြီးမှ အတည်ပြုရန်။

| Package / လက်ရှိ version | စစ်တွေ့သည့် asset | ဆုံးဖြတ်ချက်နှင့် လိုအပ်သည့်စစ်ဆေးမှု |
| --- | --- | --- |
| Extended.Wpf.Toolkit 4.0.1 | net40 | Avalonia UI တွင် မသုံးနိုင်။ အသုံးပြုထားသည့် control တစ်ခုချင်းကို Avalonia controls/styles ဖြင့် အစားထိုးမည်။ |
| Metrolib 0.3.0.162 | net45 သာ | အဓိက blocker။ UI resources၊ bootstrapper၊ helpers နှင့် Core ထဲက အသုံးပြုမှုများကို ခွဲပြီး လိုအပ်သည့်အပိုင်းသာ port/replace လုပ်မည်။ Library တစ်ခုလုံး မကူးပါ။ |
| Ookii.Dialogs.Wpf 1.1.0 | net45 သာ | Avalonia `StorageProvider` file/folder pickers သို့ပြောင်းမည်။ Cancellation နှင့် capability availability စစ်မည်။ |
| SharpRemote 0.6.184 | net45 သာ | Plugin repository RPC blocker။ Client/server/publish tool/protocol tests ကို တစ်စုတည်း စီမံရမည်။ Version upgrade တစ်ခုတည်းဖြင့် ဖြေရှင်းနိုင်ကြောင်း မအတည်ပြုနိုင်။ |
| log4net 2.0.12 | netstandard2.0 ပါ | Logging pattern ဆက်သုံးမည်။ စစ်ထားသည့် modern candidate 3.6.0 ကို သီးခြား upgrade PR ဖြင့် configuration၊ appenders၊ API ပြောင်းလဲမှု စမ်းမည်။ |
| MMQ 1.0.15 | netstandard2.0 ပါ | ချက်ချင်းမအစားထိုးပါ။ Single-instance နှင့် argument forwarding ကို Windows/Linux/macOS တွင် စမ်းမည်။ Named mapped-memory လုပ်ဆောင်ချက် မပံ့ပိုးသော OS ရှိလျှင် ထိုနေရာကိုသာ standard IPC ဖြင့် အစားထိုးမည်။ |
| System.Threading.Extensions 2.0.72 | netstandard2.0 ပါ | ပထမအဆင့်တွင် ဆက်သုံးနိုင်ဖွယ်။ `ITaskScheduler`, filesystem abstractions နှင့် deterministic test semantics ထိန်းမည်။ ဤ package သည် BCL package မဟုတ်ပါ။ |
| CommandLineParser 2.5.0 | netstandard2.0 ပါ | CLI ဆက်သုံးနိုင်ဖွယ်။ Modern candidate 2.9.1; pack/publish command arguments regression စမ်းမည်။ |
| PE 0.1.13 | netstandard2.0 ပါ | Plugin packer တွင် ဆက်သုံးနိုင်ဖွယ်။ .NET 10 assembly metadata၊ AnyCPU/x64 နှင့် invalid assembly များ စမ်းမည်။ |
| protobuf-net 2.4.0 | netstandard2.0 ပါ | Repository data/protocol compatibility စစ်ရမည်။ Serializer upgrade ကို data migration မစစ်ဘဲ မပေါင်းပါ။ |
| IsabelDb 0.6.21 | net45 သာ | Repository server blocker။ ထုတ်ဝေထားသည့် current package လည်း 0.6.21 ဖြစ်သည်။ Server port လုပ်ချိန် source-port feasibility သို့မဟုတ် data export/import ပါသော storage replacement ကို သီးခြားဆုံးဖြတ်မည်။ |
| EntityFramework 6.2.0 | net40/net45 သာ | Repository server အဟောင်းအတွက်သာ ယာယီထားမည်။ EF Core ကို drop-in replacement ဟု မယူဆပါ။ အသုံးမဝင်သော reference ဟုတ်မဟုတ် အရင်စစ်မည်။ |
| System.Data.SQLite 1.0.111.0 | Meta-package; lib မပါ | Dependency graph ဖြင့် စစ်ရန်။ NuGet URL version ကို `1.0.111` ဟု normalize လုပ်ရသည်; package ပျောက်နေခြင်း မဟုတ်ပါ။ |
| System.Data.SQLite.Core 1.0.111.0 | netstandard2.0; win-x86/x64, linux-x64, osx-x64 natives ပါ | Framework asset ရှိသော်လည်း native binary အဟောင်းများကို current OS/architecture တွင် စမ်းရန်။ ARM64 asset မတွေ့။ Server storage ဆုံးဖြတ်ချက်နှင့်တွဲကိုင်မည်။ |
| System.Data.SQLite.EF6 1.0.111.0 | net40–net46 | လက်ရှိ server stack နှင့်သာထားပြီး migration သီးခြားလုပ်မည်။ |
| System.Data.SQLite.Linq 1.0.111.0 | net20–net46 | လက်ရှိ server stack နှင့်သာထားပြီး migration သီးခြားလုပ်မည်။ |
| NUnit 3.12.0 | netstandard2.0 ပါ | Core tests ကို အရင် port မည်။ Avalonia headless NUnit integration နှင့်လိုက်ဖက်သော runner/framework versions ကို ထို့နောက် တူညီစွာရွေးမည်။ |
| Moq 4.7.127 | netstandard1.3 ပါ | ဆက်သုံးနိုင်ဖွယ်။ Current runtime/proxy behavior စမ်းပြီး လိုအပ်မှ upgrade မည်။ |
| Castle.Core 4.1.1 | netstandard1.3 ပါ | Moq နှင့်အတူစစ်မည်။ Independent version bump မလုပ်ပါ။ |
| FluentAssertions 5.6.0 | netstandard2.0 ပါ | Assertion style ထိန်းမည်။ Upgrade လိုအပ်လျှင် 7.x ကို ပထမ candidate ထားမည်။ 8+ တွင် commercial licensing စည်းကမ်း ကွာသည်။ |
| FluentAssertions.Extensions 1.0.18 | netstandard2.0 ပါ | လက်ရှိ assertion version နှင့် compatibility စမ်းမည်။ Usage မရှိမှသာ ဖြုတ်မည်။ |
| System.Runtime.CompilerServices.Unsafe 4.5.2 | netstandard2.0 ပါ | SDK-style restore ပြီးနောက် direct reference လိုအပ်မှု ပြန်စစ်မည်။ Transitive graph မစစ်ဘဲ မဖြုတ်ပါ။ |
| WpfUnit 0.1.0.14-alpha | net45 သာ; 0Harmony.dll ပါ | WPF tests အတွက်သာထားမည်။ Avalonia tests ကို `Avalonia.Headless.NUnit` ဖြင့်ပြောင်းပြီး runtime patching dependency ကို အသစ်မသယ်ပါ။ |

Avalonia candidate: audit အချိန် NuGet stable သည် **12.1.6** ဖြစ်ပြီး `net10.0` asset ပါသည်။ `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent` နှင့် headless test packages ကို တူညီသော compatible release family ဖြင့် pin လုပ်မည်။ Avalonia 11 အဟောင်း tutorial များ၏ API ကို တိုက်ရိုက်မယူပါ။

## Package list ထက် ကျော်လွန်သော blockers

1. `Tailviewer.Api/IEmptyReason.cs` က public `Geometry Icon` ကို WPF type ဖြင့် ထုတ်ထားသည်။ `SourceDoesNotExist`, `SourceCannotBeAccessed`, consumers နှင့် tests ပါပြောင်းရမည်။ Core contract တွင် UI icon အစား identifier ကိုထားပြီး Avalonia UI က icon ကို map လုပ်ရန်။ API version အသစ်နှင့် plugin rebuild လိုမည်။
2. Core `XmlReaderExtensions`, `XmlWriterExtensions`, settings များတွင် WPF `Color` နှင့် Metrolib helpers ရှိသည်။ လက်ရှိ XML အရောင်တန်ဖိုးများ၊ defaults နှင့် invalid-input handling ကို fixtures ဖြင့်ထိန်းပြီး UI conversion ကို UI ဘက်သို့ ရွှေ့မည်။
3. `Ui/LogView` တွင် `TextCanvas`, `TextLine`, `TextSegment`, formatters နှင့် column presenters က WPF drawing ကိုသုံးသည်။ Avalonia rendering သို့ port လုပ်ရမည်; ဖိုင်တစ်ခုလုံးကို UI collection ထဲ မတင်ရ။ လက်ရှိ streaming/index/cache strategy ထိန်းမည်။
4. `Tailviewer.Archiver` သည် desktop app က တိုက်ရိုက် reference လုပ်ထားသည်။ Plugin icon/description တွင် WPF imaging နှင့် `System.Drawing` ရှိသဖြင့် server-only ဟုယူဆပြီး ချန်ထား၍မရပါ။
5. Plugin assembly loader ၏ `Assembly.LoadFrom`, raw assembly loading နှင့် `AssemblyResolve` mapping ကို modern runtime တွင် ပြန်စမ်းမည်။ Dependency isolation လိုအပ်မှ `AssemblyLoadContext` သုံးမည်; ၎င်းသည် security sandbox မဟုတ်ပါ။
6. `System.Web`, `System.Web.Services`, WPF framework references နှင့် `System.Design` ကို project တစ်ခုချင်းစစ်ပြီး unused references ဖြုတ်မည်။ တကယ်သုံးသော API သာ အစားထိုးမည်။ Cross-platform image processing အတွက် Windows-only `System.Drawing` ကို မသယ်ပါ။
7. Installer/updater တွင် Registry၊ Windows P/Invoke၊ WPF/WinForms ရှိသည်။ Service project က `ServiceBase` သုံးသည်။ Desktop migration နှင့်အတူ cross-platform ဟု မသတ်မှတ်နိုင်ပါ။

## အဆင့်လိုက် migration နှင့် ပြီးစီးမှုသတ်မှတ်ချက်

### 1. လက်ရှိအလုပ်လုပ်ပုံကို baseline သိမ်းရန်

- Legacy build/test instructions နှင့် လက်ရှိ test results ကို မှတ်တမ်းတင်မည်; build မရပါက environment/package issue ကို ရှင်းလင်းဖော်ပြမည်။
- Small/100 MB/အကြမ်းဖျင်း 1 GB log၊ multiline၊ invalid encoding၊ long line၊ live append၊ truncate/rotate၊ timestamp merge fixtures ရွေးမည်။ Synthetic data ကိုသုံးပြီး user logs မကူးပါ။
- Load time၊ search/filter latency၊ peak memory၊ scroll responsiveness ကို same-machine baseline တိုင်းမည်။
- Settings၊ bookmarks၊ custom formats နှင့် plugin archives နမူနာများ သိမ်းမည်။ Original files ကိုမပြင်ပါ။
- ပြီးစီးမှု: acceptance fixture စာရင်း၊ repeatable baseline နှင့် release scope ရှိရမည်။ စမ်းသပ် target OS versions/architectures ကို သတ်မှတ်ရမည်။

### 2. SDK-style project နှင့် portable API/Core ပြုလုပ်ရန်

- Dependency အစီအစဉ် `Api → Core → Archiver → desktop` အတိုင်း SDK-style/PackageReference သို့ ပြောင်းမည်။ Library targets ကို `net10.0` သုံးမည်။ WPF legacy ကို staged work အတွင်းထိန်းရန်လိုမှ ယာယီ branch/project arrangement သုံးမည်; အမြဲ multi-target မလုပ်ပါ။
- WPF types နှင့် Metrolib dependency ကို Api/Core မှ ထုတ်မည်။ `INotifyPropertyChanged`, လက်ရှိ interfaces နှင့် test scheduler ကို ပြန်သုံးမည်။ MVVM framework အသစ် မထည့်ပါ။
- Public type ပြောင်းလဲမှုတိုင်း၏ implementations၊ callers၊ mocks၊ fixtures နှင့် XML persistence tests ကို အတူပြင်မည်။
- API SDK package `.nuspec`, `pack.cmd`, examples နှင့် Serilog plugin ကိုပါ update လုပ်မည်။ Manifest အဟောင်းက Core နှင့် `archive.exe` ကိုပါ pack လုပ်ပြီး dependencies version ကွာနေသည်။
- ပြီးစီးမှု: Api/Core restore/build/tests သည် Windows နှင့် ရွေးထားသည့် non-Windows CI တွင် WPF reference မပါဘဲ အောင်မြင်ရမည်။ `NU1701` framework fallback warning ကို compatibility အဖြစ် မလက်ခံပါ။

### 3. အခက်ဆုံး log rendering ကို prototype ဖြင့် အရင်သက်သေပြရန်

- Avalonia window တစ်ခုထဲတွင် real Core source မှ log ဖတ်ခြင်း၊ visible lines rendering၊ scroll၊ selection/copy၊ search highlight နှင့် live append ပြုလုပ်မည်။
- Long lines၊ Unicode/မြန်မာစာ၊ tabs၊ multiline၊ font fallback၊ DPI နှင့် keyboard navigation စမ်းမည်။
- Proposed acceptance gate: fixture output မှန်ကန်ပြီး same-machine load/search latency နှင့် peak memory သည် baseline ထက် 20% ကျော်မဆိုးရ။ ဤ 20% သည် စီမံကိန်းရည်မှန်းချက်သာဖြစ်ပြီး တိုင်းတာပြီးသားရလဒ် မဟုတ်ပါ။
- ပြီးစီးမှု: Windows performance gate နှင့် non-Windows smoke test အောင်မှ UI အားလုံးကို ဆက်ပြောင်းမည်။

### 4. Application shell နှင့် UI features ပြောင်းရန်

- Bootstrapper/App lifecycle၊ dispatcher၊ main window၊ source tree၊ menus၊ side panels၊ settings dialogs၊ themes/resources နှင့် controls ကို အစဉ်လိုက်ပြောင်းမည်။
- Existing view models ကိုပြန်သုံးပြီး WPF-specific commands/dispatcher/converters/styles ကိုသာ ပြင်မည်။
- File/folder dialogs ကို StorageProvider သုံးမည်; clipboard၊ drag/drop၊ focus၊ shortcuts၊ accessibility နှင့် cancellation behavior ထိန်းမည်။
- ပြီးစီးမှု: open/tail/search/filter/merge/bookmark/settings flows အားလုံးကို Windows တွင် end-to-end လုပ်နိုင်ရမည်။

### 5. Plugin loading နှင့် repository protocol ဖြေရှင်းရန်

- ပထမဦးစွာ built-in နှင့် local rebuilt plugin ကို load/parse/configure လုပ်နိုင်အောင် ပြောင်းမည်။ Old WPF UI plugin binaries အလိုအလျောက်အလုပ်လုပ်မည်ဟု မကတိပေးပါ။
- Plugin API version၊ minimum app version နှင့် incompatible-plugin message ကို ထုတ်ပေးမည်။ Missing dependency၊ invalid archive၊ type mismatch နှင့် dependency conflict tests ထည့်မည်။
- SharpRemote ကို port လုပ်နိုင်မှု စမ်းသပ်ဆုံးဖြတ်ချက်အား implementation gate ထားမည်။ Portable build မရလျှင် repository protocol အသစ်နှင့် client/server/publish tool ကို တစ်စုတည်းပြောင်းရန် စီစဉ်မည်။ Legacy server က လက်ရှိ protocol ဖြင့်သာ ဆက်အလုပ်လုပ်မည်; protocol အသစ်ကို တိုက်ရိုက်ဆက်မရပါ။
- Repository storage migration လိုအပ်ပါက IsabelDb မှ export၊ users/tokens/plugins/icons/descriptions import၊ record counts နှင့် file hashes စစ်ပြီး backup/rollback ပါသော cutover လုပ်မည်။ Database အဟောင်းကို in-place မပြောင်းပါ။
- ပြီးစီးမှု: local plugin parity နှင့် remote list/download/update/publish အားလုံး အောင်ရမည်။ Remote service ကို နောက်အဆင့်သို့ရွှေ့ထားပါက ထို build ကို full-parity release ဟု မသတ်မှတ်ပါ။

### 6. OS integration၊ settings migration နှင့် distribution

- MMQ single-instance/argument forwarding၊ app activation၊ paths/permissions၊ case sensitivity၊ line endings၊ network files နှင့် rotation behavior ကို target OS တစ်ခုချင်းစမ်းမည်။ MMQ portable မရလျှင် named pipes/local IPC ဖြင့် လိုအပ်သည့်လုပ်ဆောင်ချက်ကိုသာ အစားထိုးမည်။
- Existing settings format ကို ဖြစ်နိုင်သမျှထိန်းမည်။ Schema ပြောင်းမှ versioning၊ backup၊ atomic save နှင့် failure fallback ပါမည်။ Legacy app profile ကို အလိုအလျောက် overwrite မလုပ်ပါ။
- ပထမစမ်းသပ်ဖြန့်ဝေမှုကို RID-specific portable publish ဖြင့်စမည်။ Existing Windows installer/update behavior ကို နောက်တစ်ဆင့်တွင် restore လုပ်မည်; registry/PInvoke ကို OS guard မပါဘဲ မသုံးပါ။
- Plugins က dynamic loading သုံးသောကြောင့် ပထမ release တွင် trimming/Native AOT မဖွင့်ပါ။ App signing နှင့် native assets ပါဝင်မှု စစ်မည်။
- ပြီးစီးမှု: clean machine installation/startup/update/uninstall စမ်းသပ်ပြီး settings/plugins မပျောက်ရ။ Linux/macOS ကို သက်ဆိုင်ရာ OS ပေါ်တွင် စမ်းပြီးမှ support လို့ကြေညာမည်။

### 7. Tests၊ CI နှင့် release gate

- Pure Core/Api tests ကို UI dependency မထည့်ဘဲ ဆက်သုံးမည်။ WpfUnit tests ကို headless Avalonia layout/input tests သို့ port မည်။ Real rendering၊ native dialogs၊ clipboard နှင့် accessibility ကို actual desktop တွင်ပါ စမ်းမည်။
- Legacy project lists/output paths၊ scripts၊ signing၊ API pack နှင့် examples build ကို update မည်။ Playground projects ကို production dependency မဟုတ်လျှင် ဤ migration မှ ချန်ထားမည်။
- .NET 10 restore/build/test/publish ကို target OS CI matrix တွင်လုပ်မည်။ Direct/transitive packages နှင့် known vulnerabilities ကို restore ပြီး စစ်မည်။
- ပြီးစီးမှု: required features၊ settings/data compatibility၊ plugin migration၊ performance gate နှင့် clean-machine smoke tests အားလုံး အောင်ပြီး rollback artifact ရှိမှ stable release ထုတ်မည်။

## စတင်ရမည့် implementation အစု

ပထမ PR: SDK pin + baseline မှတ်တမ်း + Api/Core portability blockers ဖြေရှင်းမှုကို အသေးဆုံး buildable အစုများခွဲလုပ်ရန်။ ထို့နောက် rendering prototype ကို အရင်လုပ်ရန်။ UI အားလုံးပြန်ရေးခြင်း၊ repository database အစားထိုးခြင်းနှင့် installer rewrite ကို PR တစ်ခုတည်း မပေါင်းပါ။

## Official references

- [Avalonia package / verified 12.1.6](https://www.nuget.org/packages/Avalonia/12.1.6)
- [Avalonia 12 .NET support and breaking changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes)
- [WPF migration differences](https://docs.avaloniaui.net/docs/migration/wpf)
- [Supported OS versions and tiers](https://docs.avaloniaui.net/docs/supported-platforms)
- [StorageProvider](https://docs.avaloniaui.net/docs/services/storage/storage-provider)
- [Headless NUnit testing](https://docs.avaloniaui.net/docs/testing/headless-nunit)
- [NuGet package content API used for the asset audit](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource)
- [MMQ 1.0.15](https://www.nuget.org/packages/MMQ/1.0.15)
- [System.Threading.Extensions](https://www.nuget.org/packages/System.Threading.Extensions)
- [log4net candidate](https://www.nuget.org/packages/log4net/3.6.0)
- [CommandLineParser candidate](https://www.nuget.org/packages/CommandLineParser/2.9.1)
- [IsabelDb](https://www.nuget.org/packages/IsabelDb)
- [FluentAssertions version/license guidance](https://fluentassertions.com/releases/)

မစစ်ရသေးသည်: full solution restore/build/tests၊ runtime compatibility၊ complete transitive/vulnerability audit နှင့် real OS benchmarks။
အဓိက risk: custom rendering performance၊ WPF plugin/API breakage နှင့် SharpRemote/IsabelDb server migration ဖြစ်သည်။
