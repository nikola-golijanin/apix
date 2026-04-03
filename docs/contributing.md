# Contributing to apix

## 1. Development setup

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
git clone https://github.com/<your-fork>/apix.git
cd apix/src/apix
dotnet restore
dotnet build
dotnet run -- --help
dotnet run -- import --help
```

### Installing as a local global tool

For end-to-end testing as the `apix` binary (instead of `dotnet run --`):

```bash
cd src/apix

# First-time install
dotnet pack -c Release
dotnet tool install --global apix --add-source ./bin/Release

# After subsequent code changes
dotnet pack -c Release
dotnet tool update --global apix --add-source ./bin/Release
```

Then test with `apix <command>` from any directory.

---

## 2. Branching

- All work happens on feature branches cut from `master`.
- Branch naming convention:
  - `feature/<short-description>` — new functionality
  - `fix/<short-description>` — bug fixes
- `master` is protected — direct pushes are blocked; all changes must go through a PR.

---

## 3. Opening a pull request

1. Push your branch to `origin` and open a PR targeting `master`.
2. CI (the `build` job in `.github/workflows/ci.yml`) runs automatically on every push to the PR branch — **it must pass before merging**.
3. PR title should be a short imperative sentence describing the change (e.g. `Add replay command`).
4. Keep PRs focused — one logical change per PR.

---

## 4. Merging

- Squash-merge or regular merge — maintainer's choice.
- Delete the branch after merge.

---

## 5. Versioning

apix uses **semantic versioning**: `MAJOR.MINOR.PATCH`

- The `<Version>` field in `apix.csproj` is a dev-only fallback (`1.0.0`).
- CI always overrides it from the git tag at release time — no manual version-bump commits are needed.
- **The tag is the release.**

---

## 6. Cutting a release

### When to tag

| Change type | Version bump | Example |
|---|---|---|
| New user-visible feature | MINOR | `v1.0.0` → `v1.1.0` |
| Bug fix or non-breaking improvement | PATCH | `v1.1.0` → `v1.1.1` |
| Breaking change | MAJOR | `v1.1.0` → `v2.0.0` |

### How to tag

```bash
git checkout master
git pull origin master
git tag v1.1.0
git push origin v1.1.0
```

Pushing the tag triggers `.github/workflows/release.yml`, which:

1. Cross-compiles self-contained single-file binaries for win-x64, linux-x64, osx-x64, and osx-arm64
2. Packs the dotnet global tool `.nupkg`
3. Creates a GitHub Release with all 5 artifacts and auto-generated release notes

> **Never tag a commit that hasn't passed CI.**

---

## 7. Artifacts produced per release

| File | Description |
|------|-------------|
| `apix-win-x64.exe` | Windows self-contained binary |
| `apix-linux-x64` | Linux self-contained binary |
| `apix-osx-x64` | macOS Intel self-contained binary |
| `apix-osx-arm64` | macOS Apple Silicon self-contained binary |
| `apix.{version}.nupkg` | dotnet global tool package |
