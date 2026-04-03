# apix

A terminal-native HTTP client for developers managing multiple microservices. Import OpenAPI schemas, execute requests, and track history — all from the CLI.

## Installation

### Binary (no runtime needed)

**Linux:**
```bash
curl -Lo apix https://github.com/nikola-golijanin/apix/releases/latest/download/apix-linux-x64
chmod +x apix && sudo mv apix /usr/local/bin/
```

**macOS (Apple Silicon):**
```bash
curl -Lo apix https://github.com/nikola-golijanin/apix/releases/latest/download/apix-osx-arm64
chmod +x apix && sudo mv apix /usr/local/bin/
```

**macOS (Intel):**
```bash
curl -Lo apix https://github.com/nikola-golijanin/apix/releases/latest/download/apix-osx-x64
chmod +x apix && sudo mv apix /usr/local/bin/
```

**Windows:**
Download `apix-win-x64.exe` from the [Releases page](https://github.com/nikola-golijanin/apix/releases/latest), rename it to `apix.exe`, and add it to your `PATH`.

### dotnet global tool (requires .NET 10)

Download `apix.{version}.nupkg` from the [Releases page](https://github.com/nikola-golijanin/apix/releases/latest), then:

```bash
# First-time install
dotnet tool install -g apix --add-source ./

# Update existing install
dotnet tool update -g apix --add-source ./
```

## Development

```bash
cd src/apix
dotnet restore
dotnet build
dotnet run -- <command> [args]
```
