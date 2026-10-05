# Testing Guide

> Adapted from [bingo-anything's TestingGuide](https://github.com/garcipat/bingo-anything/blob/main/docs/TestingGuide.md): the general unit-test conventions are the same; the project-specific parts are rewritten for an HTTP client library.

## Test Types

| Type        | Use                                                                 | Infrastructure                                   |
| ----------- | ------------------------------------------------------------------- | ------------------------------------------------ |
| Unit        | Options binding/validation, auth handler, token provider, generator pruning/patching | Moq + AwesomeAssertions, stub `HttpMessageHandler` |

## Project Layout

There is **one test project, `src/AbacusApi.Tests`**, for all projects. Inside it, a folder per project under test, mirroring that project's folders:

```
src/AbacusApi.Tests/
  Client/          tests for AbacusApi.Client
  Generator/       tests for AbacusApi.Generator
```

Namespaces follow the folders (`Garcipat.AbacusApi.Tests.Client`, …). Don't add a test project per project.

---

## Unit Tests

### Class Structure

Test classes follow this structure:
**fields → constructor → tests → Dispose (if needed) → private helpers**

```csharp
public class {SystemUnderTest}Tests
{
    private readonly Mock<IDependency> _dependencyMock;
    private readonly ISimpleDependency _simpleDependency;
    private readonly AbacusOptions _options;
    private readonly {SystemUnderTest} _uut;

    public {SystemUnderTest}Tests()
    {
        _dependencyMock = GetDependencyMock();
        _simpleDependency = GetSimpleDependency();
        _options = new AbacusOptions { /* initial values */ };
        _uut = new {SystemUnderTest}(
            _dependencyMock.Object,
            _simpleDependency,
            Options.Create(_options),
            new NullLogger<{SystemUnderTest}>()
        );
    }

    [Fact]
    public void Method_Condition_ExpectedResult()
    {
        var result = _uut.DoSomething();

        result.Should().Be(expected);
    }

    public void Dispose() { /* cleanup if needed */ }

    private Mock<IDependency> GetDependencyMock() { }
    private ISimpleDependency GetSimpleDependency() { }
}
```

### Keep Setup Out of the Test Body

If several tests need the same shape of interaction with a dependency (the same method wired up, even if the argument differs per test), put that wiring in a small private helper instead of repeating the full `mock.Setup(...).ReturnsAsync(...)` line in every test:

```csharp
// Instead of repeating this in every test:
_tokenProviderMock.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);

// Extract it once:
private void SetupToken(string token) =>
    _tokenProviderMock.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);
```

This is distinct from the `Get*Mock()` pattern below, which builds the mock itself with defaults baked in at construction time. Use a `SetupX(...)` helper when the setup varies per test but the *shape* of the interaction doesn't.

### Arrange/Act/Assert Comments

Tests follow Arrange/Act/Assert, separated by blank lines. Most tests don't need `// Arrange` / `// Act` / `// Assert` labels: a three-line test speaks for itself. Add them only when a section (almost always Arrange) grows past one line, so a reader can tell where setup ends and the behaviour under test begins:

```csharp
[Fact]
public async Task SendAsync_WhenTokenExpired_ShouldFetchNewToken()
{
    // Arrange
    _timeProvider.Advance(TimeSpan.FromSeconds(601));
    SetupToken("first");
    await SendRequestAsync();
    SetupToken("second");

    // Act
    await SendRequestAsync();

    // Assert
    _innerHandler.LastRequest!.Headers.Authorization!.Parameter.Should().Be("second");
}
```

Don't add narration comments beyond the three labels. A comment restating the line below it is noise. If a setup line needs explaining, explain the non-obvious *why*, not what the code already says.

Tests have block bodies, not expression bodies (`=>`).

### Mocking Patterns

**Get\*() returns .Object**: use when behaviour doesn't change between tests:

```csharp
private ISimpleDependency GetSimpleDependency()
{
    var mock = new Mock<ISimpleDependency>();
    mock.Setup(x => x.Method())
        .ReturnsAsync(SomeValue);
    return mock.Object;
}
```

**Get\*Mock() returns Mock**: use when you change behaviour or verify calls per test:

```csharp
private Mock<IAbacusTokenProvider> GetTokenProviderMock()
{
    var mock = new Mock<IAbacusTokenProvider>();
    mock.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync("token");
    return mock;
}

[Fact]
public async Task SendAsync_ShouldRequestTokenOnce()
{
    await SendRequestAsync();

    _tokenProviderMock.Verify(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
}
```

### HTTP

Don't mock `HttpClient` (its methods aren't virtual). Put a **stub `HttpMessageHandler`** underneath it instead: a small test helper (`Infrastructure/StubHttpMessageHandler.cs`) that returns queued or configured responses and records the requests it received.

