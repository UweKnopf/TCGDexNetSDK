using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace net_sdk.src.models.subs;

public record class CardAttack(string Name, List<string>? Cost = null, string? Effect = null, DamageValue? Damage = null);

[JsonConverter(typeof(DamageValueConverter))]
public readonly record struct DamageValue(int Value, bool IsPlus = false)
{
    public override string ToString() => IsPlus ? $"{Value}+" : Value.ToString();
}

public sealed class DamageValueConverter : JsonConverter<DamageValue>
{
    public override DamageValue Read(ref Utf8JsonReader reader,
                                    Type typeToConvert,
                                    JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return new DamageValue(reader.GetInt32());
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();

            if (!string.IsNullOrWhiteSpace(value))
            {
                var isPlus = value.EndsWith('+');
                var numberPart = isPlus ? value[..^1] : value;

                if (int.TryParse(numberPart, out var damage))
                    return new DamageValue(damage, isPlus);
            }
        }

        throw new JsonException("Damage must be a number or a value such as '70+'.");
    }

    public override void Write(Utf8JsonWriter writer,
                               DamageValue value,
                               JsonSerializerOptions options)
    {
        if (value.IsPlus)
            writer.WriteStringValue($"{value.Value}+");
        else
            writer.WriteNumberValue(value.Value);
    }
}
