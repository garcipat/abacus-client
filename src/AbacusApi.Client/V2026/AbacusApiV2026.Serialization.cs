using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pgarcia.AbacusApi.Client.V2026;

public partial class AbacusApi
{
    /// <summary>
    /// Leave unset properties out of request bodies. OData treats <c>null</c> in a <c>PATCH</c> as "clear this field",
    /// so sending every unset property as <c>null</c> would wipe them.
    /// </summary>
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
        settings.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
}
