using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Sub2Bar.Core.Models;
using Sub2Bar.Core.Parsing;

namespace Sub2Bar.Core.Networking;

public sealed class Sub2ApiClient(HttpClient httpClient) : ISub2ApiClient
{
    public async Task<CurrentUser> GetCurrentUserAsync(Credentials credentials,
        CancellationToken cancellationToken)
    {
        const string path = "api/v1/auth/me?timezone=Asia%2FShanghai";
        try
        {
            var json = await GetAsync(credentials, path, cancellationToken);
            return ProfileParser.Parse(json);
        }
        catch (ApiException exception)
        {
            throw WithPath(exception, path);
        }
    }

    public async Task<QuotaSnapshot> GetQuotaAsync(Credentials credentials, string accountName,
        CancellationToken cancellationToken)
    {
        const string path = "api/v1/subscriptions?timezone=Asia%2FShanghai";
        try
        {
            var json = await GetAsync(credentials, path, cancellationToken);
            return QuotaParser.Parse(json, accountName);
        }
        catch (ApiException exception)
        {
            throw WithPath(exception, path);
        }
    }

    public async Task<PageResult<AdminAccount>> GetAdminAccountsAsync(Credentials credentials,
        CancellationToken cancellationToken)
    {
        const string path = "api/v1/admin/accounts?page=1&page_size=20&platform=&type=&status=&privacy_mode=&group=&search=";
        try
        {
            var json = await GetAsync(credentials, path, cancellationToken);
            return AdminParser.ParseAccounts(json);
        }
        catch (ApiException exception)
        {
            throw WithPath(exception, path);
        }
    }

    public async Task<PageResult<AdminSubscription>> GetAdminSubscriptionsAsync(Credentials credentials,
        CancellationToken cancellationToken)
    {
        const string path = "api/v1/admin/subscriptions?page=1&page_size=20&status=active&sort=-id";
        try
        {
            var json = await GetAsync(credentials, path, cancellationToken);
            return AdminParser.ParseSubscriptions(json);
        }
        catch (ApiException exception)
        {
            throw WithPath(exception, path);
        }
    }

    public async Task<AdminAccountUsage> GetAdminUsageAsync(Credentials credentials, long accountId,
        CancellationToken cancellationToken)
    {
        var path = $"api/v1/admin/accounts/{accountId}/usage?source=active&force=true&timezone=Asia%2FShanghai";
        try
        {
            var json = await GetAsync(credentials, path, cancellationToken);
            return AdminParser.ParseUsage(accountId, json);
        }
        catch (ApiException exception)
        {
            throw WithPath(exception, path);
        }
    }

    public async Task<TokenPair> RefreshTokenAsync(Credentials credentials,
        CancellationToken cancellationToken)
    {
        const string path = "api/v1/auth/refresh";
        using var request = new HttpRequestMessage(HttpMethod.Post,
            ServerAddress.Build(credentials.ServerAddress, path));
        AddCommonHeaders(request, credentials, includeAuthorization: false);
        request.Content = JsonContent.Create(new { refresh_token = credentials.RefreshToken });

        string json;
        JsonElement data;
        try
        {
            json = await SendAsync(request, cancellationToken);
            data = EnvelopeParser.Parse(json).Data;
        }
        catch (ApiException exception)
        {
            throw WithPath(exception, path);
        }
        catch (HttpRequestException exception)
        {
            throw new ApiException(ApiErrorKind.Network, "无法连接到服务器。",
                requestPath: path, innerException: exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiException(ApiErrorKind.Network, "连接服务器超时。",
                requestPath: path, innerException: exception);
        }
        var accessToken = FirstNotEmpty(
            JsonValueForToken(data, "access_token"),
            JsonValueForToken(data, "accessToken"),
            JsonValueForToken(data, "token"));
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ApiException(ApiErrorKind.InvalidResponse, "刷新响应中没有 Access Token。",
                responseBody: json, requestPath: path);
        }

        var refreshToken = FirstNotEmpty(
            JsonValueForToken(data, "refresh_token"),
            JsonValueForToken(data, "refreshToken"),
            credentials.RefreshToken);
        return new TokenPair(accessToken, refreshToken);
    }

    private async Task<string> GetAsync(Credentials credentials, string path,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    ServerAddress.Build(credentials.ServerAddress, path));
                AddCommonHeaders(request, credentials);
                return await SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException) when (attempt == 0 && !cancellationToken.IsCancellationRequested)
            {
                // GET transport failures are retried once; application and auth failures are never retried here.
            }
            catch (TaskCanceledException) when (attempt == 0 && !cancellationToken.IsCancellationRequested)
            {
                // HttpClient timeout is treated as a transient transport failure.
            }
            catch (HttpRequestException exception)
            {
                throw new ApiException(ApiErrorKind.Network, "无法连接到服务器。", innerException: exception);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ApiException(ApiErrorKind.Network, "连接服务器超时。", innerException: exception);
            }
        }
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                EnvelopeParser.Parse(body, response.StatusCode);
            }
            catch (ApiException exception) when (exception.Kind == ApiErrorKind.InvalidResponse)
            {
                throw new ApiException(ApiErrorClassifier.Classify(response.StatusCode, null),
                    $"服务器返回 HTTP {(int)response.StatusCode}。", response.StatusCode,
                    responseBody: body, innerException: exception);
            }
        }

        return body;
    }

    private static void AddCommonHeaders(HttpRequestMessage request, Credentials credentials,
        bool includeAuthorization = true)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        request.Headers.AcceptLanguage.ParseAdd("zh-CN");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        request.Headers.Pragma.ParseAdd("no-cache");
        if (!request.Headers.UserAgent.TryParseAdd(credentials.BoundUserAgent))
        {
            throw new ApiException(ApiErrorKind.CredentialRejected, "登录会话缺少有效的 User-Agent，请重新登录。");
        }

        request.Headers.Referrer = ServerAddress.Build(credentials.ServerAddress, "usage");
        if (includeAuthorization)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        }
        request.Headers.TryAddWithoutValidation("X-User-UI-Request", "1");
    }

    private static string JsonValueForToken(JsonElement data, string name)
    {
        if (TryGetProperty(data, name, out var direct))
        {
            return direct.ValueKind == JsonValueKind.String ? direct.GetString() ?? string.Empty : string.Empty;
        }

        if (TryGetProperty(data, "data", out var nested) && TryGetProperty(nested, name, out var nestedValue))
        {
            return nestedValue.ValueKind == JsonValueKind.String ? nestedValue.GetString() ?? string.Empty : string.Empty;
        }

        return string.Empty;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string FirstNotEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static ApiException WithPath(ApiException exception, string path) =>
        exception.RequestPath is not null
            ? exception
            : new ApiException(exception.Kind, exception.Message, exception.StatusCode, exception.ApiCode,
                exception.ResponseBody, path, exception);
}
