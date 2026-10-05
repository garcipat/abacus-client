using NJsonSchema.CodeGeneration.CSharp;
using NSwag;
using NSwag.CodeGeneration.CSharp;
using NSwag.CodeGeneration.OperationNameGenerators;

namespace Pgarcia.AbacusApi.Generator;

public static class ClientGenerator
{
    public static async Task<string> GenerateAsync(string openApiJson, string nameSpace, string className)
    {
        var document = await OpenApiDocument.FromJsonAsync(openApiJson);
        var settings = new CSharpClientGeneratorSettings
        {
            ClassName = className,
            GenerateClientInterfaces = true,
            GenerateOptionalParameters = true,
            // The HttpClient from IHttpClientFactory carries the configured base address.
            UseBaseUrl = false,
            InjectHttpClient = true,
            OperationNameGenerator = new SingleClientFromOperationIdOperationNameGenerator(),
            CSharpGeneratorSettings =
            {
                Namespace = nameSpace,
                JsonLibrary = CSharpJsonLibrary.SystemTextJson,
                GenerateNullableReferenceTypes = true,
                // Abacus sends "2026-10-05" and "08:12:00"; DateTimeOffset/TimeSpan would not round-trip those.
                DateType = "System.DateOnly",
                TimeType = "System.TimeOnly",
            },
        };

        return new CSharpClientGenerator(document, settings).GenerateFile();
    }
}
