using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BeepsOnBot.Database.Infrastructure;

public class JsonValueComparer<TData> : ValueComparer<TData>
    where TData : class?
{
    public JsonValueComparer()
        : base(
            (left, right) => JsonSerializer.Serialize(left, JsonSerializerSettings) ==
                             JsonSerializer.Serialize(right, JsonSerializerSettings),
            value => value == null ? 0 : JsonSerializer.Serialize(value, JsonSerializerSettings).GetHashCode(),
            value => JsonSerializer.Deserialize<TData>(JsonSerializer.Serialize(value, JsonSerializerSettings),
                JsonSerializerSettings)!)
    {
    }

    private static readonly JsonSerializerOptions JsonSerializerSettings =
        new() {DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull};
}