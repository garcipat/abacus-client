# abacus-client

Unofficial C# client for the [Abacus](https://www.abacus.ch) ERP REST API (OData 4.0), generated with NSwag from the OpenAPI document Abacus publishes on the [API Hub](https://apihub.abacus.ch). Not affiliated with Abacus Research AG.

Status: generator, generated client, DI registration and client-credentials auth done; not yet tried against a real Abacus server. See [docs/research.md](docs/research.md) for the API, the available integration options, authentication and the library design, and [docs/TestingGuide.md](docs/TestingGuide.md) for test conventions.

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

The client secret goes into user secrets or the `Abacus__ClientSecret` environment variable, never into `appsettings.json`.

```csharp
services.AddAbacusApi(configuration);                      // OAuth client credentials (service user)

// or with your own token provider, e.g. for the user-dependent login:
services.AddAbacusApi(configuration).AddTokenProvider<MyTokenProvider>();
```

```csharp
public class Bookings(IAbacusApi abacus)
{
    public Task<ProjectBookingCollection> ForEmployeeAsync(long employeeId) =>
        abacus.ListProjectBookingsAsync(filter: $"EmployeeId eq {employeeId}", top: 100);
}
```

`IAbacusApi` and the models are in `Garcipat.AbacusApi.Client.V2026`. Missing or invalid options fail at startup.