```csharp
var handler = new StubHttpMessageHandler()
    .Respond(HttpStatusCode.OK, """{ "access_token": "abc", "expires_in": 600 }""");
var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://abacus.test/") };
```

- Assert on what was sent (`handler.Requests`: method, URL, headers, body) and on what the code made of the response.
- For a `DelegatingHandler` (e.g. `AbacusAuthHandler`), set the stub as its `InnerHandler` and call it through an `HttpMessageInvoker`.
- Use `https://abacus.test/` as the base address in unit tests. Unit tests never reach a real server.
- Time-dependent code (token expiry) takes a `TimeProvider`; tests use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`).

### Special Cases

**IOptions:** Always field + `Options.Create()`. The same instance is wrapped, so tests can mutate the field without creating a new UUT.

```csharp
private readonly AbacusOptions _options = new() { BaseUrl = new Uri("https://abacus.test/"), Mandant = 7777, ClientId = "client" };
```

`AbacusOptions` uses `init` properties, so per-test variations use `with`:

```csharp
var uut = CreateUut(_options with { Mandant = 1 });
```

**Options binding and validation:** test through a real `ServiceCollection` with an in-memory configuration (`ConfigurationBuilder().AddInMemoryCollection(...)`), resolve `IOptions<AbacusOptions>` and assert on `.Value`, or on `OptionsValidationException` for invalid input.

**Logger:** Always `new NullLogger<T>()`.

**Async:** Use async/await naturally.

**Exceptions:** Test the exception type only. Add `.WithMessage()` only when disambiguating multiple throws of the same type.

```csharp
await _uut.Awaiting(x => x.GetAccessTokenAsync(CancellationToken.None))
    .Should().ThrowAsync<HttpRequestException>();
```

**Theory:** Use `[InlineData]` with known values.

### Assertions

Use AwesomeAssertions. One assertion per line.

```csharp
result.Should().Be(expected);
result.Should().NotBeNull();
list.Should().HaveCount(5);
await action.Should().ThrowAsync<Exception>();
```

### Test Naming

Follow: `{Method}_{Condition}_{ExpectedResult}`, PascalCase.

```
SendAsync_WhenTokenExpired_ShouldFetchNewToken
GetAccessTokenAsync_WithClientCredentials_ShouldPostBasicAuth
Prune_WithProjectBookingsPath_ShouldKeepReferencedSchemas
```

---

## Generator Tests

The generator's pruning and patching are tested on **small hand-written OpenAPI documents** inline in the test (a path or two, a schema with an `anyOf [integer, string]` property), not on the 26 MB `docs/openapi.json`. One test may load the real document to check that the configured paths still exist in it; mark it `[Trait("Category", "Slow")]` if it slows down the normal run.

---

## Generated Client Tests

There are **no integration tests for now**: Abacus is commercial on-premise software with no container image or public sandbox, so there is no server to test against. The generated client is checked with unit tests on the stub `HttpMessageHandler` for the few calls we use: the request goes to the right URL (`…/mandants/{Mandant}/ProjectBookings`) with the right method, and the JSON body and response bind to the expected properties (`long? EmployeeId`, `decimal? Quantity`, the time strings). The response JSON in these tests is taken from the shapes in the OpenAPI document.

---

## Coverage Targets

| Area                                          | Target              |
| --------------------------------------------- | ------------------- |
| Hand-written client code (options, DI, auth)  | ≥ 80% line coverage |
| Generator (pruning/patching)                  | ≥ 80% line coverage |
| Generated client (`V2026/…`)                  | not measured; the calls we use are covered by stub-handler tests |
