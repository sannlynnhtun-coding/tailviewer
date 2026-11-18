# Avalonia log-viewer prototype

ဤ prototype သည် migrated Core ၏ `StreamingTextLogSourceFactory` ကိုသုံးသော .NET 10 / Avalonia 12.1.6 desktop application ဖြစ်သည်။

Repository root မှ run ရန်:

```powershell
dotnet run --project src/Tailviewer.Avalonia
```

ဖိုင်တန်းဖွင့်ရန်:

```powershell
dotnet run --project src/Tailviewer.Avalonia -- "C:\logs\application.log"
```

Build နှင့် tests:

```powershell
dotnet build Tailviewer.Avalonia.slnx
dotnet test Tailviewer.Avalonia.slnx
```

အသုံးပြုပုံ:

- **Open file… / Ctrl/Cmd+O** ဖြင့် local log file တစ်ဖိုင်ဖွင့်ပါ။ Cancel လုပ်လျှင် လက်ရှိဖိုင်ကို ဆက်ထားသည်။ ဖိုင်ဖွင့်မရလျှင် error ပြပြီး လက်ရှိဖိုင်ကို မပိတ်ပါ။
- Vertical/horizontal scrollbars၊ mouse wheel နှင့် keyboard navigation သုံးနိုင်သည်။
- လိုင်းကို click လုပ်ရွေးပါ။ Shift ဖြင့် range၊ Ctrl/Cmd ဖြင့် လိုင်းများရွေးပြီး **Copy selected / Ctrl/Cmd+C** နှိပ်ပါ။ Clipboard တွင် ဖိုင်အစီအစဉ်အတိုင်း မူလစာသားသာပါပြီး display line numbers မပါပါ။
- **Follow tail** သည် append ဖြစ်သောလိုင်းများဆီ အလိုအလျောက်ရွှေ့သည်။ Scroll/selection/keyboard navigation လုပ်လျှင် follow ရပ်သော်လည်း Core က ဆက်ဖတ်နေသည်။ Follow tail ကို ပြန်ဖွင့်လျှင် နောက်ဆုံးလိုင်းဆီ ရွှေ့သည်။
- UTF-8 ကို default သုံးပြီး UTF-8/UTF-16 စသည့် BOM ကို platform StreamReader ဖြင့် detect လုပ်သည်။

Core က ဖိုင်ကို index လုပ်ပြီး UI က လိုင်းတစ်လိုင်းလျှင် integer index တစ်ခုသာ သိမ်းသည်။ Virtualized ListBox နှင့် စာသား cache အများဆုံး 8 × 128 လိုင်းကိုသုံးသည်။ Column reads ကို background threads တွင်လုပ်ပြီး UI timer က 100 ms တစ်ကြိမ် update ယူသည်။ ဖိုင်တစ်ခုလုံးကို string တစ်ခုအဖြစ် မတင်ပါ။ Selection/copy သည် **လိုင်းအလိုက်** ဖြစ်ပြီး character အပိုင်းတစ်ခုချင်း ရွေးခြင်း မပါပါ။ ကြီးမားသော selection ကို copy လုပ်လျှင် ထိုစာသားအတွက် memory လိုသည်။

စစ်ထားသည်: real Core + Avalonia headless integration tests ဖြင့် Unicode/BOM၊ whitespace/copy ordering၊ empty file၊ partial-line append၊ selection/clipboard၊ truncation၊ failed-open recovery၊ close during read နှင့် 20,000-line viewport virtualization ကို စမ်းထားသည်။

မပြီးသေးသည်: Windows native file picker/rendering ကို desktop capture timeout ကြောင့် မျက်မြင်အတည်မပြုနိုင်သေးပါ။ Linux/macOS execution နှင့် 100 MB/1 GB performance comparisons မစမ်းရသေးပါ။ Legacy Core ၏ same-size file replacement/rotation handling ကို ဤ prototype က မဖြေရှင်းထားပါ။ Search၊ parsed columns၊ plugins နှင့် installer သည် နောက်အဆင့်များဖြစ်သည်။

Multi-target Core restore တွင် legacy log4net 2.0.12 advisory warning ပြနိုင်သည်။ ဤ executable က `net10.0` graph နှင့် log4net 3.6.0 ကိုသာ သုံးသည်။
