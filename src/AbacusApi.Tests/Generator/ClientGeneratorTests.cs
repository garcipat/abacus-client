using AwesomeAssertions;
using Garcipat.AbacusApi.Generator;

namespace Garcipat.AbacusApi.Tests.Generator;

public class ClientGeneratorTests
{
    private const string Document = """
    {
      "openapi": "3.0.0",
      "info": { "title": "test", "version": "" },
      "paths": {
        "/Bookings": {
          "get": {
            "operationId": "ListBookings",
            "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": {
              "type": "object", "properties": { "value": { "type": "array", "items": { "$ref": "#/components/schemas/ns.Booking" } } } } } } } }
          }
        }
      },
      "components": {
        "schemas": {
          "ns.Booking": {
            "type": "object",
            "properties": {
              "EmployeeId": { "type": "integer", "format": "int64", "nullable": true },
              "Quantity": { "type": "number", "format": "decimal", "nullable": true },
              "Date": { "type": "string", "format": "date" },
              "TimeFrom": { "type": "string", "format": "time", "nullable": true }
            }
          }
        }
      }
    }
    """;

    [Fact]
    public async Task GenerateAsync_WithOperationId_ShouldGenerateInterfaceMethod()
    {
        var code = await ClientGenerator.GenerateAsync(Document, "Test.V1", "TestApi");

        code.Should().Contain("public partial interface ITestApi");
        code.Should().Contain("ListBookingsAsync(");
    }

    [Fact]
    public async Task GenerateAsync_WithNumericFormats_ShouldUseLongAndDecimal()
    {
        var code = await ClientGenerator.GenerateAsync(Document, "Test.V1", "TestApi");

        code.Should().Contain("long? EmployeeId");
        code.Should().Contain("decimal? Quantity");
    }

    [Fact]
    public async Task GenerateAsync_WithDateAndTimeFormats_ShouldUseDateOnlyAndTimeOnly()
    {
        var code = await ClientGenerator.GenerateAsync(Document, "Test.V1", "TestApi");

        code.Should().Contain("System.DateOnly Date");
        code.Should().Contain("System.TimeOnly? TimeFrom");
    }

    [Fact]
    public async Task GenerateAsync_ShouldTakeBaseAddressFromHttpClient()
    {
        var code = await ClientGenerator.GenerateAsync(Document, "Test.V1", "TestApi");

        code.Should().Contain("public TestApi(System.Net.Http.HttpClient httpClient)");
        code.Should().NotContain("BaseUrl");
    }

    [Fact]
    public async Task GenerateAsync_ShouldUseSystemTextJson()
    {
        var code = await ClientGenerator.GenerateAsync(Document, "Test.V1", "TestApi");

        code.Should().Contain("System.Text.Json.JsonSerializer");
        code.Should().NotContain("Newtonsoft.Json.JsonSerializer");
    }
}
