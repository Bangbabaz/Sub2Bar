using System.Net;

namespace Sub2Bar.Core.Networking;

public static class ApiErrorClassifier
{
    private static readonly string[] CredentialTerms =
    [
        "TOKEN", "AUTH", "SESSION", "EXPIRED", "INVALID", "REVOKED", "UNAUTHORIZED", "MISMATCH"
    ];

    public static ApiErrorKind Classify(HttpStatusCode? statusCode, string? apiCode)
    {
        var code = apiCode?.Trim() ?? string.Empty;
        if (code.Equals("SESSION_BINDING_MISMATCH", StringComparison.OrdinalIgnoreCase))
        {
            return ApiErrorKind.SessionBindingMismatch;
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiErrorKind.CredentialRejected;
        }

        if (long.TryParse(code, out var numericCode) &&
            (numericCode is 401 or 403 ||
             numericCode is >= 40100 and < 40200 ||
             numericCode is >= 40300 and < 40400))
        {
            return ApiErrorKind.CredentialRejected;
        }

        if (CredentialTerms.Any(term => code.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return ApiErrorKind.CredentialRejected;
        }

        return statusCode.HasValue ? ApiErrorKind.Http : ApiErrorKind.InvalidResponse;
    }
}

