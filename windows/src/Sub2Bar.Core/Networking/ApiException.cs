using System.Net;

namespace Sub2Bar.Core.Networking;

public enum ApiErrorKind
{
    Network,
    Http,
    InvalidResponse,
    CredentialRejected,
    SessionBindingMismatch
}

public sealed class ApiException : Exception
{
    public ApiException(
        ApiErrorKind kind,
        string message,
        HttpStatusCode? statusCode = null,
        string? apiCode = null,
        string? responseBody = null,
        string? requestPath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
        ApiCode = apiCode;
        ResponseBody = responseBody;
        RequestPath = requestPath;
    }

    public ApiErrorKind Kind { get; }
    public HttpStatusCode? StatusCode { get; }
    public string? ApiCode { get; }
    public string? ResponseBody { get; }
    public string? RequestPath { get; }
    public bool IsCredentialRejected => Kind is ApiErrorKind.CredentialRejected or ApiErrorKind.SessionBindingMismatch;
}
