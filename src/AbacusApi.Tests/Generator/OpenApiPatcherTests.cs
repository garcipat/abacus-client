using System.Text.Json.Nodes;
using AwesomeAssertions;
using Garcipat.AbacusApi.Generator;

namespace Garcipat.AbacusApi.Tests.Generator;

public class OpenApiPatcherTests
{
    [Theory]
    [InlineData("integer", "int64")]
    [InlineData("number", "decimal")]
    public void CollapseNumericStringUnions_WithNumericOrString_ShouldBecomeNumeric(string type, string format)
    {
        var property = Property($$"""{ "anyOf": [ { "type": "{{type}}" }, { "type": "string" } ], "format": "{{format}}", "nullable": true }""");

        OpenApiPatcher.CollapseNumericStringUnions(property);

        property.ToJsonString().Should().Be($$"""{"format":"{{format}}","nullable":true,"type":"{{type}}"}""");
    }

    [Fact]
    public void CollapseNumericStringUnions_WithOtherUnion_ShouldNotChange()
    {
        var property = Property("""{ "anyOf": [ { "$ref": "#/components/schemas/a" }, { "type": "string" } ] }""");
        var before = property.ToJsonString();

        OpenApiPatcher.CollapseNumericStringUnions(property);

        property.ToJsonString().Should().Be(before);
    }

    [Fact]
    public void CollapseNumericStringUnions_WithNestedUnions_ShouldCollapseAll()
    {
        var document = Property("""
        {
          "properties": {
            "Id": { "anyOf": [ { "type": "integer" }, { "type": "string" } ], "format": "int64" },
            "Values": { "type": "array", "items": { "anyOf": [ { "type": "number" }, { "type": "string" } ], "format": "decimal" } }
          }
        }
        """);

        OpenApiPatcher.CollapseNumericStringUnions(document);

        document["properties"]!["Id"]!["type"]!.GetValue<string>().Should().Be("integer");
        document["properties"]!["Values"]!["items"]!["type"]!.GetValue<string>().Should().Be("number");
    }

    [Fact]
    public void ReplaceStatusCodeRanges_WithRange_ShouldBecomeDefault()
    {
        var document = Property("""
        { "paths": { "/Bookings": {
          "parameters": [ { "name": "Id", "in": "path" } ],
          "get": { "responses": {
            "200": { "description": "ok" },
            "4XX": { "$ref": "#/components/responses/error" }
          } } } } }
        """);

        OpenApiPatcher.ReplaceStatusCodeRanges(document);

        Responses(document).Should().Equal("200", "default");
        document["paths"]!["/Bookings"]!["get"]!["responses"]!["default"]!["$ref"]!.GetValue<string>().Should().Be("#/components/responses/error");
    }

    [Fact]
    public void ReplaceStatusCodeRanges_WithExistingDefault_ShouldDropRange()
    {
        var document = Property("""
        { "paths": { "/Bookings": { "get": { "responses": {
          "200": { "description": "ok" },
          "4XX": { "description": "client error" },
          "default": { "description": "error" }
        } } } } }
        """);

        OpenApiPatcher.ReplaceStatusCodeRanges(document);

        Responses(document).Should().Equal("200", "default");
        document["paths"]!["/Bookings"]!["get"]!["responses"]!["default"]!["description"]!.GetValue<string>().Should().Be("error");
    }

    [Theory]
    [InlineData("$orderby")]
    [InlineData("$select")]
    [InlineData("$expand")]
    public void SimplifyQueryOptions_WithEnumArray_ShouldBecomeStringArray(string name)
    {
        var document = Property($$"""
        { "paths": { "/Bookings": { "get": { "parameters": [
          { "name": "{{name}}", "in": "query", "explode": false, "schema": { "type": "array", "uniqueItems": true, "items": { "type": "string", "enum": [ "Id", "Id desc" ] } } }
        ] } } } }
        """);

        OpenApiPatcher.SimplifyQueryOptions(document);

        document["paths"]!["/Bookings"]!["get"]!["parameters"]![0]!["schema"]!["items"]!.ToJsonString().Should().Be("""{"type":"string"}""");
    }

    [Fact]
    public void SimplifyQueryOptions_WithOtherParameter_ShouldNotChange()
    {
        var document = Property("""
        { "paths": { "/Bookings": { "get": { "parameters": [
          { "name": "kind", "in": "query", "schema": { "type": "array", "items": { "type": "string", "enum": [ "A", "B" ] } } }
        ] } } } }
        """);
        var before = document.ToJsonString();

        OpenApiPatcher.SimplifyQueryOptions(document);

        document.ToJsonString().Should().Be(before);
    }

    [Fact]
    public void MakeUpdateSchemasOptional_ShouldMakeAllPropertiesNullable()
    {
        var document = UpdateDocument();

        OpenApiPatcher.MakeUpdateSchemasOptional(document);

        var properties = document["components"]!["schemas"]!["ns.Booking-update"]!["properties"]!;
        properties["Date"]!["nullable"]!.GetValue<bool>().Should().BeTrue();
        properties["Values"]!["nullable"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void MakeUpdateSchemasOptional_ShouldRemoveRequired()
    {
        var document = UpdateDocument();

        OpenApiPatcher.MakeUpdateSchemasOptional(document);

        document["components"]!["schemas"]!["ns.Booking-update"]!.AsObject().ContainsKey("required").Should().BeFalse();
    }

    [Fact]
    public void MakeUpdateSchemasOptional_WithPlainReference_ShouldWrapInAllOf()
    {
        var document = UpdateDocument();

        OpenApiPatcher.MakeUpdateSchemasOptional(document);

        document["components"]!["schemas"]!["ns.Booking-update"]!["properties"]!["Employee"]!.ToJsonString()
            .Should().Be("""{"allOf":[{"$ref":"#/components/schemas/ns.Employee"}],"nullable":true}""");
    }

    [Fact]
    public void MakeUpdateSchemasOptional_ShouldNotChangeOtherSchemas()
    {
        var document = UpdateDocument();
        var before = document["components"]!["schemas"]!["ns.Booking"]!.ToJsonString();

        OpenApiPatcher.MakeUpdateSchemasOptional(document);

        document["components"]!["schemas"]!["ns.Booking"]!.ToJsonString().Should().Be(before);
    }

    private static JsonObject UpdateDocument() => Property("""
        { "components": { "schemas": {
          "ns.Booking": { "type": "object", "required": [ "Date" ], "properties": { "Date": { "type": "string", "format": "date" } } },
          "ns.Booking-update": {
            "type": "object",
            "required": [ "Date" ],
            "properties": {
              "Date": { "type": "string", "format": "date" },
              "Values": { "nullable": true, "allOf": [ { "$ref": "#/components/schemas/ns.Values-update" } ] },
              "Employee": { "$ref": "#/components/schemas/ns.Employee" }
            }
          }
        } } }
        """);

    private static IEnumerable<string> Responses(JsonObject document) =>
        document["paths"]!["/Bookings"]!["get"]!["responses"]!.AsObject().Select(r => r.Key);

    private static JsonObject Property(string json) => JsonNode.Parse(json)!.AsObject();
}
