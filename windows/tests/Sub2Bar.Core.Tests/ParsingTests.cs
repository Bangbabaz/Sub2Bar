using System.Net;
using Sub2Bar.Core.Models;
using Sub2Bar.Core.Networking;
using Sub2Bar.Core.Parsing;
using Xunit;

namespace Sub2Bar.Core.Tests;

public sealed class ParsingTests
{
    [Fact]
    public void ProfileParser_AcceptsWrappedAdminProfile()
    {
        const string json = """
            {"code":"0","message":"success","data":{"user":{"id":"7","username":"demo","role":"admin"}}}
            """;

        var user = ProfileParser.Parse(json);

        Assert.Equal(7L, user.Id);
        Assert.Equal("demo", user.Username);
        Assert.True(user.IsAdmin);
    }

    [Fact]
    public void QuotaParser_AcceptsFlatAndNestedQuotaWindows()
    {
        const string json = """
            {
              "code": 0,
              "data": {
                "username": "demo",
                "balance": "18.75",
                "subscriptions": [{
                  "id": 12,
                  "status": "active",
                  "expires_at": "2026-08-27T13:02:53.508173+08:00",
                  "daily_usage_usd": 3.5,
                  "weekly_usage_usd": 11,
                  "group": {
                    "name": "Claude Max",
                    "daily_limit_usd": 10,
                    "weekly_limit_usd": 50
                  }
                }]
              }
            }
            """;

        var snapshot = QuotaParser.Parse(json);

        Assert.Equal("demo", snapshot.AccountName);
        Assert.Equal(18.75, snapshot.Balance);
        var subscription = Assert.Single(snapshot.Subscriptions);
        Assert.Equal("Claude Max", subscription.Name);
        Assert.Equal(6.5, Assert.Single(subscription.Windows, item => item.Period == QuotaPeriod.Daily).Remaining);
        Assert.Equal(39, Assert.Single(subscription.Windows, item => item.Period == QuotaPeriod.Weekly).Remaining);
        Assert.Equal(65d, snapshot.LowestRemainingPercentage);
    }

    [Fact]
    public void QuotaParser_UsesResetSecondsAndDoesNotDivideByZero()
    {
        var fetchedAt = new DateTimeOffset(2026, 7, 30, 10, 0, 0, TimeSpan.FromHours(8));
        const string json = """
            {"subscription_id":7,"name":"Codex","daily":{"used":8,"limit":0,"percentage":80,"reset_in_seconds":3600}}
            """;

        var snapshot = QuotaParser.Parse(json, fetchedAt: fetchedAt);
        var window = Assert.Single(Assert.Single(snapshot.Subscriptions).Windows);

        Assert.Null(window.Limit);
        Assert.Null(window.Remaining);
        Assert.Equal(20, window.RemainingPercentage);
        Assert.Equal(fetchedAt.AddHours(1), window.ResetsAt);
    }

    [Fact]
    public void QuotaParser_AcceptsSubscriptionArrayEnvelope()
    {
        const string json = """
            {
              "code": 0,
              "data": [{
                "id": 12,
                "status": "active",
                "daily_usage_usd": 66.766473,
                "weekly_usage_usd": 82.100713,
                "monthly_usage_usd": 82.100713,
                "group": {
                  "name": "bangbaba",
                  "daily_limit_usd": 0,
                  "weekly_limit_usd": 400,
                  "monthly_limit_usd": 0
                }
              }]
            }
            """;

        var snapshot = QuotaParser.Parse(json, "account@example.com");

        Assert.Null(snapshot.Balance);
        var subscription = Assert.Single(snapshot.Subscriptions);
        Assert.Equal("bangbaba", subscription.Name);
        Assert.True(subscription.IsActive);
        Assert.Equal(3, subscription.Windows.Count);
        Assert.Null(Assert.Single(subscription.Windows, item => item.Period == QuotaPeriod.Daily).Limit);
        var weekly = Assert.Single(subscription.Windows, item => item.Period == QuotaPeriod.Weekly);
        Assert.NotNull(weekly.Remaining);
        Assert.Equal(317.899287, weekly.Remaining.Value, 6);
        Assert.Null(Assert.Single(subscription.Windows, item => item.Period == QuotaPeriod.Monthly).Limit);
    }

