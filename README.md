# abacus-client

Unofficial C# client for the [Abacus](https://www.abacus.ch) ERP REST API (OData 4.0), generated with NSwag from the OpenAPI document Abacus publishes on the [API Hub](https://apihub.abacus.ch). Not affiliated with Abacus Research AG.

Status: research done, no code yet. See [docs/research.md](docs/research.md) for the API, authentication and the library design.

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
