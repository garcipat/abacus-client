# Abacus API — Research

> Background for this client library: how the Abacus REST API works, how to authenticate, what the published OpenAPI document looks like, and how the library is designed. Researched 2026-10-05 against the Abacus API Hub (V2026, patch delivered 15.08.2026). It is independent of any particular consumer: the integration options are described in general terms (see [Integration options](#integration-options)).

## Summary

- Abacus has a REST API following **OData 4.0**, served by each customer's own Abacus server. Login is **OAuth 2.0 / OpenID Connect** against that server.
- The REST API is the only supported way for an external application to read and write data; AbaConnect, AbaClock and ODBC don't fit (see [Integration options](#integration-options)). For time recording, `InAndOuts` (attendance) and `ProjectBookings` (project hours) are the relevant entities.
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
| `TimeFrom` | time | `"08:12:00"` |
| `TimeTo` | time, nullable | open block while null |
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
| `Values.InternalValue` / `Values.ExternalValue` | `Quantity`, `Price`, `Amount` (decimal) | the hours go into `Quantity`; which of the two (or both) is expected is to be checked |
| `Values.TimeFrom` / `Values.TimeTo` | time, nullable | |
| `Text` | string | |
| `Status` | `Unaccounted`, `Accounted`, `Cancelled`, `CancelledAndAccounted`, `CancellationAccounted`, `DoNotAccount` | |

Navigations: `Project`, `ServiceCode`, `Documents`.

```http
POST /api/entity/v1/mandants/{mandant}/ProjectBookings
Authorization: Bearer …
Content-Type: application/json

{ "Id": "a42e…", "Type": "Booking", "Date": "2026-10-05", "EmployeeId": 123,
  "ProjectId": 4711, "ServiceCodeId": 100, "Text": "Code review",
  "Values": { "InternalValue": { "Quantity": 1.75 } } }
```

## Integration options

| Path | How | Where it runs | Suitable for an external application |
|---|---|---|---|
| **REST API (OData entities)** | `api/entity/v1/mandants/{mandant}/…`, OAuth 2.0 | over HTTPS against the customer's Abacus server | **yes**: the only supported read/write path from outside; what this library wraps |
| **AbaConnect** | file/XML import and export (e.g. "PROJ – Employee time axis"), `abaconnectimportconsole.exe` / `abaconnectexportconsole.exe` | **on the Abacus server** | no, only for jobs running on the server itself |
| **AbaClock / MyAbacus** | AbaClock terminals/app sync stamps via Abacus' cloud; MyAbacus is the employee portal | Abacus cloud / browser | no: no public API to stamp or book on someone's behalf |
| **ODBC/JDBC** | direct database access | database connection to the server | read-only reporting, not a supported write path |

### Time recording via the REST API

Two entity sets cover time recording. They are independent: Abacus may or may not derive one from the other, depending on the installation's configuration (see [Open questions](#open-questions)).

**Attendance → `InAndOuts`** (when an employee was working at all):
- One row per block per day (`Date`, `TimeFrom`, `TimeTo`, optionally `Presence`). A break is the gap between two rows. A block over midnight has to be split into one row per day.
- `TimeTo` is nullable, so an open block can be written at clock-in and patched at clock-out. Pushing finished blocks in a batch (e.g. at the end of the day) is simpler and avoids half-written days.
- `Value` ("Number") is presumably the hours; whether Abacus computes it or the client has to send it is to be checked.
- Only available from **2026**, and only a **read** scope is documented (see [Entities in scope](#entities-in-scope)). If it turns out read-only, a client can still read the official attendance (e.g. to compare it with its own records), and employees keep clocking in Abacus/AbaClock.

**Project time → `ProjectBookings`** (what the time was spent on):
- `Type = Booking`, `Date`, `EmployeeId`, `Values.InternalValue.Quantity` (hours; see [Open questions](#open-questions) on internal vs. external value), `Text`.
- Every booking needs a **project and a service code** (`ProjectId` + `ServiceCodeId`, Leistungsart). A client has to know that pair for whatever it books, either by letting the user map their own tasks/activities to a project + service code, or by reading the candidates from Abacus: `Projects` (e.g. filtered by `ProjectTeams` membership) and the allowed service codes (`ServiceCodes`, `Projects({Id})/ServiceCodeRelations`).
- Granularity is the client's choice: one booking per project/service code per day (aggregated `Quantity`), or one per time block with `Values.TimeFrom`/`Values.TimeTo`. Rounding rules depend on the installation.
- `Status` (`Unaccounted`, `Accounted`, …): an accounted booking is presumably locked; a client should read it back before changing a booking.

**Re-sync without duplicates.** Both entities take a **client-supplied UUID `Id`** on create. A client generates the Id once, stores it with its own record, and on a later sync `PATCH`es that record (or `DELETE`s it if the source record was removed) instead of creating a duplicate. The stored Id is the "already synced" marker; no separate bookkeeping is needed.

### Choosing an auth flow

Which OAuth option (see [Authentication](#authentication)) fits depends on the kind of application:

| | A: client credentials | B: user-dependent |
|---|---|---|
| Acts as | a technical user; the client sets `EmployeeId` itself | the logged-in Abacus user |
| Secret | yes; it can write for **every** employee within the granted scopes | none with a public client |
| User interaction | none after setup | browser login at least every 60 days (10 h without Offline access), and again after every Abacus logout |
| Callback | not needed | a redirect URL reachable from the Abacus server; `localhost` "generally not possible" |
| Fits | server-side integrations and background jobs run by the organisation | personal or desktop tools where each user books their own time |

For a **desktop or local tool**, B is the better model (each user books as themselves, no all-powerful secret on a laptop), but the redirect requirement is the catch. Whether a `http://localhost:<port>/…` callback works has to be tested. If it doesn't, the options are a small relay endpoint on an internal host that stores the code for the tool to poll (Abacus' own suggestion), or A with a service user restricted to the needed scopes. This library keeps the flow pluggable (`IAbacusTokenProvider`, see [Library design](#library-design)).

## The OpenAPI document

What Abacus publishes on the API Hub (e.g. [V2026](https://apihub.abacus.ch/endpoints/2026)):

- **"Download YAML-File"**: despite the name, an **OpenAPI 3.0 document in JSON**, ~26 MB, **2 335 paths** covering every entity. **This is the input for generation.**
  - **Download manually, it can't be linked:** the hub is a Vaadin app and its download links (`/VAADIN/dynamic/resource/<n>/<uuid>/`) are created per browser session. The same URL returns 404 outside that session, so a build or CI can't fetch it.
  - Saved locally as `src/AbacusApi.Generator/OpenApi/abacus-<release>.json` (e.g. `abacus-2026.201.json`, renamed from `.yaml` so tools parse it as JSON). It is **gitignored** because of its size. The generator trims it to `abacus-<release>.trimmed.json` (only the paths we use, `anyOf` fixed), which **is committed**, so regenerating the same paths needs no download and each Abacus release gives a readable diff.
- **"Download JSON-File"**: an **OData JSON Schema** (~3.5 MB, starts with `"$id": "ch.abacus.odata"` and `"odata-version": "4.0"`; browsers save it as `swagger.json`). Useful for reading entity shapes but **not OpenAPI**: no paths, operations or servers, so NSwag/Kiota cannot generate from it.
- A live instance also serves the OData CSDL `$metadata` and Swagger UI.

**Quirks that affect generation** (checked on `ProjectBooking-create`):

- Produced by SAP's OData→OpenAPI converter (`x-sap-precision`/`x-sap-scale` extensions, OData's `IEEE754Compatible` style). Every **int64 and decimal** property is `anyOf: [{type: integer|number}, {type: string}]` with `format: int64|decimal` (e.g. `EmployeeId`, `ProjectId`, `Values.Quantity`). NSwag maps that to `object`, Kiota to composed wrapper types.
- Dates are `format: date` (`"2026-10-05"`), times `format: time` (`"15:51:04"`). NSwag's defaults (`DateTimeOffset`, `TimeSpan`) would not round-trip them.
- **No `operationId`s**, so NSwag would invent method names from the paths.
- Error responses use status code **ranges** (`4XX`), which NSwag 14 doesn't support: it emits `if (status_ == 4XX)`, which doesn't compile.
- Collection responses are **inline schemas**, which NSwag names `Response`, `Response2`, …
- `$orderby`, `$select`, `$expand` list every property as an **enum** (`"Id"`, `"Id desc"`, …), which NSwag generates as `AnonymousN` enums per operation.
- **Navigation properties** (`ProjectBooking.Project`, `Project.CustomerSubject`, …) chain into almost every module: the five entity sets we need reference **1 366 of 3 688** schemas.
- `servers` is a placeholder (`https://services.OData.org/service-root`). No `securitySchemes`. Paging via `@odata.nextLink` isn't modelled.

So the document needs **preprocessing** before generation, see [Generator](#generator).

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
- A generator console app that loads the OpenAPI document (`src/AbacusApi.Generator/OpenApi/`, see [The OpenAPI document](#the-openapi-document)) with NSwag's in-process API (`NSwag.CodeGeneration.CSharp`) and writes the client into a versioned folder (`V2026/AbacusApiV2026.cs`).
- The generated code is checked in.
- An options record bound from configuration.
- A `ServiceConfiguration` DI extension method.
- Packaging through `Directory.Build.props` (`GeneratePackageOnBuild`).

**Changed:**

| abusalpdb-client | Abacus client | Why |
|---|---|---|
| The packed library is itself the generator (`OutputType Exe`, `NSwag.CodeGeneration.CSharp` reference, runs after every Debug build) | Two projects: **`…Generator`** (Exe, `IsPackable=false`, run explicitly) and **`…Client`** (library, packed) | The NuGet package doesn't drag NSwag codegen and an exe into consumers; the 26 MB doc isn't parsed on every build |
| Generates from the doc as is | The generator **trims and patches the document** first and commits the result (see [Generator](#generator)) | The raw document doesn't generate usable (or compilable) code; repeatable per Abacus release |
| Hard-coded `BaseUrl` | **Base URL from configuration**: `{BaseUrl}/api/entity/v1/mandants/{Mandant}/`, set as `HttpClient.BaseAddress` (`UseBaseUrl = false`, `InjectHttpClient = true`) | Every Abacus customer has their own server |
| `Func<IApi>` factory + `DefaultRequestHeaders` API key | **Typed client** via `AddHttpClient<IAbacusApi, AbacusApi>()` + an **`AbacusAuthHandler`** (`DelegatingHandler`) that adds the bearer token | Tokens expire every 600 s; the handler caches and renews them |
| Newtonsoft (NSwag default) | `JsonLibrary = SystemTextJson`, `GenerateClientInterfaces = true`, `GenerateOptionalParameters = true` | Modern default; the interface lets consumers fake the client |

### Generator

Runs automatically after `AbacusApi.Generator` builds (MSBuild target `GenerateAbacusClient`, `AfterTargets="Build"`). `AbacusApi.Client` references the generator for build order only (`ReferenceOutputAssembly="false"`, `PrivateAssets="all"`), so building the client, the tests or the solution generates first, and the package gets no dependency on the generator. The target is incremental (Inputs: generator dll, its csproj, the OpenAPI documents; Output: the generated file) and skipped in Release, where packing uses the committed file.

Settings are MSBuild properties in `AbacusApi.Generator.csproj`, passed to the generator as `--name value` options:

| Property | Default | |
|---|---|---|
| `AbacusRelease` | `2026.201` | selects `OpenApi/abacus-{release}[.trimmed].json` |
| `AbacusEntitySets` | `InAndOuts;ProjectBookings;Projects;ServiceCodes;Employees` | kept by the trimmer (takes effect only with the full download) |
| `AbacusClientNamespace` | `Garcipat.AbacusApi.Client.V{major}` | |
| `AbacusClientClassName` | `AbacusApi` | interface `I{name}` |
| `AbacusClientOutput` | `../AbacusApi.Client/V{major}/{class}V{major}.cs` | |
| `AbacusGenerateOnBuild` | `true` (`false` in Release) | `-p:AbacusGenerateOnBuild=false` to skip |

Steps:

1. If the full download `OpenApi/abacus-2026.201.json` exists, it is trimmed and patched into `OpenApi/abacus-2026.201.trimmed.json` (committed; ~360 KB, 10 paths, ~225 schemas):
   - **`OpenApiTrimmer`**: keeps `/{set}` and `/{set}({Id})` of `InAndOuts`, `ProjectBookings`, `Projects`, `ServiceCodes`, `Employees`, plus the components they reference (transitively). Navigation properties to entity types that are **not** kept are removed (entity types are found via each collection `GET`'s `value.items`). Sets operation ids (`ListProjectBookings`, `GetProjectBooking`, `CreateProjectBooking`, `UpdateProjectBooking`, `DeleteProjectBooking`), moves collection responses to named schemas (`ProjectBookingCollection`), drops the `info.description` diagram and unrelated tags. Components keep the source order, so diffs between releases stay readable.
   - **`OpenApiPatcher`**: collapses `anyOf [integer|number, string]` to the numeric type (→ `long`/`decimal` via `format`); replaces `4XX` ranges with `default` (→ `ApiException<Error>`); turns the `$orderby`/`$select`/`$expand` enums into plain strings (→ `IEnumerable<string>`); makes every property of the `-update` (PATCH) schemas optional and nullable. Without that last one, NSwag generates e.g. `DateOnly Date` on `ProjectBookingUpdate`, and every PATCH would send `"Date":"0001-01-01"`.
2. **`ClientGenerator`** runs NSwag on the trimmed document into `AbacusApi.Client/V2026/AbacusApiV2026.cs` (namespace `Garcipat.AbacusApi.Client.V2026`, class `AbacusApi`, interface `IAbacusApi`): `System.Text.Json`, nullable reference types, `DateOnly`/`TimeOnly`, `UseBaseUrl = false` with an injected `HttpClient`, `SingleClientFromOperationId` naming.

Without the full download, step 1 is skipped and the client is regenerated from the committed trimmed document.

**Unset properties are not sent.** The generated serializer would write every unset property as `null`, and for a `PATCH` (OData: `null` = clear the field) that would wipe them. `V2026/AbacusApiV2026.Serialization.cs` sets `DefaultIgnoreCondition = WhenWritingNull` through NSwag's `static partial void UpdateJsonSerializerSettings(...)` hook. That file is hand-written; each generated release folder needs one.

### Configuration

Section `Abacus` in `appsettings.json`:

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

    [Required] public Uri BaseUrl { get; set; } = null!;   // e.g. https://abacus.example.ch
    [Range(1, int.MaxValue)] public int Mandant { get; set; }
    [Required] public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }             // client credentials only, never in appsettings.json
    public IReadOnlyList<string> Scopes { get; set; } = [];

    public Uri GetEntityBaseAddress() => …;               // {BaseUrl}/api/entity/v1/mandants/{Mandant}/, BaseUrl path kept
}
```

- Settable properties (not `init`), so `AddAbacusApi(Action<AbacusOptions>)` can configure them.
- `GetEntityBaseAddress()` is a method, not a property: `ValidateDataAnnotations` reads all properties, and a computed property would throw while `BaseUrl` is still missing instead of reporting a validation error.

`AddAbacusApi` registers (see `ServiceConfiguration.cs`):

```csharp
services.AddOptions<AbacusOptions>().Bind(section).ValidateDataAnnotations().ValidateOnStart();
services.TryAddSingleton(TimeProvider.System);
services.TryAddSingleton<IAbacusTokenProvider, ClientCredentialsTokenProvider>();
services.TryAddTransient<AbacusAuthHandler>();
services.AddHttpClient(ClientCredentialsTokenProvider.HttpClientName);   // token requests, without the auth handler
services.AddHttpClient<IAbacusApi, AbacusApi>((sp, http) =>
            http.BaseAddress = sp.GetRequiredService<IOptions<AbacusOptions>>().Value.GetEntityBaseAddress())
        .AddHttpMessageHandler<AbacusAuthHandler>();
```

- `ValidateOnStart()`: a missing or invalid `BaseUrl`/`Mandant`/`ClientId` fails when the host starts, not on the first request.
- Library classes take `IOptions<AbacusOptions>`: the typed client's base address, `AbacusAuthHandler`, and `ClientCredentialsTokenProvider` (token endpoint discovery, `ClientId`/`ClientSecret`, `Scopes`). Consumers can inject the same `IOptions<AbacusOptions>`, e.g. to show the configured server.
- Overloads for consumers that don't use the default section: `AddAbacusApi(IConfigurationSection section)` and `AddAbacusApi(Action<AbacusOptions> configure)`. The latter is handy in tests.
- `IOptions` (a singleton snapshot) is enough. The server doesn't change at runtime, and the typed `HttpClient`'s base address is set when the client is created anyway.
- The **secret is not in `appsettings.json`**. It binds onto the same `ClientSecret` property from user secrets or an environment variable (`Abacus__ClientSecret`), since all configuration providers feed the same section.
- `ClientCredentialsTokenProvider` (singleton): token endpoint discovery (`{BaseUrl}/.well-known/openid-configuration`) once, then `POST grant_type=client_credentials` (+ `scope`) with Basic auth. The token is cached and renewed 30 s before `expires_in` runs out; concurrent callers share one request. A missing `ClientSecret` throws `InvalidOperationException`, a failed token request `HttpRequestException`.
- `AbacusAuthHandler` adds `Authorization: Bearer <token>` to each request. There is no retry on 401: the provider renews before expiry, and replaying a request isn't safe for every body.

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
- `Generator/`: trimming, patching and the NSwag settings (`long`/`decimal`, `DateOnly`/`TimeOnly`, no `BaseUrl`, System.Text.Json).
- `Client/`: options (base address), DI registration (binding, validation, default and replaced token provider, base address and bearer token on real requests through the stub handler), `AbacusAuthHandler`, `ClientCredentialsTokenProvider` (discovery, Basic auth, scopes, caching, renewal, errors), and serialization of the generated client (no unset properties in a PATCH, Abacus date/time/decimal formats).
- No integration tests for now: there is no Abacus server or container to test against. The calls we use are tested against a stub `HttpMessageHandler` (see [TestingGuide.md](TestingGuide.md)).

**Naming:** "Abacus" is Abacus Research AG's product name. The package is prefixed (`Garcipat.AbacusApi.Client`) and described as unofficial.

## Open questions

To be answered per Abacus installation (and ideally once on a test server):

1. **Release:** 2026 or later (`InAndOuts` exists) or older (only `ProjectBookings`)?
2. **Is `InAndOuts` writable**, and with which scope?
3. **Licensing/enablement:** is the API enabled for the Mandant, is AbaConnect licensed for AbaProject, and who creates and gets approval (within 21 days) for the Q910 service user? Do the users have the scopes in Q981?
4. **Redirect URL:** does a `localhost` redirect work for the user-dependent flow?
5. **Locking:** can bookings be changed once `Accounted`, or after a period is closed/approved (visa)?
6. **Attendance vs. bookings:** does the installation derive attendance from project bookings (or the other way round)? If so, writing both may be redundant or conflict.
7. **Presence:** is `Presence` (`Office`/`Homeoffice`/`Remote`) required by the configuration?
8. **`InAndOut.Value`:** computed by Abacus or sent by the client?
9. **`ProjectBooking` hours:** do they go into `Values.InternalValue.Quantity`, `Values.ExternalValue.Quantity`, or both? Are `Price`/`Amount` filled in by Abacus from the rates?
10. **Test server:** is there a test server/Mandant (the API Hub has a "Testservers" page)? That would allow integration tests.

## Sources

- [Abacus API Hub](https://apihub.abacus.ch): [OData endpoints / getting started](https://apihub.abacus.ch/odata), [V2026 entity list + OpenAPI download](https://apihub.abacus.ch/endpoints/2026), [Authorization](https://apihub.abacus.ch/authorization), [InAndOuts](https://apihub.abacus.ch/apis/2026/entity/in-and-outs.api), [ProjectBookings](https://apihub.abacus.ch/apis/2026/entity/project-bookings.api), [Projects](https://apihub.abacus.ch/apis/2026/entity/projects.api), [ServiceCodes](https://apihub.abacus.ch/apis/2026/entity/service-codes.api), [Employees](https://apihub.abacus.ch/apis/2026/entity/employees.api), [Licensing](https://apihub.abacus.ch/licensing), [Rate limit](https://apihub.abacus.ch/ratelimit), [Client libraries](https://apihub.abacus.ch/clientlibraries)
- [Abacus REST-API overview](https://downloads.abacus.ch/fileadmin/ablage/abaconnect/htmlfiles/docs/restapi/abacus_rest_api.html) and [authorization](https://downloads.abacus.ch/fileadmin/ablage/abaconnect/htmlfiles/docs/restapi/abacus_authorization_rest_api.html)
- [AbaConnect PROJ – Employee time axis](https://downloads.abacus.ch/fileadmin/ablage/abaconnect/htmlfiles/proj/PROJ__EmployeeTimeAxis_2022.00_AbaDefault_EN.html)
- [AbaClock FAQ](https://downloads.abacus.ch/fileadmin/ablage/dokumente/06_weitere_applikationsdokumente/zeiterfassung/AbaClock_FAQ_DE.pdf)
- [abacus-php-rest-api](https://github.com/Memurame/abacus-php-rest-api) (community client, PHP)
