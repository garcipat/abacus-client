using System.Text.Json.Nodes;

namespace Pgarcia.AbacusApi.Generator;

/// <summary>Fixes constructs of the Abacus OpenAPI document that NSwag does not map to useful C# types.</summary>
public static class OpenApiPatcher
{
    /// <summary>
    /// OData (IEEE754Compatible) writes int64 and decimal values as <c>anyOf [integer|number, string]</c>,
    /// which NSwag generates as <c>object</c>. Collapses these unions to the numeric type; the
    /// <c>format</c> (int64, decimal) stays and gives <c>long</c>/<c>decimal</c>.
    /// </summary>
    public static void CollapseNumericStringUnions(JsonNode document)
    {
        switch (document)
        {
            case JsonObject obj:
                if (NumericType(obj["anyOf"]) is { } type)
                {
                    obj.Remove("anyOf");
                    obj["type"] = type;
                }

                foreach (var (_, value) in obj)
                {
                    if (value is not null)
                        CollapseNumericStringUnions(value);
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                        CollapseNumericStringUnions(item);
                }
                break;
        }
    }

    /// <summary>
    /// NSwag does not support status code ranges (<c>4XX</c>) and emits <c>status_ == 4XX</c>, which does not compile.
    /// The first range of an operation becomes its <c>default</c> response (NSwag throws <c>ApiException&lt;T&gt;</c> for it);
    /// further ranges, or ranges next to an existing <c>default</c>, are dropped.
    /// </summary>
    public static void ReplaceStatusCodeRanges(JsonObject document)
    {
        foreach (var (_, pathItem) in document["paths"]?.AsObject() ?? [])
        {
            foreach (var (_, operation) in pathItem?.AsObject() ?? [])
            {
                if (operation is JsonObject && operation["responses"] is JsonObject responses && responses.Any(r => IsRange(r.Key)))
                    operation["responses"] = WithoutRanges(responses);
            }
        }
    }

    /// <summary>
    /// <c>$orderby</c>, <c>$select</c> and <c>$expand</c> list every property as an enum (with values like <c>"Id desc"</c>),
    /// which NSwag generates as <c>AnonymousN</c> enums per operation. Plain strings are simpler to use.
    /// </summary>
    public static void SimplifyQueryOptions(JsonObject document)
    {
        foreach (var (_, pathItem) in document["paths"]?.AsObject() ?? [])
        {
            if (pathItem is not JsonObject item)
                continue;

            SimplifyQueryOptionParameters(item["parameters"]);
            foreach (var (_, operation) in item)
            {
                if (operation is JsonObject)
                    SimplifyQueryOptionParameters(operation["parameters"]);
            }
        }
    }

    private static void SimplifyQueryOptionParameters(JsonNode? parameters)
    {
        if (parameters is not JsonArray array)
            return;

        foreach (var parameter in array.OfType<JsonObject>())
        {
            var name = parameter["name"]?.GetValue<string>();
            if (name is "$orderby" or "$select" or "$expand" && parameter["schema"]?["items"] is JsonObject items && items.ContainsKey("enum"))
                parameter["schema"]!["items"] = new JsonObject { ["type"] = "string" };
        }
    }

    /// <summary>
    /// The <c>-update</c> schemas (PATCH bodies) keep the entity's <c>required</c> list and non-nullable types, so NSwag
    /// generates e.g. <c>DateOnly Date</c>, and every PATCH would send <c>"Date":"0001-01-01"</c>. In a PATCH every
    /// property is optional: drop <c>required</c> and make each property nullable (a plain <c>$ref</c> is wrapped in
    /// <c>allOf</c>, because OpenAPI 3.0 ignores siblings of <c>$ref</c>).
    /// </summary>
    public static void MakeUpdateSchemasOptional(JsonObject document)
    {
        foreach (var (name, schema) in document["components"]?["schemas"]?.AsObject() ?? [])
        {
            if (!name.EndsWith("-update", StringComparison.Ordinal) || schema is not JsonObject update)
                continue;

            update.Remove("required");
            if (update["properties"] is not JsonObject properties)
                continue;

            foreach (var propertyName in properties.Select(p => p.Key).ToList())
            {
                if (properties[propertyName] is not JsonObject property)
                    continue;

                if (property.Count == 1 && property["$ref"] is not null)
                    properties[propertyName] = new JsonObject { ["allOf"] = new JsonArray(property.DeepClone()), ["nullable"] = true };
                else
                    property["nullable"] = true;
            }
        }
    }

    private static JsonObject WithoutRanges(JsonObject responses)
    {
        var hasDefault = responses.ContainsKey("default");
        var result = new JsonObject();
        foreach (var (status, response) in responses)
        {
            var key = status;
            if (IsRange(status))
            {
                if (hasDefault)
                    continue;

                key = "default";
                hasDefault = true;
            }

            result[key] = response?.DeepClone();
        }

        return result;
    }

    private static bool IsRange(string status) =>
        status.Length == 3 && status[0] is >= '1' and <= '5' && status.EndsWith("XX", StringComparison.OrdinalIgnoreCase);

    private static string? NumericType(JsonNode? anyOf)
    {
        if (anyOf is not JsonArray { Count: 2 } options)
            return null;

        var types = options.Select(option => option is JsonObject { Count: 1 } o ? o["type"]?.GetValue<string>() : null).ToList();
        var numeric = types.FirstOrDefault(type => type is "integer" or "number");
        return numeric is not null && types.Contains("string") ? numeric : null;
    }
}
