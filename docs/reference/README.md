Reference frames: ORIGINAL launcher (origin/main aa6b411, win-x64 self-contained) run under Wine 11 (scratch prefix) with
only the Windows-only startup bits (Minecraft/PasswordVault WinRT calls, self-update) patched out, captured with
`magick x:<window id>` while `tools/interact.py` drove hover/press/click through XTEST. `../linux/` has the same states from the Linux build.
Pixel diffs: `../pixel-diff.txt` (AE pixels, fuzz 3%, of 400000).