    [Fact]
    public void AdminParser_HandlesOauthAndApiKeyAccounts()
    {
        const string json = """
            {
              "code":0,
              "data":{"items":[
                {"id":1,"name":"OAuth account","platform":"openai","type":"oauth","status":"active",
                 "credentials":{"email":"account@example.com","plan_type":"pro"},
                 "groups":[{"id":17,"name":"default"}]},
                {"id":4,"name":"API key account","platform":"openai","type":"apikey","status":"active",
                 "extra":{"quota_limit":200},"quota_used":12.5}
              ],"total":2,"page":1,"page_size":20,"pages":1}
            }
            """;

        var page = AdminParser.ParseAccounts(json);

        Assert.Equal(2, page.Total);
        Assert.Equal("account@example.com", page.Items[0].DisplayName);
        Assert.Equal("pro", page.Items[0].Plan);
        Assert.Equal("default", Assert.Single(page.Items[0].Groups));
        Assert.Equal(200, page.Items[1].QuotaLimit);
        Assert.Equal(12.5, page.Items[1].QuotaUsed);
    }

    [Fact]
    public void AdminParser_FallsBackToCostForMissingCostVariants()
    {
        const string json = """
            {"code":0,"data":{"five_hour":{"utilization":14,"resets_at":"2026-07-28T16:24:17+08:00",
             "window_stats":{"requests":1897,"tokens":239848676,"cost":263.385088}}}}
            """;

        var usage = AdminParser.ParseUsage(1, json);

        Assert.Equal(14, usage.FiveHour?.Utilization);
        Assert.Equal(263.385088, usage.FiveHour?.StandardCost);
        Assert.Equal(263.385088, usage.FiveHour?.UserCost);
    }

    [Fact]
    public void AdminParser_ParsesSubscriptionQuotaWindows()
    {
        const string json = """
            {"code":0,"data":{"items":[{
              "id":12,"user_id":1,"status":"active",
              "expires_at":"2026-08-27T13:02:53.508173+08:00",
              "weekly_window_start":"2026-07-29T00:00:00+08:00",
              "weekly_usage_usd":84.87953,
              "user":{"email":"owner@example.com"},
              "group":{"name":"bangbaba","weekly_limit_usd":400}
            }],"total":1}}
            """;

        var page = AdminParser.ParseSubscriptions(json);

        var subscription = Assert.Single(page.Items);
        Assert.Equal("bangbaba", subscription.Name);
        Assert.Equal("owner@example.com", subscription.Username);
        var weekly = Assert.Single(subscription.Windows);
        Assert.Equal(84.87953, weekly.Used, 6);
        Assert.Equal(400, weekly.Limit);
        Assert.Equal(new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.FromHours(8)), weekly.ResetsAt);
    }

    [Theory]
    [InlineData("https://example.com/base/", "https://example.com/base")]
    [InlineData("example.com/sub2api", "http://example.com/sub2api")]
    [InlineData(" http://localhost:8080/base?q=1#x ", "http://localhost:8080/base")]
    public void ServerAddress_NormalizesBaseAddress(string input, string expected)
    {
        Assert.Equal(expected, ServerAddress.Normalize(input));
    }

    [Fact]
    public void ServerAddress_PreservesBasePathWhenBuildingApiUri()
    {
        var uri = ServerAddress.Build("https://example.com/sub2api", "/api/v1/auth/me");

        Assert.Equal("https://example.com/sub2api/api/v1/auth/me", uri.AbsoluteUri);
    }

    [Fact]
    public void EnvelopeParser_ClassifiesSessionBindingMismatch()
    {
        const string json = """
            {"code":"SESSION_BINDING_MISMATCH","message":"Session network fingerprint changed"}
            """;

        var exception = Assert.Throws<ApiException>(() => EnvelopeParser.Parse(json, HttpStatusCode.OK));

        Assert.Equal(ApiErrorKind.SessionBindingMismatch, exception.Kind);
    }

    [Fact]
    public void EnvelopeParser_AcceptsDirectArrays()
    {
        var envelope = EnvelopeParser.Parse("[1,2,3]");

        Assert.Equal(3, envelope.Data.GetArrayLength());
    }
}
