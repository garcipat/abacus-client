using System.Diagnostics;
using System.Net;
using System.Text;

namespace Pgarcia.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;

/// <summary>
/// Opens the Abacus login in the system browser and listens on the loopback <c>RedirectUri</c>
/// (e.g. <c>http://localhost:53682/callback</c>) for the redirect back.
/// </summary>
public sealed class LoopbackBrowserCodeReceiver : IAuthorizationCodeReceiver
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    private readonly Func<Uri, Task> _openBrowser;
    private readonly TimeSpan _timeout;

    public LoopbackBrowserCodeReceiver()
        : this(OpenSystemBrowser, DefaultTimeout)
    {
    }

    /// <param name="openBrowser">Shows the login URL; the default starts the system browser.</param>
    /// <param name="timeout">How long to wait for the user to log in.</param>
    public LoopbackBrowserCodeReceiver(Func<Uri, Task> openBrowser, TimeSpan timeout)
    {
        _openBrowser = openBrowser;
        _timeout = timeout;
    }

    public async Task<IReadOnlyDictionary<string, string>> ReceiveAsync(Uri authorizationUrl, Uri redirectUri, CancellationToken cancellationToken)
    {
        if (redirectUri.Scheme != Uri.UriSchemeHttp || !redirectUri.IsLoopback)
            throw new ArgumentException("The interactive login needs an http loopback RedirectUri, e.g. http://localhost:53682/callback.", nameof(redirectUri));

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://{redirectUri.Authority}/");
        listener.Start();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        await _openBrowser(authorizationUrl).ConfigureAwait(false);

        try
        {
            while (true)
            {
                var context = await listener.GetContextAsync().WaitAsync(timeout.Token).ConfigureAwait(false);
                if (!string.Equals(context.Request.Url!.AbsolutePath, redirectUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    // e.g. the browser asking for /favicon.ico
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    continue;
                }

                var query = context.Request.QueryString;
                var result = query.AllKeys.OfType<string>().ToDictionary(key => key, key => query[key] ?? string.Empty);
                await WritePageAsync(context.Response, result, cancellationToken).ConfigureAwait(false);
                return result;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AbacusLoginException($"No response from the Abacus login within {_timeout.TotalMinutes:0.#} minutes.");
        }
    }

    private static async Task WritePageAsync(HttpListenerResponse response, Dictionary<string, string> result, CancellationToken cancellationToken)
    {
        var message = result.TryGetValue("error", out var error)
            ? $"The Abacus login failed: {WebUtility.HtmlEncode(error)}."
            : "Logged in to Abacus.";
        var html = $"<!doctype html><html><head><meta charset=\"utf-8\"><title>Abacus login</title></head><body><p>{message}</p><p>You can close this window and return to the application.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);

        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private static Task OpenSystemBrowser(Uri url)
    {
        using var _ = Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
