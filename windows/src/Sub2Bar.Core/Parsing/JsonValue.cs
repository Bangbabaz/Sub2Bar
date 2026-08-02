using System.Globalization;
using System.Text.Json;

namespace Sub2Bar.Core.Parsing;

internal static class JsonValue
{
    public static bool TryGet(JsonElement element, out JsonElement value, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public static JsonElement Get(JsonElement element, params string[] names) =>
        TryGet(element, out var value, names) ? value : default;

    public static string? AsString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    public static string String(JsonElement element, params string[] names) =>
        TryGet(element, out var value, names) ? AsString(value) ?? string.Empty : string.Empty;

    public static double? Double(JsonElement element, params string[] names) =>
        TryGet(element, out var value, names) ? AsDouble(value) : null;

    public static double? AsDouble(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    public static long? Long(JsonElement element, params string[] names)
    {
        if (!TryGet(element, out var value, names))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return long.TryParse(AsString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    public static int? Int(JsonElement element, params string[] names)
    {
        var value = Long(element, names);
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }

    public static bool? Bool(JsonElement element, params string[] names)
    {
        if (!TryGet(element, out var value, names))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        var text = AsString(value);
        if (bool.TryParse(text, out var result))
        {
            return result;
        }

        return text switch
        {
            "1" => true,
            "0" => false,
            _ => null
        };
    }

    public static DateTimeOffset? Date(JsonElement element, params string[] names)
    {
        if (!TryGet(element, out var value, names))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var result))
        {
            return result;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unixSeconds))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return null;
    }

    public static IEnumerable<JsonElement> ArrayOrSingle(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().ToArray();
        }

        return value.ValueKind == JsonValueKind.Object ? [value] : [];
    }
}

