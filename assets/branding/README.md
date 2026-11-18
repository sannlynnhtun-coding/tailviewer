# Tailviewer branding

The mark combines three log strokes with a hooked tail inside a document silhouette. It follows the existing blue/white app palette (`#2196F3`, `#0069C0`, white) and is designed for the Avalonia Fluent UI.

`tailviewer-master.png` is the transparent 1254 × 1254 master, created with the built-in image generation tool. Every shipped icon is a size export of this master; the master is not reconstructed separately for each platform or theme.

| Asset | Purpose |
| --- | --- |
| `tailviewer-master.png` | Transparent source master |
| `icon-16.png` through `icon-1024.png` | Transparent PNG exports: 16, 20, 24, 32, 40, 48, 64, 128, 180, 192, 256, 512, 1024 px |
| `tailviewer.ico` | Windows executable/icon bundle: 16, 20, 24, 32, 40, 48, 64, 128, 256 px; also suitable as a favicon |
| `../../src/Tailviewer/Resources/Tailviewer.png` | 256 px compatibility copy for WPF, installer windows and the API NuGet package |
| `../../src/Tailviewer/Resources/Tailviewer.ico` | Compatibility copy for WPF and installer executables, also used by the Avalonia executable |
| `../../src/Tailviewer.Avalonia/Assets/Tailviewer.png` | 64 px embedded Avalonia header/window icon |

Regenerate the mechanical size exports on Windows using the existing .NET drawing library:

```powershell
./scripts/export-branding.ps1
```

The export script preserves transparency and checks square dimensions and clear corners. There is one color design for both light and dark themes. Keep its transparent padding and white interior strokes; do not add a white background to the master.

This repository is a desktop app. It has no web header, splash screen, web manifest, favicon reference, or social-sharing metadata to replace. The PNG/ICO assets are available for those uses if a website is introduced. The README logo is 96 px wide and fits narrow/mobile document layouts.

The original project attribution and MIT license remain in [LICENSE](../../LICENSE). The new branding is distributed with this repository under the same license. Historical screenshots and unrelated plugin test images are retained; they are not current logo assets.

## Generation brief

Built-in image generation, use case `logo-brand`. Initial prompt: a transparent square icon for Tailviewer, matching the existing blue/white palette, with a rounded/clipped-corner log document and three white strokes, the last flowing into a hooked tail; no text, mockup, eye, magnifier or background tile; recognizable at 16 px and in light/dark themes. The selected result was refined with the same silhouette and a cleaner flat-color rendering request. Size variants are exported mechanically from the selected transparent master.
