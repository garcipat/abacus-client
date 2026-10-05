using System.Text.Json.Nodes;
using AwesomeAssertions;
using Garcipat.AbacusApi.Generator;

namespace Garcipat.AbacusApi.Tests.Generator;

public class OpenApiTrimmerTests
{
    private readonly JsonObject _document;

    public OpenApiTrimmerTests()
    {
        _document = CreateDocument();
    }

    [Fact]
    public void Trim_WithEntitySet_ShouldKeepItsCollectionAndKeyPaths()
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings"]);

        Paths(result).Should().BeEquivalentTo("/Bookings", "/Bookings({Id})");
    }

    [Fact]
    public void Trim_WithSeveralEntitySets_ShouldKeepPathsOfAll()
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings", "Projects"]);

        Paths(result).Should().BeEquivalentTo("/Bookings", "/Bookings({Id})", "/Projects", "/Projects({Id})");
    }

    [Fact]
    public void Trim_WithEntitySet_ShouldKeepReferencedSchemas()
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings"]);

        Schemas(result).Should().BeEquivalentTo("ns.Booking", "ns.BookingCollection", "ns.Booking-create", "ns.Values", "count");
    }

    [Fact]
    public void Trim_WithEntitySet_ShouldMoveCollectionResponseToNamedSchema()
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings"]);

        var schema = result["paths"]!["/Bookings"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;
        schema["$ref"]!.GetValue<string>().Should().Be("#/components/schemas/ns.BookingCollection");
        result["components"]!["schemas"]!["ns.BookingCollection"]!["properties"]!["value"].Should().NotBeNull();
    }

    [Fact]
    public void Trim_WithEntitySet_ShouldKeepReferencedParametersAndResponses()
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings"]);

        result["components"]!["parameters"]!.AsObject().Select(p => p.Key).Should().BeEquivalentTo("top");
        result["components"]!["responses"]!.AsObject().Select(p => p.Key).Should().BeEquivalentTo("error");
    }

    [Fact]
    public void Trim_WithNavigationToTrimmedEntity_ShouldRemoveProperty()
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings"]);

        Properties(result, "ns.Booking").Should().BeEquivalentTo("Id", "Values");
    }

    [Fact]
    public void Trim_WithNavigationToKeptEntity_ShouldKeepProperty()
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings", "Projects"]);

        Properties(result, "ns.Booking").Should().BeEquivalentTo("Id", "Values", "Project");
    }

    [Fact]
    public void Trim_WithNavigationInCreateSchema_ShouldRemoveProperty()
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings"]);

        Properties(result, "ns.Booking-create").Should().BeEquivalentTo("Id");
    }

    [Theory]
    [InlineData("/Bookings", "get", "ListBookings")]
    [InlineData("/Bookings", "post", "CreateBooking")]
    [InlineData("/Bookings({Id})", "get", "GetBooking")]
    [InlineData("/Bookings({Id})", "patch", "UpdateBooking")]
    [InlineData("/Bookings({Id})", "delete", "DeleteBooking")]
    public void Trim_WithEntitySet_ShouldSetOperationIds(string path, string method, string expected)
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings"]);

        result["paths"]![path]![method]!["operationId"]!.GetValue<string>().Should().Be(expected);
    }

    [Fact]
    public void Trim_WithEntitySet_ShouldDropInfoDescription()
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings"]);

        result["info"]!.AsObject().ContainsKey("description").Should().BeFalse();
    }

    [Fact]
    public void Trim_WithEntitySet_ShouldKeepTagsOfKeptEntitySetsOnly()
    {
        var result = OpenApiTrimmer.Trim(_document, ["Bookings"]);

        result["tags"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()).Should().BeEquivalentTo("Bookings");
    }

    [Fact]
    public void Trim_WithEntitySet_ShouldNotChangeInputDocument()
    {
        var before = _document.ToJsonString();

        OpenApiTrimmer.Trim(_document, ["Bookings"]);

        _document.ToJsonString().Should().Be(before);
    }

    [Fact]
    public void Trim_WithUnknownEntitySet_ShouldThrow()
    {
        var act = () => OpenApiTrimmer.Trim(_document, ["Unknown"]);

        act.Should().Throw<ArgumentException>();
    }

    private static IEnumerable<string> Paths(JsonObject document) =>
        document["paths"]!.AsObject().Select(p => p.Key);

    private static IEnumerable<string> Schemas(JsonObject document) =>
        document["components"]!["schemas"]!.AsObject().Select(s => s.Key);

    private static IEnumerable<string> Properties(JsonObject document, string schema) =>
        document["components"]!["schemas"]![schema]!["properties"]!.AsObject().Select(p => p.Key);

    private static JsonObject CreateDocument() =>
        JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "V 2026.201", "version": "", "description": "huge diagram" },
          "servers": [ { "url": "https://services.OData.org/service-root" } ],
          "tags": [ { "name": "Bookings" }, { "name": "Projects" }, { "name": "Customers" }, { "name": "Documents" } ],
          "paths": {
            "/Bookings": {
              "get": {
                "tags": [ "Bookings" ],
                "parameters": [ { "$ref": "#/components/parameters/top" } ],
                "responses": {
                  "200": { "content": { "application/json": { "schema": {
                    "type": "object",
                    "properties": {
                      "@odata.count": { "$ref": "#/components/schemas/count" },
                      "value": { "type": "array", "items": { "$ref": "#/components/schemas/ns.Booking" } }
                    } } } } },
                  "4XX": { "$ref": "#/components/responses/error" }
                }
              },
              "post": {
                "tags": [ "Bookings" ],
                "requestBody": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ns.Booking-create" } } } },
                "responses": { "201": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ns.Booking" } } } } }
              }
            },
            "/Bookings({Id})": {
              "parameters": [ { "name": "Id", "in": "path", "required": true, "schema": { "type": "string", "format": "uuid" } } ],
              "get": { "tags": [ "Bookings" ], "responses": { "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ns.Booking" } } } } } },
              "patch": { "tags": [ "Bookings" ], "responses": { "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ns.Booking" } } } } } },
              "delete": { "tags": [ "Bookings" ], "responses": { "204": { "description": "Success" } } }
            },
            "/Bookings({Id})/Project": {
              "get": { "tags": [ "Bookings" ], "responses": { "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ns.Project" } } } } } }
            },
            "/Projects": {
              "get": { "tags": [ "Projects" ], "responses": { "200": { "content": { "application/json": { "schema": {
                "type": "object", "properties": { "value": { "type": "array", "items": { "$ref": "#/components/schemas/ns.Project" } } } } } } } } }
            },
            "/Projects({Id})": {
              "get": { "tags": [ "Projects" ], "responses": { "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ns.Project" } } } } } }
            },
            "/Customers": {
              "get": { "tags": [ "Customers" ], "responses": { "200": { "content": { "application/json": { "schema": {
                "type": "object", "properties": { "value": { "type": "array", "items": { "$ref": "#/components/schemas/ns.Customer" } } } } } } } } }
            },
            "/Documents": {
              "get": { "tags": [ "Documents" ], "responses": { "200": { "content": { "application/json": { "schema": {
                "type": "object", "properties": { "value": { "type": "array", "items": { "$ref": "#/components/schemas/ns.Document" } } } } } } } } }
            }
          },
          "components": {
            "schemas": {
              "count": { "type": "integer" },
              "ns.Booking": {
                "type": "object",
                "properties": {
                  "Id": { "type": "string", "format": "uuid" },
                  "Values": { "nullable": true, "allOf": [ { "$ref": "#/components/schemas/ns.Values" } ] },
                  "Project": { "nullable": true, "allOf": [ { "$ref": "#/components/schemas/ns.Project" } ] },
                  "Customer": { "$ref": "#/components/schemas/ns.Customer" },
                  "Documents": { "type": "array", "items": { "$ref": "#/components/schemas/ns.Document" } }
                }
              },
              "ns.Booking-create": {
                "type": "object",
                "properties": {
                  "Id": { "type": "string", "format": "uuid" },
                  "Customer": { "$ref": "#/components/schemas/ns.Customer-create" }
                }
              },
              "ns.Values": { "type": "object", "properties": { "Quantity": { "type": "number" } } },
              "ns.Project": { "type": "object", "properties": { "Id": { "type": "integer" }, "Customer": { "$ref": "#/components/schemas/ns.Customer" } } },
              "ns.Customer": { "type": "object", "properties": { "Id": { "type": "integer" } } },
              "ns.Customer-create": { "type": "object", "properties": { "Id": { "type": "integer" } } },
              "ns.Document": { "type": "object", "properties": { "Id": { "type": "string" } } },
              "ns.Unused": { "type": "object", "properties": { "Id": { "type": "string" } } }
            },
            "parameters": {
              "top": { "name": "$top", "in": "query", "schema": { "type": "integer" } },
              "skip": { "name": "$skip", "in": "query", "schema": { "type": "integer" } }
            },
            "responses": {
              "error": { "description": "Error" },
              "other": { "description": "Other" }
            }
          }
        }
        """)!.AsObject();
}
