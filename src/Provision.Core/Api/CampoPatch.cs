using System.Text.Json;
using System.Text.Json.Serialization;

namespace Provision.Core.Api;

/// <summary>default omite el campo; una asignación explícita, incluso null, lo envía.</summary>
[JsonConverter(typeof(CampoPatchConverter))]
public readonly record struct CampoPatch<T>(bool Presente, T Valor)
{
    public static implicit operator CampoPatch<T>(T valor) => new(true, valor);
}

public sealed class CampoPatchConverter : JsonConverterFactory
{
    public override bool CanConvert(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(CampoPatch<>);

    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(type.GetGenericArguments()))!;

    private sealed class Converter<T> : JsonConverter<CampoPatch<T>>
    {
        public override CampoPatch<T> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            new(true, JsonSerializer.Deserialize<T>(ref reader, options)!);

        public override void Write(Utf8JsonWriter writer, CampoPatch<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Valor, options);
    }
}
