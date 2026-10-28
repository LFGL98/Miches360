using System.Text.Json;
using System.Text.Json.Serialization;

namespace Miches360.Servidor;

public sealed class FechaUtcConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var fecha = reader.GetDateTime();
        return fecha.Kind == DateTimeKind.Utc ? fecha.ToLocalTime() : fecha;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
        };
        writer.WriteStringValue(utc);
    }
}
