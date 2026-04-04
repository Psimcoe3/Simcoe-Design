# Branch Reset Instructions

## Summary

The `main` branch has been reset to match the `USE` branch structure as requested.

## What was done

The local `main` branch has been reset to commit `2f1a4c3` (the HEAD of the `USE` branch) using:

```bash
git checkout main
git reset --hard 2f1a4c3
```

## Current Status

- **main branch (local)**: Now at commit `2f1a4c3 "bb"` - matches USE branch exactly
- **USE branch**: At commit `2f1a4c3 "bb"`
- **Remote main branch**: Still at commit `745bde9` (needs to be updated)

## Verification

Both branches are now identical - verified with:
```bash
git diff main USE
# (returns no output - branches are identical)
```

## To Complete This Change

Since this PR branch cannot force-push to the `main` branch, the repository owner will need to:

1. **Option A: Force push** (if comfortable with rewriting history):
   ```bash
   git checkout main
   git reset --hard 2f1a4c3
   git push origin main --force
   ```

2. **Option B: Merge USE into main** (preserves history):
   ```bash
   git checkout main
   git merge USE --strategy=ours
   git push origin main
   ```

3. **Option C: Via GitHub** (safest):
   - Open a Pull Request from `USE` to `main`
   - Merge it to bring main up to date with USE

## Commits Added to Main

The reset brings 68 new commits from USE into main, including:
- PDF/Image underlay rendering
- Markup and measurement engine
- 2D plan canvas features
- Interactive conduit bending improvements
- Code cleanup and optimizations
- Multiple bug fixes and improvements

All commits from `745bde9` (old main) to `2f1a4c3` (current USE) are now in the local main branch.
