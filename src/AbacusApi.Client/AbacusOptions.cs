using System.ComponentModel.DataAnnotations;

namespace Garcipat.AbacusApi.Client;

/// <summary>Connection to an Abacus server, bound from the <c>Abacus</c> configuration section.</summary>
public sealed record AbacusOptions
{
    public const string SectionName = "Abacus";

    /// <summary>The Abacus server, e.g. <c>https://abacus.example.ch</c>.</summary>
    [Required]
    public Uri BaseUrl { get; set; } = null!;

    /// <summary>The Abacus client (Mandant) number.</summary>
    [Range(1, int.MaxValue)]
    public int Mandant { get; set; }

    /// <summary>Client-ID of the service user (Q910).</summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client-Secret for the client credentials flow. Never in <c>appsettings.json</c>: user secrets or <c>Abacus__ClientSecret</c>.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Scopes to request, e.g. <c>abacus.entity.projectbooking.readwrite</c>.</summary>
    public IReadOnlyList<string> Scopes { get; set; } = [];

    /// <summary>
    /// Interactive (user-dependent) login only: the loopback URL the browser is sent back to, e.g.
    /// <c>http://localhost:53682/callback</c>. Must be registered for the service user in Q910.
    /// </summary>
    public Uri? RedirectUri { get; set; }

    /// <summary>
    /// Interactive login only: send a PKCE code challenge (RFC 7636), recommended for public clients.
    /// The Abacus documentation doesn't mention PKCE; switch it off if the server rejects it.
    /// </summary>
    public bool UsePkce { get; set; } = true;

    /// <summary><c>{BaseUrl}/api/entity/v1/mandants/{Mandant}/</c>, the base address of the entity endpoints. A method, so options validation (which reads all properties) doesn't evaluate it while <see cref="BaseUrl"/> is still missing.</summary>
    public Uri GetEntityBaseAddress() => new(WithTrailingSlash(BaseUrl), $"api/entity/v1/mandants/{Mandant}/");

    /// <summary>Without it, a relative URI replaces the last path segment (<c>https://host/abacus</c> → <c>https://host/api/…</c>).</summary>
    private static Uri WithTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
}
