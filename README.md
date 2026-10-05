# abacus-client

[![CI](https://github.com/garcipat/abacus-client/actions/workflows/ci.yml/badge.svg)](https://github.com/garcipat/abacus-client/actions/workflows/ci.yml)

Unofficial C# client for the [Abacus](https://www.abacus.ch) ERP REST API (OData 4.0), generated with NSwag from the OpenAPI document Abacus publishes on the [API Hub](https://apihub.abacus.ch). Not affiliated with Abacus Research AG.

Status: generator, generated client, DI registration, client-credentials and interactive browser login done; not yet tried against a real Abacus server (opt-in integration tests are ready, see [docs/TestingGuide.md](docs/TestingGuide.md#integration-tests)). See [docs/research.md](docs/research.md) for the API, the available integration options, authentication and the library design, and [docs/TestingGuide.md](docs/TestingGuide.md) for test conventions. Changes per version: [CHANGELOG.md](CHANGELOG.md).

## OpenAPI document

The full OpenAPI document (~26 MB) is not in the repository. To regenerate from a new Abacus release, download it manually: [API Hub](https://apihub.abacus.ch) → REST APIs → OData Endpoints → the release (e.g. V2026) → **Download YAML-File** (it is JSON despite the name). Save it as `src/AbacusApi.Generator/OpenApi/abacus-<release>.json` (e.g. `abacus-2026.201.json`; the release is in the document's `info.title`). The download links are per browser session and can't be scripted.

## Generating the client

The client is generated automatically when the generator project builds, which also happens when you build the client, the tests or the solution:

```bash
dotnet build src/AbacusApi.Generator
```

It trims the full download (if present) into the committed `OpenApi/abacus-2026.201.trimmed.json` and generates `src/AbacusApi.Client/V2026/AbacusApiV2026.cs`. Without the full download it regenerates from the trimmed document. It only runs when the generator, its csproj or an OpenAPI document changed, and not in Release builds.

The settings (Abacus release, entity sets, namespace, class name, output path, on/off) are MSBuild properties in [`AbacusApi.Generator.csproj`](src/AbacusApi.Generator/AbacusApi.Generator.csproj). To skip generation once: `dotnet build -p:AbacusGenerateOnBuild=false`. Details: [docs/research.md → Generator](docs/research.md#generator).

## Usage

`appsettings.json`:

```json
"Abacus": {
  "BaseUrl": "https://abacus.example.ch",
  "Mandant": 7777,
  "ClientId": "…",
  "Scopes": [ "abacus.entity.projectbooking.readwrite", "abacus.entity.project.read" ]
}
```

Pick how to log in:

```csharp
// Service user (OAuth client credentials). The secret goes into user secrets or Abacus__ClientSecret, never appsettings.json.
services.AddAbacusApi(configuration);

// Log in as yourself in the browser (desktop/local apps). Needs "RedirectUri": "http://localhost:53682/callback" in the
// section, registered for a user-dependent service user in Q910. The refresh token is kept DPAPI-encrypted on Windows.
services.AddAbacusApi(configuration, api => api
    .UseInteractiveBrowserLogin());

// Or your own IAbacusTokenProvider:
services.AddAbacusApi(configuration, api => api
    .UseTokenProvider<MyTokenProvider>());
```

```csharp
public class Bookings(IAbacusApi abacus)
{
    public Task<ProjectBookingCollection> ForEmployeeAsync(long employeeId) =>
        abacus.ListProjectBookingsAsync(filter: $"EmployeeId eq {employeeId}", top: 100);
}
```

`IAbacusApi` and the models are in `Pgarcia.AbacusApi.Client.V2026`. Missing or invalid options fail at startup.

## Installing

The package `Pgarcia.AbacusApi.Client` is published to **GitHub Packages**. The package is public, but GitHub Packages still requires a token to install NuGet packages: add the feed once, with a GitHub personal access token (classic) that has the `read:packages` scope:

```bash
dotnet nuget add source https://nuget.pkg.github.com/garcipat/index.json --name garcipat --username <github-user> --password <token>
```

```bash
dotnet add package Pgarcia.AbacusApi.Client
```

## Releasing

Versions follow [Semantic Versioning](https://semver.org/), starting at 0.1.0; [CHANGELOG.md](CHANGELOG.md) uses the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.

1. While working, add entries under `## [Unreleased]` in `CHANGELOG.md` (`### Added`, `### Changed`, `### Fixed`, `### Removed`).
2. To release, rename `## [Unreleased]` to `## [x.y.z] - YYYY-MM-DD`, add a new empty `## [Unreleased]` above it, update the compare links at the bottom, and merge that through a pull request.
3. Tag the merged commit on `main` and push the tag:

   ```bash
   git tag v0.1.0
   ```

   ```bash
   git push origin v0.1.0
   ```

The [release workflow](.github/workflows/release.yml) then takes the version from the tag, checks that `CHANGELOG.md` has that section, builds, tests and packs with that version, publishes to GitHub Packages and creates a GitHub Release with the changelog section as notes. A tag like `v0.2.0-beta.1` gives a pre-release. [CI](.github/workflows/ci.yml) runs on every push to `main` and every pull request, and fails if the generated client isn't up to date.

## Contributing

`main` is protected: every change, including the maintainer's, goes through a pull request, and the CI check (`build`) has to pass before merging. Force-pushes and deleting `main` are blocked. Fork the repository (or create a branch if you have access), open a pull request, and add a line under `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md) if the change matters to users.

## License

[MIT](LICENSE). Abacus is a product of Abacus Research AG; this project is not affiliated with or endorsed by them.
