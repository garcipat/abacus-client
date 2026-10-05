using System.Text.Json.Nodes;

namespace Garcipat.AbacusApi.Generator;

/// <summary>
/// Reduces the Abacus OpenAPI document (all entity sets of a release) to the given entity sets:
/// their collection and by-key paths, the components those reference, and readable operation ids.
/// Navigation properties to entity types that are not kept are removed, otherwise they would pull in
/// most of the document.
/// </summary>
public static class OpenApiTrimmer
{
    private const string ComponentsPrefix = "#/components/";

    public static JsonObject Trim(JsonObject document, IReadOnlyCollection<string> entitySets)
    {
        var entityTypes = FindEntityTypes(document);
        var unknown = entitySets.Where(set => !entityTypes.ContainsKey(set)).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"Unknown entity sets: {string.Join(", ", unknown)}", nameof(entitySets));

        var keptTypes = entitySets.Select(set => entityTypes[set]).ToHashSet();
        var trimmedTypes = entityTypes.Values.Where(type => !keptTypes.Contains(type)).ToHashSet();

        var paths = TrimPaths(document, entitySets, entityTypes);
        var collections = ExtractCollectionSchemas(paths, entitySets, entityTypes);
        var components = TrimComponents(document, paths, collections, trimmedTypes);

        return new JsonObject
        {
            ["openapi"] = document["openapi"]?.DeepClone(),
            ["info"] = TrimInfo(document),
            ["servers"] = document["servers"]?.DeepClone(),
            ["tags"] = TrimTags(document, entitySets),
            ["paths"] = paths,
            ["components"] = components,
        };
    }

    /// <summary>Entity set name → entity type schema name, read from each collection GET's <c>value</c> items.</summary>
    private static Dictionary<string, string> FindEntityTypes(JsonObject document)
    {
        var entityTypes = new Dictionary<string, string>();
        foreach (var (path, item) in document["paths"]!.AsObject())
        {
            if (path.IndexOfAny(['(', '/'], 1) >= 0)
                continue;

            var reference = item?["get"]?["responses"]?["200"]?["content"]?["application/json"]?["schema"]?["properties"]?["value"]?["items"]?["$ref"];
            if (reference is not null)
                entityTypes[path[1..]] = ComponentName(reference.GetValue<string>());
        }

        return entityTypes;
    }

    private static JsonObject TrimPaths(JsonObject document, IReadOnlyCollection<string> entitySets, Dictionary<string, string> entityTypes)
    {
        var source = document["paths"]!.AsObject();
        var paths = new JsonObject();
        foreach (var set in entitySets)
        {
            var type = ShortName(entityTypes[set]);
            AddPath(source, paths, $"/{set}", new() { ["get"] = $"List{set}", ["post"] = $"Create{type}" });
            AddPath(source, paths, $"/{set}({{Id}})", new() { ["get"] = $"Get{type}", ["patch"] = $"Update{type}", ["delete"] = $"Delete{type}" });
        }

        return paths;
    }

    private static void AddPath(JsonObject source, JsonObject paths, string path, Dictionary<string, string> operationIds)
    {
        if (source[path] is not JsonObject item)
            return;

        var clone = item.DeepClone().AsObject();
        foreach (var (method, operationId) in operationIds)
        {
            if (clone[method] is JsonObject operation)
                operation["operationId"] = operationId;
        }

        paths[path] = clone;
    }

    /// <summary>
    /// Moves the inline collection response of each <c>GET /{set}</c> into a named schema <c>{type}Collection</c>,
    /// otherwise NSwag names them <c>Response</c>, <c>Response2</c>, …
    /// </summary>
    private static Dictionary<string, JsonNode> ExtractCollectionSchemas(JsonObject paths, IReadOnlyCollection<string> entitySets, Dictionary<string, string> entityTypes)
    {
        var collections = new Dictionary<string, JsonNode>();
        foreach (var set in entitySets)
        {
            var content = paths[$"/{set}"]?["get"]?["responses"]?["200"]?["content"]?["application/json"];
            if (content?["schema"] is not JsonObject schema || schema.ContainsKey("$ref"))
                continue;

            var name = $"{entityTypes[set]}Collection";
            schema.Remove("title");
            content["schema"] = new JsonObject { ["$ref"] = $"{ComponentsPrefix}schemas/{name}" };
            collections[name] = schema;
        }

        return collections;
    }

    private static JsonObject TrimComponents(JsonObject document, JsonObject paths, Dictionary<string, JsonNode> collections, HashSet<string> trimmedTypes)
    {
        var source = document["components"]!.AsObject();
        var kept = new Dictionary<string, JsonNode>();
        var pending = new Stack<string>(References(paths));

        while (pending.TryPop(out var reference))
        {
            if (kept.ContainsKey(reference) || !reference.StartsWith(ComponentsPrefix, StringComparison.Ordinal))
                continue;

            var parts = reference[ComponentsPrefix.Length..].Split('/', 2);
            var component = parts[0] == "schemas" && collections.TryGetValue(parts[1], out var collection)
                ? collection
                : source[parts[0]]?[parts[1]];
            if (component is null)
                continue;

            var clone = component.DeepClone();
            if (parts[0] == "schemas")
                RemoveNavigations(clone, trimmedTypes);

            kept[reference] = clone;
            foreach (var child in References(clone))
                pending.Push(child);
        }

        // Emit in the source order so the trimmed document diffs cleanly between releases.
        var components = new JsonObject();
        foreach (var (section, entries) in source)
        {
            var sectionObject = new JsonObject();
            foreach (var (name, _) in entries!.AsObject())
            {
                if (kept.TryGetValue($"{ComponentsPrefix}{section}/{name}", out var component))
                    sectionObject[name] = component;

                // Extracted collection schemas go right after their entity type.
                if (section == "schemas" && kept.TryGetValue($"{ComponentsPrefix}schemas/{name}Collection", out var collection) && !sectionObject.ContainsKey($"{name}Collection"))
                    sectionObject[$"{name}Collection"] = collection;
            }

            if (sectionObject.Count > 0)
                components[section] = sectionObject;
        }

        return components;
    }

    private static void RemoveNavigations(JsonNode schema, HashSet<string> trimmedTypes)
    {
        if (schema["properties"] is not JsonObject properties)
            return;

        var navigations = properties
            .Where(property => References(property.Value).Any(reference => trimmedTypes.Contains(EntityTypeName(reference))))
            .Select(property => property.Key)
            .ToList();

        foreach (var name in navigations)
            properties.Remove(name);
    }

    private static JsonObject TrimInfo(JsonObject document)
    {
        var info = document["info"]?.DeepClone().AsObject() ?? [];
        info.Remove("description");
        return info;
    }

    private static JsonArray TrimTags(JsonObject document, IReadOnlyCollection<string> entitySets)
    {
        var tags = document["tags"]?.AsArray() ?? [];
        return new JsonArray(tags
            .Where(tag => entitySets.Contains(tag?["name"]?.GetValue<string>()))
            .Select(tag => tag!.DeepClone())
            .ToArray());
    }

    private static IEnumerable<string> References(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "$ref" && value is JsonValue reference)
                        yield return reference.GetValue<string>();
                    else
                        foreach (var child in References(value))
                            yield return child;
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                    foreach (var child in References(item))
                        yield return child;
                break;
        }
    }

    private static string ComponentName(string reference) => reference[(reference.LastIndexOf('/') + 1)..];

    /// <summary><c>#/components/schemas/ns.Customer-create</c> → <c>ns.Customer</c>.</summary>
    private static string EntityTypeName(string reference)
    {
        var name = ComponentName(reference);
        var dash = name.LastIndexOf('-');
        return dash > 0 ? name[..dash] : name;
    }

    /// <summary><c>ch.abacus.proj.InAndOut</c> → <c>InAndOut</c>.</summary>
    private static string ShortName(string schemaName) => schemaName[(schemaName.LastIndexOf('.') + 1)..];
}
