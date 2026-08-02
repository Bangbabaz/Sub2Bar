namespace Sub2Bar.Core.Models;

public sealed record Credentials(
    string ServerAddress,
    string AccessToken,
    string RefreshToken,
    string BoundUserAgent)
{
    public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);
}

public sealed record TokenPair(string AccessToken, string RefreshToken);
