using System.Net;
using System.Text;
using System.Text.Json;
using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AIUsageTracker.Tests.Infrastructure.Providers;

public class CodexProviderTests
{
    private readonly Mock<HttpMessageHandler> _messageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<CodexProvider>> _logger;

    public CodexProviderTests()
    {
        _messageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_messageHandler.Object);
        _logger = new Mock<ILogger<CodexProvider>>();
    }

    [Fact]
    public async Task GetUsageAsync_AuthFileMissing_ReturnsUnavailable()
    {
        // Arrange
        var missingAuthPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "auth.json");
        var provider = new CodexProvider(_httpClient, _logger.Object, missingAuthPath);

        // Act
        var results = (await provider.GetUsageAsync(new ProviderConfig { ProviderId = "codex" })).ToList();
        var usage = results.Single();

        // Assert
        Assert.False(usage.IsAvailable);
        Assert.Contains("auth token not found", usage.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetUsageAsync_NativeAuthAndUsageResponse_ReturnsParsedUsage()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var authPath = Path.Combine(tempDir, "auth.json");
        var token = CreateJwt("user@example.com", "plus");
        var accountId = "acct_123";

        await File.WriteAllTextAsync(authPath, JsonSerializer.Serialize(new
        {
            tokens = new
            {
                access_token = token,
                account_id = accountId
            }
        }));

        var rawJsonResponse = @"{
            ""model_name"": ""OpenAI-Codex-Live"",
            ""plan_type"": ""plus"",
            ""rate_limit"": {
                ""primary_window"": { ""used_percent"": 25, ""reset_after_seconds"": 1200 },
                ""secondary_window"": { ""used_percent"": 10, ""reset_after_seconds"": 600 }
            },
            ""additional_rate_limits"": [
                {
                    ""limit_name"": ""spark-plan-window"",
                    ""model_name"": ""GPT-5.3-Codex-Spark"",
                    ""rate_limit"": {
                        ""primary_window"": { ""used_percent"": 40, ""reset_after_seconds"": 3600 }
                    }
                }
            ],
            ""credits"": {
                ""balance"": 7.5,
                ""unlimited"": false
            }
        }";

        _messageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Get &&
                    request.RequestUri!.ToString() == "https://chatgpt.com/backend-api/wham/usage"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(rawJsonResponse)
            });

        var provider = new CodexProvider(_httpClient, _logger.Object, authPath);

        try
        {
            // Act
            var allUsages = (await provider.GetUsageAsync(new ProviderConfig { ProviderId = "codex" })).ToList();
            
            // Expected usages (Straight Line Architecture - no summary parent):
            // 1. Primary child (ID: codex.primary)
            // 2. Spark child (ID: codex.gpt-5.3-codex-spark)
            // (Note: secondary_window from rate_limit is also extracted)
            
            var primary = allUsages.SingleOrDefault(u => u.ProviderId == "codex.primary");
            var spark = allUsages.SingleOrDefault(u => u.ProviderId.Contains("spark", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(primary);
            Assert.NotNull(spark);
            
            // Primary Child Bar
            Assert.Equal("Codex [OpenAI Codex]", primary.ProviderName);
            Assert.Equal("user@example.com", primary.AccountName);
            Assert.Equal(75.0, primary.RequestsPercentage);
            Assert.True(primary.IsQuotaBased);

            // Spark Child Bar
            Assert.Contains("Spark", spark.ProviderName);
            Assert.Equal(60.0, spark.RequestsPercentage);
            Assert.True(spark.IsQuotaBased);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetUsageAsync_WhamSnapshot_ParsesContractFields()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var authPath = Path.Combine(tempDir, "auth.json");
        var token = CreateJwt("snapshot@example.com", "pro");
        var accountId = "acct_snapshot";
        var snapshotJson = LoadFixture("codex_wham_usage.snapshot.json");

        await File.WriteAllTextAsync(authPath, JsonSerializer.Serialize(new
        {
            tokens = new
            {
                access_token = token,
                account_id = accountId
            }
        }));

        _messageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Get &&
                    request.RequestUri!.ToString() == "https://chatgpt.com/backend-api/wham/usage"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(snapshotJson)
            });

        var provider = new CodexProvider(_httpClient, _logger.Object, authPath);

        try
        {
            // Act
            var allUsages = (await provider.GetUsageAsync(new ProviderConfig { ProviderId = "codex" })).ToList();
            var primary = allUsages.Single(u => u.ProviderId == "codex.primary");

            // Assert
            Assert.True(primary.IsAvailable);
            Assert.Equal("snapshot@example.com", primary.AccountName);
            Assert.Equal(52.0, primary.RequestsPercentage);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetUsageAsync_WhenJwtEmailMissing_UsesAccountIdAsIdentity()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var authPath = Path.Combine(tempDir, "auth.json");
        var nonJwtToken = "not-a-jwt-token";
        var accountId = "acct_fallback_456";

        await File.WriteAllTextAsync(authPath, JsonSerializer.Serialize(new
        {
            tokens = new
            {
                access_token = nonJwtToken,
                account_id = accountId
            }
        }));

        var rawJsonResponse = @"{
            ""plan_type"": ""plus"",
            ""rate_limit"": {
                ""primary_window"": { ""used_percent"": 10, ""reset_after_seconds"": 600 }
            }
        }";

        _messageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Get &&
                    request.RequestUri!.ToString() == "https://chatgpt.com/backend-api/wham/usage"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(rawJsonResponse)
            });

        var provider = new CodexProvider(_httpClient, _logger.Object, authPath);

        try
        {
            // Act
            var results = (await provider.GetUsageAsync(new ProviderConfig { ProviderId = "codex" })).ToList();
            var primary = results.Single(u => u.ProviderId == "codex.primary");

            // Assert
            Assert.Equal(accountId, primary.AccountName);
            Assert.True(primary.IsAvailable);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetUsageAsync_IdentityFallsBackToIdToken_WhenAccessTokenLacksEmail()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var authPath = Path.Combine(tempDir, "auth.json");
        var accessTokenWithoutEmail = CreateJwtWithoutIdentity();
        var idTokenWithEmail = CreateJwt("id-token@example.com", "plus");
        var accountId = "acct_id_token";

        await File.WriteAllTextAsync(authPath, JsonSerializer.Serialize(new
        {
            tokens = new
            {
                access_token = accessTokenWithoutEmail,
                id_token = idTokenWithEmail,
                account_id = accountId
            }
        }));

        var rawJsonResponse = @"{
            ""plan_type"": ""plus"",
            ""rate_limit"": {
                ""primary_window"": { ""used_percent"": 20, ""reset_after_seconds"": 1200 }
            }
        }";

        _messageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Get &&
                    request.RequestUri!.ToString() == "https://chatgpt.com/backend-api/wham/usage"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(rawJsonResponse)
            });

        var provider = new CodexProvider(_httpClient, _logger.Object, authPath);

        try
        {
            // Act
            var results = (await provider.GetUsageAsync(new ProviderConfig { ProviderId = "codex" })).ToList();
            var primary = results.Single(u => u.ProviderId == "codex.primary");

            // Assert
            Assert.Equal("id-token@example.com", primary.AccountName);
            Assert.True(primary.IsAvailable);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateJwt(string email, string planType)
    {
        var headerJson = JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" });
        var payloadJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            [ "https://api.openai.com/profile" ] = new Dictionary<string, object?>
            {
                ["email"] = email,
                ["email_verified"] = true
            },
            [ "https://api.openai.com/auth" ] = new Dictionary<string, object?>
            {
                ["chatgpt_plan_type"] = planType,
                ["chatgpt_user_id"] = "user_123"
            },
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        return $"{Base64UrlEncode(headerJson)}.{Base64UrlEncode(payloadJson)}.sig";
    }

    private static string CreateJwtWithoutIdentity()
    {
        var headerJson = JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" });
        var payloadJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            [ "https://api.openai.com/auth" ] = new Dictionary<string, object?>
            {
                ["chatgpt_plan_type"] = "plus",
                ["chatgpt_user_id"] = "user_without_email"
            },
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        return $"{Base64UrlEncode(headerJson)}.{Base64UrlEncode(payloadJson)}.sig";
    }

    private static string Base64UrlEncode(string value)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string LoadFixture(string fileName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", "Providers", fileName);
        Assert.True(File.Exists(fixturePath), $"Fixture file not found: {fixturePath}");
        return File.ReadAllText(fixturePath);
    }

    [Fact]
    public async Task GetUsageAsync_WithConfiguredModels_OverridesIdentityAndName()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var authPath = Path.Combine(tempDir, "auth.json");
        var token = CreateJwt("user@example.com", "plus");
        var accountId = "acct_123";

        await File.WriteAllTextAsync(authPath, JsonSerializer.Serialize(new
        {
            tokens = new
            {
                access_token = token,
                account_id = accountId
            }
        }));

        var rawJsonResponse = @"{
            ""model_name"": ""OpenAI-Codex-Live"",
            ""plan_type"": ""plus"",
            ""rate_limit"": {
                ""primary_window"": { ""used_percent"": 25, ""reset_after_seconds"": 1200 }
            },
            ""additional_rate_limits"": [
                {
                    ""limit_name"": ""spark-plan-window"",
                    ""model_name"": ""GPT-5.3-Codex-Spark"",
                    ""rate_limit"": {
                        ""primary_window"": { ""used_percent"": 40, ""reset_after_seconds"": 3600 }
                    }
                }
            ]
        }";

        _messageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Get &&
                    request.RequestUri!.ToString() == "https://chatgpt.com/backend-api/wham/usage"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(rawJsonResponse)
            });

        var provider = new CodexProvider(_httpClient, _logger.Object, authPath);

        var config = new ProviderConfig
        {
            ProviderId = "codex",
            Models = new List<AIModelConfig>
            {
                new() { Id = "custom.codex", Name = "Custom Codex", Matches = new List<string> { "OpenAI-Codex-Live", "OpenAI (Codex)" } },
                new() { Id = "custom.spark", Name = "Custom Spark", Matches = new List<string> { "GPT-5.3-Codex-Spark", "OpenAI (GPT-5.3-Codex-Spark)" } }
            }
        };

        try
        {
            // Act
            var allUsages = (await provider.GetUsageAsync(config)).ToList();
            var primaryUsage = allUsages.Single(u => u.ProviderId == "custom.codex");
            var sparkUsage = allUsages.Single(u => u.ProviderId == "custom.spark");

            // Assert
            Assert.Equal("Custom Codex", primaryUsage.ProviderName);
            Assert.Equal("Custom Spark", sparkUsage.ProviderName);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
