# abacus-client

Unofficial C# client for the [Abacus](https://www.abacus.ch) ERP REST API (OData 4.0), generated with NSwag from the OpenAPI document Abacus publishes on the [API Hub](https://apihub.abacus.ch). Not affiliated with Abacus Research AG.

Status: research done, no code yet. See [docs/research.md](docs/research.md) for the API, authentication and the library design, and [docs/TestingGuide.md](docs/TestingGuide.md) for test conventions.

## OpenAPI document

The full OpenAPI document (~26 MB) is not in the repository. To regenerate from a new Abacus release, download it manually: [API Hub](https://apihub.abacus.ch) → REST APIs → OData Endpoints → the release (e.g. V2026) → **Download YAML-File** (it is JSON despite the name). Save it as `src/AbacusApi.Generator/OpenApi/abacus-<release>.json` (e.g. `abacus-2026.201.json`; the release is in the document's `info.title`). The download links are per browser session and can't be scripted.

## Planned usage

```json
"Abacus": {
  "BaseUrl": "https://abacus.example.ch",
  "Mandant": 7777,
  "ClientId": "…"
}
```

```csharp
services.AddAbacusApi(configuration);
```
