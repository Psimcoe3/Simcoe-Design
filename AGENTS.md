# Repository Agent Instructions

## Line Endings (Critical)
- Keep these file types in `CRLF`: `.cs`, `.xaml`, `.csproj`, `.props`, `.targets`, `.sln`, `.slnx`, `.config`, `.ps1`, `.bat`, `.cmd`.
- Keep `.sh` files in `LF`.
- After every edit, run:
  - `pwsh -File scripts/Normalize-LineEndings.ps1`
- Never leave mixed line endings in modified files.
