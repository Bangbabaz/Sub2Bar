namespace Sub2Bar.Core.Networking;

public static class ServerAddress
{
    public static string Normalize(string value)
    {
        var candidate = value.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new ArgumentException("请输入服务器地址。", nameof(value));
        }

        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = $"http://{candidate}";
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("服务器地址必须是有效的 HTTP 或 HTTPS 地址。", nameof(value));
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty,
            Path = uri.AbsolutePath.TrimEnd('/')
        };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    public static Uri Build(string baseAddress, string relativePath)
    {
        var normalized = Normalize(baseAddress);
        return new Uri($"{normalized}/{relativePath.TrimStart('/')}", UriKind.Absolute);
    }

    public static bool IsSameOrigin(string baseAddress, Uri candidate)
    {
        var expected = new Uri(Normalize(baseAddress), UriKind.Absolute);
        return expected.Scheme.Equals(candidate.Scheme, StringComparison.OrdinalIgnoreCase) &&
               expected.IdnHost.Equals(candidate.IdnHost, StringComparison.OrdinalIgnoreCase) &&
               expected.Port == candidate.Port;
    }
}
