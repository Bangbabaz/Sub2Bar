using System.Text.Json;
using Sub2Bar.Core.Models;

namespace Sub2Bar.Core.Parsing;

public static class ProfileParser
{
    public static CurrentUser Parse(string json)
    {
        var data = EnvelopeParser.Parse(json).Data;
        var user = JsonValue.TryGet(data, out var nested, "user", "profile") &&
                   nested.ValueKind == JsonValueKind.Object
            ? nested
            : data;

        var username = FirstNotEmpty(
            JsonValue.String(user, "username", "user_name", "name"),
            JsonValue.String(user, "email"),
            "用户");
        var role = JsonValue.String(user, "role", "user_role", "type");
        if (string.IsNullOrWhiteSpace(role) && JsonValue.Bool(user, "is_admin", "isAdmin") == true)
        {
            role = "admin";
        }

        return new CurrentUser(JsonValue.Long(user, "id", "user_id", "userId"), username, role);
    }

    private static string FirstNotEmpty(params string[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value));
}

