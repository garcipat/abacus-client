using AwesomeAssertions;
using Pgarcia.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;
using Pgarcia.AbacusApi.Client.Authentication;
using Pgarcia.AbacusApi.Client;
using Pgarcia.AbacusApi.Tests.Infrastructure;

namespace Pgarcia.AbacusApi.Tests.Client.Authentication.TokenProviders.InteractiveBrowser;

public sealed class FileTokenCacheTests : IDisposable
{
    private readonly string _directory;
    private readonly FileTokenCache _uut;

    public FileTokenCacheTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Pgarcia.AbacusApi.Tests", Guid.NewGuid().ToString("N"));
        _uut = new FileTokenCache(Path.Combine(_directory, "token.bin"));
    }

    [WindowsFact]
    public async Task LoadAsync_WithoutFile_ShouldReturnNull()
    {
        var token = await _uut.LoadAsync(CancellationToken.None);

        token.Should().BeNull();
    }

    [WindowsFact]
    public async Task SaveAsync_ThenLoadAsync_ShouldReturnToken()
    {
        await _uut.SaveAsync("refresh-token", CancellationToken.None);

        var token = await _uut.LoadAsync(CancellationToken.None);

        token.Should().Be("refresh-token");
    }

    [WindowsFact]
    public async Task SaveAsync_ShouldNotStorePlainText()
    {
        await _uut.SaveAsync("refresh-token", CancellationToken.None);

        var content = await File.ReadAllBytesAsync(_uut.Path);

        System.Text.Encoding.UTF8.GetString(content).Should().NotContain("refresh-token");
    }

    [WindowsFact]
    public async Task SaveAsync_WithNull_ShouldDeleteFile()
    {
        await _uut.SaveAsync("refresh-token", CancellationToken.None);

        await _uut.SaveAsync(null, CancellationToken.None);

        File.Exists(_uut.Path).Should().BeFalse();
    }

    [WindowsFact]
    public async Task LoadAsync_WithCorruptFile_ShouldReturnNull()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_uut.Path, "not encrypted");

        var token = await _uut.LoadAsync(CancellationToken.None);

        token.Should().BeNull();
    }

    [Fact]
    public void ForOptions_WithDifferentServers_ShouldUseDifferentFiles()
    {
        var first = FileTokenCache.ForOptions(new AbacusOptions { BaseUrl = new Uri("https://a.test"), Mandant = 1, ClientId = "client" });
        var second = FileTokenCache.ForOptions(new AbacusOptions { BaseUrl = new Uri("https://b.test"), Mandant = 1, ClientId = "client" });

        first.Path.Should().NotBe(second.Path);
    }

    [Fact]
    public void ForOptions_ShouldUseLocalApplicationData()
    {
        var cache = FileTokenCache.ForOptions(new AbacusOptions { BaseUrl = new Uri("https://a.test"), Mandant = 1, ClientId = "client" });

        cache.Path.Should().StartWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pgarcia.AbacusApi"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
