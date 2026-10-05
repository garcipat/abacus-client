using AwesomeAssertions;
using Garcipat.AbacusApi.Client;

namespace Garcipat.AbacusApi.Tests.Client;

public class AbacusOptionsTests
{
    [Theory]
    [InlineData("https://abacus.test")]
    [InlineData("https://abacus.test/")]
    public void GetEntityBaseAddress_WithServerRoot_ShouldAppendMandantPath(string baseUrl)
    {
        var options = new AbacusOptions { BaseUrl = new Uri(baseUrl), Mandant = 7777 };

        options.GetEntityBaseAddress().AbsoluteUri.Should().Be("https://abacus.test/api/entity/v1/mandants/7777/");
    }

    [Theory]
    [InlineData("https://abacus.test/abacus")]
    [InlineData("https://abacus.test/abacus/")]
    public void GetEntityBaseAddress_WithPath_ShouldKeepPath(string baseUrl)
    {
        var options = new AbacusOptions { BaseUrl = new Uri(baseUrl), Mandant = 7777 };

        options.GetEntityBaseAddress().AbsoluteUri.Should().Be("https://abacus.test/abacus/api/entity/v1/mandants/7777/");
    }
}
