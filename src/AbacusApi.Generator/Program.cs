using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pgarcia.AbacusApi.Generator;

/// <summary>
/// Runs after each build of this project (the <c>GenerateAbacusClient</c> target in the csproj, which also holds the settings):
/// <list type="number">
/// <item>If the full download <c>abacus-{release}.json</c> exists: trim and patch it into <c>abacus-{release}.trimmed.json</c>.</item>
/// <item>Generate the client from the trimmed document into <c>--output</c>.</item>
/// </list>
/// </summary>
internal static class Program
{
    private static readonly string[] RequiredOptions = ["release", "openapi-directory", "entity-sets", "namespace", "class-name", "output"];

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private static async Task<int> Main(string[] args)
    {
        var options = ParseOptions(args);
        var missing = RequiredOptions.Where(name => !options.ContainsKey(name)).ToList();
        if (missing.Count > 0)
        {
            Console.Error.WriteLine($"Missing options: {string.Join(", ", missing.Select(name => "--" + name))}. Build the generator project instead of running it directly; the settings are in its csproj.");
            return 1;
        }

        var release = options["release"];
        var fullPath = Path.Combine(options["openapi-directory"], $"abacus-{release}.json");
        var trimmedPath = Path.Combine(options["openapi-directory"], $"abacus-{release}.trimmed.json");

        if (File.Exists(fullPath))
        {
            Console.WriteLine($"Trimming {fullPath}");
            await using var stream = File.OpenRead(fullPath);
            var document = (await JsonNode.ParseAsync(stream))!.AsObject();
            var entitySets = options["entity-sets"].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var trimmed = OpenApiTrimmer.Trim(document, entitySets);
            OpenApiPatcher.CollapseNumericStringUnions(trimmed);
            OpenApiPatcher.ReplaceStatusCodeRanges(trimmed);
            OpenApiPatcher.SimplifyQueryOptions(trimmed);
            OpenApiPatcher.MakeUpdateSchemasOptional(trimmed);
            await File.WriteAllTextAsync(trimmedPath, trimmed.ToJsonString(WriteOptions) + Environment.NewLine);
        }
        else if (!File.Exists(trimmedPath))
        {
            Console.Error.WriteLine($"Neither {fullPath} nor {trimmedPath} exists. Download the document first (see README).");
            return 1;
        }

        Console.WriteLine($"Generating from {trimmedPath}");
        var code = await ClientGenerator.GenerateAsync(await File.ReadAllTextAsync(trimmedPath), options["namespace"], options["class-name"]);

        var outputPath = Path.GetFullPath(options["output"]);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, code);
        Console.WriteLine($"Wrote {outputPath}");

        return 0;
    }

    /// <summary><c>--name value</c> pairs.</summary>
    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>();
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
                options[args[i][2..]] = args[i + 1];
        }

        return options;
    }
}
