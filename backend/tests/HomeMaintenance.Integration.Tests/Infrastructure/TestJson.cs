using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeMaintenance.Integration.Tests.Infrastructure;

/// <summary>
/// Mirror of the API's http JSON options (enums as strings) so
/// ReadFromJsonAsync can parse responses like "status": "Active".
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };
}
