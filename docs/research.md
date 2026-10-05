# Abacus API — Research

> Background for this client library: how the Abacus REST API works, how to authenticate, what the published OpenAPI document looks like, and how the library is designed. Researched 2026-10-05 against the Abacus API Hub (V2026, patch delivered 15.08.2026). The first consumer is [Tally](https://github.com/garcipat/tally) (spec S-003, which covers how Tally maps its data onto these entities).

## Summary

- Abacus has a REST API following **OData 4.0**, served by each customer's own Abacus server. Login is **OAuth 2.0 / OpenID Connect** against that server.
- There is **no official .NET SDK**. Abacus publishes an **OpenAPI 3.0 document** (~26 MB, 2 335 paths) per release, which NSwag can generate from after a preprocessing step.
- This library: a **generated NSwag client**, pruned to the entities we need, with the **Abacus server configurable in `appsettings.json`**, a token-handling `DelegatingHandler` and a pluggable token provider. Published as a NuGet package. See [Library design](#library-design).

## The Abacus API

| Topic | Finding |
|---|---|
| Style | REST, **OData 4.0** (`$filter`, `$select`, `$expand`, `$orderby`, `$top`, batch, deep insert). |
| Base URL | `{AbacusServerUrl}/api/entity/v1/mandants/{mandant}/{EntitySet}`, e.g. `/api/entity/v1/mandants/7777/ProjectBookings`. Key access: `.../ProjectBookings({Id})`. |
| Metadata | `.../mandants/{mandant}/$metadata` (CSDL); list of entity sets at `.../mandants/{mandant}/`. Swagger UI on the instance at `/swagger-ui/index.html`. |
| Version | Entity availability depends on the Abacus release installed on the server (2024 / 2025 / 2026). E.g. `InAndOuts` only exists from **2026**. |
| Paging | Max **100 records** per response; follow `@odata.nextLink`. |
| Rate limit | Per user. ≤ 2025: 200/min, 12 000/h, 30 000/day. From 2026: 400/min, 18 000/h, 40 000/day. HTTP 429 when exceeded. A `$batch` counts as one request. |
| Change tracking | Subscription endpoints exist (changes since a timestamp). |

## Authentication

OAuth 2.0 / OpenID Connect against the Abacus server itself ([API Hub: Authorization](https://apihub.abacus.ch/authorization)).

**Discovery.** `GET {server}/.well-known/openid-configuration` returns `token_endpoint` (`/oauth/oauth2/v1/token`), `authorization_endpoint` (`/oauth/oauth2/v1/auth`), `token_revocation_endpoint`, `end_session_endpoint`, `userinfo_endpoint` and `jwks_uri`; supported scopes include `openid`, `profile`, `email`, `offline_access`. Abacus asks clients to always read the endpoints from here, not hard-code them.

**Service user setup (Q910, "Abacus API connections").** For the OData entities the service user is created in **Q910**, where the admin picks the login type (user-independent or user-dependent), the client (Mandant), the scopes and web origins. A Q910 service user is active immediately but **must be approved by Abacus within 21 days** (by e-mail, or via DeepBox), otherwise it is deactivated.

### Option A — user-independent (client credentials)

- Q910 shows a **Client-ID and Client-Secret**; the secret is shown **only once** at creation (it can be regenerated, never read again).
- `POST token_endpoint`, body `grant_type=client_credentials`, header `Authorization: Basic base64(ClientId:ClientSecret)` (recommended) or `client_id`/`client_secret` in the body.
- Response: `access_token`, `token_type: Bearer`, `expires_in: 600`, and `scope` (the active scopes, handy for diagnosing access problems). No refresh token; request a new token when it expires. Reuse a token for its lifetime, not one per request. No logout needed.

### Option B — user-dependent (authorization code)

- Q910 login type "Benutzerabhängig". Client type **Public** (Client-ID only, no secret) or **Trusted / "Vertraulicher Client"** (secret required in the token request). Also configured: login policy, **Login Redirect URL**, **Offline access**, scopes.
- Flow:
  1. Open in the browser: `{server}/oauth/oauth2/v1/auth?response_type=code&client_id={ClientId}&scope={space-separated, URL-encoded}&redirect_uri={URL-encoded}`. The redirect URL must be registered in Q910.
  2. The user logs in with their normal Abacus account. (If a session for another user is already open in the browser, it may be adopted.)
  3. Abacus calls the redirect URL with `?code=…&session_state=…`.
  4. `POST token_endpoint` with `grant_type=authorization_code&client_id=…&code=…&redirect_uri=…` (+ `client_secret` for a trusted client) → `access_token` (600 s), `refresh_token`, `scope`.
  5. Refresh: `grant_type=refresh_token&client_id=…&refresh_token=…` (no `redirect_uri`). The refresh token is **issued once and reused** (not rotated): later refreshes don't return a new one.
- **Refresh token lifetime:** max **60 days** with Offline access, **10 hours** without, and in any case only while the user's Abacus login session is valid. It becomes invalid when the user logs out of Abacus (or an admin logs them out), and **when another API integration uses the same user**, since that may regenerate the token. Then the browser login has to be repeated.
- **Scopes** must be on the service user (Q910) *and* on the Abacus user (Q981, or via the user category from 2026) *and* in the URL; only the intersection is granted. Max 25 scopes. Errors come back on the redirect: `invalid_scope` / "The user has no access to any scope requested" (scopes missing in Q981), "Too many scopes requested".
- **Redirect URL caveat:** Abacus writes that the callback "must be accessible from the Abacus Server (i.e. 'localhost' is generally not possible)". It also suggests the code can be fetched "by polling a further URL where the callback data would be saved", i.e. a relay that receives the callback. For desktop/local apps this is the open point: in a standard code flow the *browser* performs the redirect, so a `localhost` callback may still work. Needs testing against a real server.

## Licensing and setup

- The application behind the entities (e.g. **AbaProject** for `ProjectBookings`/`InAndOuts`) must be installed with its **AbaConnect option** enabled.
- The service user **counts as one application user** (licence cost). No distinction between read and read/write on the licence level.
- The client (Mandant) must be **enabled for the API by Abacus Research AG** (wizard in **Q910**, or **Q926**); enabling clients has a price.

## Entities in scope

| Entity set | Read scope | Read/write scope | Available | Operations in OpenAPI |
|---|---|---|---|---|
| `InAndOuts` | `abacus.entity.inandout.read` | *none listed* | 2026 | GET, POST, PATCH, DELETE |
| `ProjectBookings` | `abacus.entity.projectbooking.read` | `abacus.entity.projectbooking.readwrite` | 2024–2026 | GET, POST, PATCH, DELETE |
| `Projects` | `abacus.entity.project.read` | `abacus.entity.project.readwrite` | | GET, POST, PATCH, DELETE |
| `ServiceCodes` | `abacus.entity.projectbase.read` | `abacus.entity.projectbase.readwrite` | | GET, POST, PATCH, DELETE |
| `Employees` | `abacus.entity.employee.read` | `abacus.entity.employee.readwrite` | | GET, POST, PATCH, DELETE |

**Note:** the API Hub lists only a read scope for `InAndOuts` although the OpenAPI document has write operations. Whether it can be written (and with which scope) has to be checked on a real server.

### `InAndOuts` (`ch.abacus.proj.InAndOut`, "In & Out")

Attendance blocks of an employee.

| Property | Type | Notes |
|---|---|---|
| `Id` | uuid | **required on create** (client-supplied) |
| `EmployeeId` | int64 | personnel number |
| `Date` | date | |
| `TimeFrom` | partial-time | `"08:12:00"` |
| `TimeTo` | partial-time, nullable | open block while null |
| `PositionNumber` | int32 | item number |
| `Value` | decimal | "Number", presumably hours |
| `Presence` | `Office` / `Homeoffice` / `Remote` | nullable |

### `ProjectBookings` (`ch.abacus.proj.ProjectBooking`)

Hours, expenses, material… booked on a project. For time recording:

| Property | Type | Notes |
|---|---|---|
| `Id` | uuid | **required on create** (client-supplied) |
| `Type` | `Booking`, `Budget`, `CarryForward`, `LumpSum`, `OnAccount`, `Surcharges`, `Contract` | |
| `Date` | date | |
| `EmployeeId` | int64 | |
| `ProjectId` | int64 | |
| `ServiceCodeId` | int32 | Leistungsart |
| `Values.Quantity` | decimal | hours |
| `Values.TimeFrom` / `Values.TimeTo` | partial-time, nullable | |
| `Text` | string | |
| `Status` | `Unaccounted`, `Accounted`, `Cancelled`, `CancelledAndAccounted`, `CancellationAccounted`, `DoNotAccount` | |

Navigations: `Project`, `ServiceCode`, `Documents`.

```http
POST /api/entity/v1/mandants/{mandant}/ProjectBookings
Authorization: Bearer …
Content-Type: application/json

{ "Id": "a42e…", "Type": "Booking", "Date": "2026-10-05", "EmployeeId": 123,
  "ProjectId": 4711, "ServiceCodeId": 100, "Text": "Code review",
  "Values": { "Quantity": 1.75 } }
```

Because `Id` is supplied by the client, a consumer can store it and later `PATCH`/`DELETE` the same record instead of creating duplicates.

## The OpenAPI document

What Abacus publishes on the API Hub (e.g. [V2026](https://apihub.abacus.ch/endpoints/2026)):

- **"Download YAML-File"**: despite the name, an **OpenAPI 3.0 document in JSON**, ~26 MB, **2 335 paths** covering every entity. **This is the input for generation** (stored as `docs/openapi.json`, renamed so tools parse it as JSON).
- **"Download JSON-File"**: an **OData JSON Schema** (~3.5 MB, starts with `"$id": "ch.abacus.odata"` and `"odata-version": "4.0"`; browsers save it as `swagger.json`). Useful for reading entity shapes but **not OpenAPI**: no paths, operations or servers, so NSwag/Kiota cannot generate from it.
- A live instance also serves the OData CSDL `$metadata` and Swagger UI.

**Quirks that affect generation** (checked on `ProjectBooking-create`):

- Produced by SAP's OData→OpenAPI converter (`x-sap-precision`/`x-sap-scale` extensions, OData's `IEEE754Compatible` style). Every **int64 and decimal** property is `anyOf: [{type: integer|number}, {type: string}]` with `format: int64|decimal` (e.g. `EmployeeId`, `ProjectId`, `Values.Quantity`). NSwag maps that to `object`, Kiota to composed wrapper types.
- Times are `format: partial-time`, which generators don't map to `TimeOnly`, so they come out as `string`.
- `servers` is a placeholder (`https://services.OData.org/service-root`).
- No `securitySchemes`.
- Collection `GET`s take `$top`, `$skip`, `$filter`, `$orderby`, `$select`, `$expand`, `$count`, `$search` as plain string parameters; paging via `@odata.nextLink` isn't modelled.

So the document needs **preprocessing** before generation: keep only the needed paths plus the schemas they reference, and collapse the `anyOf [integer|number, string]` unions into plain `integer/int64` or `number/decimal`.

### Generator options considered

| Option | Pros | Cons |
|---|---|---|
| **NSwag** on the pruned doc | Familiar, one self-contained `.cs` file, `System.Text.Json`, client interfaces for testing | Needs the pruning/patching step (no path filter); no OData helpers (`$filter` strings, paging by hand) |
| **Kiota** with `--include-path` | Built-in path filter; fluent request builders | `anyOf` still needs preprocessing (or ugly composed types); pulls in the Kiota runtime packages |
| **OData Connected Service / `Microsoft.OData.Client`** from `$metadata` | Real OData client (LINQ, batch, nextLink), types from CSDL are correct | Needs a live instance to generate; heavy dependency |
| **Hand-written** (`HttpClient` + `System.Text.Json` records) | No generator, no preprocessing | Manual updates per Abacus release |

**Decision (2026-10-05): NSwag**, with the pruning and patching done in memory by the generator (below).

## Library design

Modelled on [garcipat/abusalpdb-client](https://github.com/garcipat/abusalpdb-client).

**Kept from abusalpdb-client:**
- A generator console app that loads `docs/openapi.json` with NSwag's in-process API (`NSwag.CodeGeneration.CSharp`) and writes the client into a versioned folder (`V2026/AbacusApiV2026.cs`).
- The generated code is checked in.
- An options record bound from configuration.
- A `ServiceConfiguration` DI extension method.
- Packaging through `Directory.Build.props` (`GeneratePackageOnBuild`).

**Changed:**

| abusalpdb-client | Abacus client | Why |
|---|---|---|
| The packed library is itself the generator (`OutputType Exe`, `NSwag.CodeGeneration.CSharp` reference, runs after every Debug build) | Two projects: **`…Generator`** (Exe, `IsPackable=false`, run explicitly) and **`…Client`** (library, packed) | The NuGet package doesn't drag NSwag codegen and an exe into consumers; the 26 MB doc isn't parsed on every build |
| Generates from the doc as is | The generator **prunes and patches the `OpenApiDocument` in memory**: keep only the needed paths (`/InAndOuts`, `/ProjectBookings`, `/Projects`, `/ServiceCodes`, `/Employees`, by-key and navigation paths as needed) plus referenced schemas, and collapse the `anyOf` unions into `int64`/`decimal` | No separate script; repeatable per Abacus release |
| Hard-coded `BaseUrl` | **Base URL from configuration**: `{BaseUrl}/api/entity/v1/mandants/{Mandant}/`, set as `HttpClient.BaseAddress` (`UseBaseUrl = false`, `InjectHttpClient = true`) | Every Abacus customer has their own server |
| `Func<IApi>` factory + `DefaultRequestHeaders` API key | **Typed client** via `AddHttpClient<IAbacusApi, AbacusApi>()` + an **`AbacusAuthHandler`** (`DelegatingHandler`) that adds the bearer token | Tokens expire every 600 s; the handler caches and renews them |
| Newtonsoft (NSwag default) | `JsonLibrary = SystemTextJson`, `GenerateClientInterfaces = true`, `GenerateOptionalParameters = true` | Modern default; the interface lets consumers fake the client |

**Configuration** (section `Abacus` in `appsettings.json`):

```json
"Abacus": {
  "BaseUrl": "https://abacus.example.ch",
  "Mandant": 7777,
  "ClientId": "…",
  "Scopes": [ "abacus.entity.projectbooking.readwrite", "abacus.entity.inandout.read" ]
}
```

The section is parsed into a typed `AbacusOptions` through the **options pattern** (`IOptions<T>`), as `AbusalPdbOptions` is in abusalpdb-client:

```csharp
public sealed record AbacusOptions
{
    public const string SectionName = "Abacus";

    [Required] public Uri BaseUrl { get; init; } = null!;   // e.g. https://abacus.example.ch
    [Range(1, int.MaxValue)] public int Mandant { get; init; }
    [Required] public string ClientId { get; init; } = string.Empty;
    public string? ClientSecret { get; init; }             // client credentials only, never in appsettings.json
    public IReadOnlyList<string> Scopes { get; init; } = [];

    public Uri EntityBaseAddress => new(BaseUrl, $"api/entity/v1/mandants/{Mandant}/");
}
```

```csharp
services.AddOptions<AbacusOptions>()
        .Bind(configuration.GetSection(AbacusOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

services.AddHttpClient<IAbacusApi, AbacusApi>((sp, http) =>
            http.BaseAddress = sp.GetRequiredService<IOptions<AbacusOptions>>().Value.EntityBaseAddress)
        .AddHttpMessageHandler<AbacusAuthHandler>();
```

- `ValidateOnStart()`: a missing or invalid `BaseUrl`/`Mandant`/`ClientId` fails when the host starts, not on the first request.
- Library classes take `IOptions<AbacusOptions>`: the typed client's base address, `AbacusAuthHandler`, and `ClientCredentialsTokenProvider` (token endpoint discovery, `ClientId`/`ClientSecret`, `Scopes`). Consumers can inject the same `IOptions<AbacusOptions>`, e.g. Tally to show the configured server.
- Overloads for consumers that don't use the default section: `AddAbacusApi(IConfigurationSection section)` and `AddAbacusApi(Action<AbacusOptions> configure)`. The latter is handy in tests.
- `IOptions` (a singleton snapshot) is enough. The server doesn't change at runtime, and the typed `HttpClient`'s base address is set when the client is created anyway.
- The **secret is not in `appsettings.json`**. It binds onto the same `ClientSecret` property from user secrets or an environment variable (`Abacus__ClientSecret`), since all configuration providers feed the same section.
- Token endpoint discovery (`{BaseUrl}/.well-known/openid-configuration`) happens once and is cached.

**Auth is pluggable**, since the right flow depends on the consumer:
- `IAbacusTokenProvider` (`Task<string> GetAccessTokenAsync(CancellationToken)`).
- The package ships `ClientCredentialsTokenProvider` (option A).
- For option B the consuming app registers its own provider, which owns the browser login and refresh-token storage (app-specific, stays out of the package).

```csharp
services.AddAbacusApi(configuration);               // client credentials (default provider)
services.AddAbacusApi(configuration)
        .AddTokenProvider<MyAppAbacusTokenProvider>(); // user-dependent, provided by the app
```

**Versioning:** namespace and folder per Abacus release (`…V2026`). The package version tracks it (e.g. `2026.201.0` for "V 2026.201"). A 2025 server would get a `V2025` generation from the 2025 doc.

**Tests:** one test project, **`AbacusApi.Tests`**, for all projects, with a folder per project under test (`Client/`, `Generator/`, …) rather than a test project per project.
- Unit tests for the document pruning/patching (the generated `ProjectBooking` has `long? EmployeeId`, `decimal? Quantity`).
- Unit tests for `AbacusAuthHandler` (token cached, renewed after expiry, 401 → one retry with a fresh token).
- No integration tests for now: there is no Abacus server or container to test against. The calls we use are tested against a stub `HttpMessageHandler` (see [TestingGuide.md](TestingGuide.md)).

**Naming:** "Abacus" is Abacus Research AG's product name. The package is prefixed (`Garcipat.AbacusApi.Client`) and described as unofficial.

## Other integration paths (not suitable for a client library)

- **AbaConnect** (file/XML import, e.g. the "PROJ – Employee time axis" interface): runs as `abaconnectimportconsole.exe` **on the Abacus server**.
- **AbaClock / MyAbacus portal**: AbaClock is a terminal/app syncing via Abacus' cloud; no public API to stamp on someone's behalf. MyAbacus has no documented API for clocking.
- **ODBC/JDBC**: read access to the database, not a supported write path.

## Open questions

1. Is `InAndOuts` writable, and with which scope?
2. Does a `localhost` redirect URL work for the user-dependent flow?
3. Is there a test server/Mandant (the API Hub has a "Testservers" page)? That would allow integration tests later.

## Sources

- [Abacus API Hub](https://apihub.abacus.ch): [OData endpoints / getting started](https://apihub.abacus.ch/odata), [V2026 entity list + OpenAPI download](https://apihub.abacus.ch/endpoints/2026), [Authorization](https://apihub.abacus.ch/authorization), [InAndOuts](https://apihub.abacus.ch/apis/2026/entity/in-and-outs.api), [ProjectBookings](https://apihub.abacus.ch/apis/2026/entity/project-bookings.api), [Projects](https://apihub.abacus.ch/apis/2026/entity/projects.api), [ServiceCodes](https://apihub.abacus.ch/apis/2026/entity/service-codes.api), [Employees](https://apihub.abacus.ch/apis/2026/entity/employees.api), [Licensing](https://apihub.abacus.ch/licensing), [Rate limit](https://apihub.abacus.ch/ratelimit), [Client libraries](https://apihub.abacus.ch/clientlibraries)
- [Abacus REST-API overview](https://downloads.abacus.ch/fileadmin/ablage/abaconnect/htmlfiles/docs/restapi/abacus_rest_api.html) and [authorization](https://downloads.abacus.ch/fileadmin/ablage/abaconnect/htmlfiles/docs/restapi/abacus_authorization_rest_api.html)
- [AbaConnect PROJ – Employee time axis](https://downloads.abacus.ch/fileadmin/ablage/abaconnect/htmlfiles/proj/PROJ__EmployeeTimeAxis_2022.00_AbaDefault_EN.html)
- [AbaClock FAQ](https://downloads.abacus.ch/fileadmin/ablage/dokumente/06_weitere_applikationsdokumente/zeiterfassung/AbaClock_FAQ_DE.pdf)
- [abacus-php-rest-api](https://github.com/Memurame/abacus-php-rest-api) (community client, PHP)
