# ShareX code incorporated into Clipsa

- Upstream: https://github.com/ShareX/ShareX
- Release: v22.4.0 (2026-11-08)
- Commit: 08bfff15e748a945944e609aca7faffbe888c7af
- Copyright (c) 2007–2026 ShareX Team.
- Modified by Francisco Serrano Jiménez, 2026-11-16.

The upstream file notices offer GPL version 2 or any later version. Clipsa distributes this combined application under GPL version 3; the original notices are retained. The full GPL version 3 text accompanies the application in LICENSE and licenses/ShareX-GPL-3.0.txt.

Adapted files:

| Clipsa file | Upstream file / portions | Changes |
| --- | --- | --- |
| Clipsa/Capture/ShareXScrollComposer.cs | ShareX.ScreenCaptureLib/ScrollingCaptureManager.cs / CombineImages, CompareRows | System.Drawing pixel buffers instead of Skia; cancellation; bounded image dimensions; explicit opt-in for estimated joins; no uploader or capture UI dependencies. |
| Clipsa/Capture/DesktopCapture.cs | ShareX.HelpersLib/Helpers/WindowsImageInterop.cs / Capture, DrawNative, SetOpaque | System.Drawing bitmap output; desktop-only capture without cursor; physical bounds checks and deterministic GDI resource cleanup. |
| Clipsa/ScrollDriver.cs | ShareX.ScreenCaptureLib/ScrollingCaptureManager.cs / scroll methods; ShareX.HelpersLib/Input/InputManager.cs / keyboard and wheel input | Only four scrolling methods; original target/process and modifier checks; targeted first-gesture replay for held capture. |

Other capture orchestration, bounds, frame guide and WPF interface are maintained by Clipsa. This is an adapted engine, not an unmodified copy of ShareX or a product endorsed by its team. Original .NET and Tendedero notices are retained separately. Prior MIT versions of Clipsa remain available under their original terms; licenses/Clipsa-previous-MIT.txt preserves that notice.
