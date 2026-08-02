using System.Net;
using Sub2Bar.Core.Models;
using Sub2Bar.Core.Networking;
using Xunit;

namespace Sub2Bar.Core.Tests;

public sealed class Sub2ApiClientTests
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Test/1.0";
    private static readonly Credentials Credentials = new(
        "https://example.com/sub2api", "access", "refresh", UserAgent);

    [Fact]
    public async Task AuthorizedGet_SendsBoundIdentityAndPreservesBasePath()
    {
        var handler = new RecordingHandler((_, _) => JsonResponse(
            "{\"code\":0,\"data\":{\"id\":1,\"username\":\"demo\",\"role\":\"user\"}}"));
        var client = new Sub2ApiClient(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) });

        await client.GetCurrentUserAsync(Credentials, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://example.com/sub2api/api/v1/auth/me?timezone=Asia%2FShanghai", request.Uri);
        Assert.Equal(UserAgent, request.UserAgent);
        Assert.Equal("https://example.com/sub2api/usage", request.Referrer);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("access", request.AuthorizationParameter);
        Assert.Equal("1", request.UserUiRequest);
        Assert.True(request.NoCache);
    }

    [Fact]
    public async Task AuthorizedGet_RetriesOneTransportFailure()
    {
        var handler = new RecordingHandler((attempt, _) => attempt == 1
            ? throw new HttpRequestException("transient")
            : JsonResponse("{\"code\":0,\"data\":{\"username\":\"demo\"}}"));
        var client = new Sub2ApiClient(new HttpClient(handler));

        var user = await client.GetCurrentUserAsync(Credentials, CancellationToken.None);

        Assert.Equal("demo", user.Username);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task RefreshToken_DoesNotRetryTransportFailure()
    {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("offline"));
        var client = new Sub2ApiClient(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            client.RefreshTokenAsync(Credentials, CancellationToken.None));

        Assert.Equal(ApiErrorKind.Network, exception.Kind);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RefreshToken_KeepsOldRefreshTokenWhenResponseOmitsIt()
    {
        var handler = new RecordingHandler((_, _) => JsonResponse(
            "{\"code\":0,\"data\":{\"access_token\":\"new-access\"}}"));
        var client = new Sub2ApiClient(new HttpClient(handler));

        var pair = await client.RefreshTokenAsync(Credentials, CancellationToken.None);

        Assert.Equal("new-access", pair.AccessToken);
        Assert.Equal("refresh", pair.RefreshToken);
        var request = Assert.Single(handler.Requests);
        Assert.Null(request.AuthorizationScheme);
    }

    [Fact]
    public void SameOrigin_RequiresSchemeHostAndEffectivePort()
    {
        Assert.True(ServerAddress.IsSameOrigin("https://example.com/base",
            new Uri("https://example.com/login")));
        Assert.False(ServerAddress.IsSameOrigin("https://example.com/base",
            new Uri("http://example.com/login")));
        Assert.False(ServerAddress.IsSameOrigin("https://example.com/base",
            new Uri("https://example.com:444/login")));
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed record RequestSnapshot(
        string Uri,
        string UserAgent,
        string? Referrer,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? UserUiRequest,
        bool NoCache);

    private sealed class RecordingHandler(
        Func<int, HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<RequestSnapshot> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Requests.Add(new RequestSnapshot(
                request.RequestUri?.AbsoluteUri ?? string.Empty,
                request.Headers.UserAgent.ToString(),
                request.Headers.Referrer?.AbsoluteUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("X-User-UI-Request", out var values) ? values.Single() : null,
                request.Headers.CacheControl?.NoCache == true));
            return Task.FromResult(responseFactory(Calls, request));
        }
    }
}
