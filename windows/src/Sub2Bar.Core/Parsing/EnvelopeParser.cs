using System.Net;
using System.Text.Json;
using Sub2Bar.Core.Networking;

namespace Sub2Bar.Core.Parsing;

public sealed record ParsedEnvelope(string? Code, string? Message, JsonElement Data);

public static class EnvelopeParser
{
    public static ParsedEnvelope Parse(string json, HttpStatusCode? statusCode = null)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var code = JsonValue.TryGet(root, out var codeElement, "code")
                ? JsonValue.AsString(codeElement)
                : null;
            var message = JsonValue.String(root, "message", "detail", "error");

            var httpFailure = statusCode.HasValue &&
                              ((int)statusCode.Value < 200 || (int)statusCode.Value >= 300);
            if (IsFailureCode(code) || httpFailure)
            {
                var kind = ApiErrorClassifier.Classify(statusCode, code);
                var fallback = statusCode.HasValue
                    ? $"服务器返回 HTTP {(int)statusCode.Value}。"
                    : "服务器返回了错误。";
                throw new ApiException(kind, string.IsNullOrWhiteSpace(message) ? fallback : message,
                    statusCode, code, json);
            }

            var data = JsonValue.TryGet(root, out var wrapped, "data") ? wrapped : root;
            return new ParsedEnvelope(code, message, data.Clone());
        }
        catch (ApiException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new ApiException(ApiErrorKind.InvalidResponse, "服务器返回的 JSON 无法解析。",
                statusCode, responseBody: json, innerException: exception);
        }
    }

    private static bool IsFailureCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return !code.Equals("0", StringComparison.OrdinalIgnoreCase) &&
               !code.Equals("success", StringComparison.OrdinalIgnoreCase) &&
               !code.Equals("ok", StringComparison.OrdinalIgnoreCase);
    }
}
