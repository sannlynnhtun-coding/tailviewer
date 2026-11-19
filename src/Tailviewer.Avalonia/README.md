# Tailviewer desktop app

.NET 10 / Avalonia 12.1.6 နှင့် migrated Core ကိုသုံးသော Windows x64 desktop app ဖြစ်သည်။ Legacy Tailviewer feature အားလုံး ပြည့်စုံသေးခြင်းမရှိပါ။ အကောင်အထည်ဖော်ပြီးသောအပိုင်းနှင့် ကျန်ရှိသည့်အလုပ်များကို [feature inventory](../../specs/001-full-project-migration/feature-inventory.md) နှင့် [tasks](../../specs/001-full-project-migration/tasks.md) တွင် ကြည့်နိုင်သည်။

Repository root မှ run ရန်:

```powershell
dotnet run --project src/Tailviewer.Avalonia
```

ဖိုင်များကို command line မှ ဖွင့်ရန်:

```powershell
dotnet run --project src/Tailviewer.Avalonia -- "C:\logs\application.log" "C:\logs\service.log"
```

Build နှင့် modern tests:

```powershell
dotnet build Tailviewer.Avalonia.slnx -c Release
dotnet test Tailviewer.Avalonia.slnx -c Release
```

အသုံးပြုပုံ:

- **Open file… / Ctrl+O** ဖြင့် ဖိုင်များ၊ **Open folder…** ဖြင့် folder ဖွင့်နိုင်သည်။ Local file/folder drop နှင့် existing-instance handoff ပါသည်။ ဖိုင်ဖွင့်မရလျှင် identity/error ကို ထိန်းပြီး လက်ရှိဖတ်နေသော rows ကို ဆက်ထားသည်။
- Source tree မှ ဖိုင်ပြောင်းခြင်း၊ group ဖွဲ့ခြင်း/အမည်ပြောင်းခြင်း၊ member include/exclude နှင့် close/all လုပ်နိုင်သည်။ Folder options တွင် semicolon ဖြင့် ခွဲထားသော wildcard patterns နှင့် recursive discovery ကို ပြင်နိုင်သည်။ ရွေးထားသော folder/group သည် ပါဝင်သည့် logs များကို timestamp အလိုက် ပေါင်းပြသည်။
- Vertical/horizontal scroll၊ mouse wheel နှင့် keyboard navigation သုံးနိုင်သည်။ **Ctrl+G** ဖြင့် line number၊ **Ctrl+T** ဖြင့် configured source ကို ရှာရွေးပါ။ **Ctrl+Tab / Ctrl+Shift+Tab** ဖြင့် source ပြောင်းနိုင်သည်။
- Click၊ Shift နှင့် Ctrl ဖြင့် လိုင်းများရွေးပါ။ **Alt+drag** ဖြင့် character range ရွေးနိုင်သည်။ **Copy selected / Ctrl+C** သည် ပြထားသောအစီအစဉ်အတိုင်း မူလစာသားကို copy လုပ်ပြီး display line number၊ delta/elapsed-time prefix မပါပါ။ ကြီးမားသော selection ကို copy လုပ်လျှင် ထိုစာသားအတွက် memory လိုသည်။
- **Follow tail** သည် append ဖြစ်သောလိုင်းများဆီ ရွှေ့သည်။ Scroll/selection/navigation လုပ်လျှင် follow ရပ်သော်လည်း background reader က ဆက်ဖတ်သည်။ Follow ကို ပြန်ဖွင့်လျှင် နောက်ဆုံးလိုင်းဆီ ရွှေ့သည်။
- **Text encoding** မှ Auto detect သို့မဟုတ် explicit encoding ရွေးနိုင်သည်။ Auto သည် BOM ကို detect လုပ်ပြီး BOM မရှိလျှင် UTF-8 သုံးသည်။ Accepted encoding change သည် readers/cache ကို ပြန်ဖန်တီးသည်။
- Source context menu မှ line numbers၊ delta/elapsed times၊ Single Line mode နှင့် containing-folder actions သုံးနိုင်သည်။

Configured/discovered files များသည် ပထမဆုံးရွေးဖတ်ခြင်းမပြုမီ background တွင် index/tail လုပ်သည်။ Visible text cache ကို အများဆုံး 8 × 128 လိုင်း ထိန်းထားပြီး UI တွင် line တစ်လိုင်းလျှင် integer index သိမ်းသည်။ ပုံမှန် preview အတွက် ဖိုင်တစ်ခုလုံးကို string တစ်ခုအဖြစ် မတင်ပါ။

Core က metadata နှင့် ဖတ်ပြီးသား content hash ကို စစ်၍ stable same-size/middle-only rewrites၊ truncate/regrow၊ rotation နှင့် Auto mode BOM encoding replacement ကို detect လုပ်သည်။ Replacement သည် revision/cache/selection ကို invalidate လုပ်ပြီး ပုံမှန် append တွင် selection ကို ထိန်းသည်။ Writer က ဆက်တိုက်ပြောင်းနေသော ဖိုင်အတွက် atomic filesystem snapshot အာမခံချက် မရှိပါ။

စစ်ဆေးမှုအထောက်အထားနှင့် ကန့်သတ်ချက်များ:

- [Source lifecycle tests](../../specs/001-full-project-migration/source-lifecycle-tests.md): file/folder/group၊ background readers၊ selection/copy၊ missing/locked-file recovery နှင့် native clipboard checks။ Native desktop focus/clipboard interference ကြောင့် picker/copy failure စစ်ဆေးမှု ကျန်နေသည်။
- [Recovery tests](../../specs/001-full-project-migration/recovery-tests.md): replacement၊ encoding နှင့် streaming recovery။ Performance အထောက်အထားများတွင် သတ်မှတ်ထားသော hardware/workload/cache အခြေအနေများအတွက်သာ အကျုံးဝင်သည်။ Full acceptance matrix မပြီးသေးပါ။
- လက်ရှိ app ၏ source/view settings သည် session အတွင်းသာ ထိန်းထားသည်။ Shipping profile save/restore/import UI၊ plugin lifecycle နှင့် updates migration တို့ ကျန်နေသည်။ Linux/macOS သည် နောက် milestone ဖြစ်သည်။

Multi-target restore တွင် legacy log4net 2.0.12 ၏ NU1902 advisory ပြနိုင်သည်။ Modern runtime သည် log4net 3.6.0 ကို resolve လုပ်သည်။
