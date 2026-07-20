using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OfrenCollect.Application.Assistant;
using OfrenCollect.Infrastructure.Ai;

namespace OfrenCollect.Infrastructure.UnitTests.Ai;

public class LlmIntentClassifierTests
{
    private static LlmIntentClassifier CreateClassifier(string content, HttpStatusCode status = HttpStatusCode.OK)
    {
        var json = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"" + content + "\"}}]}";
        var http = new HttpClient(new StubHandler(json, status)) { BaseAddress = new Uri("http://localhost:11434") };
        return new LlmIntentClassifier(
            http, new AiOptions { Model = "test", BaseUrl = "http://localhost:11434" },
            NullLogger<LlmIntentClassifier>.Instance);
    }

    [Theory]
    [InlineData("collected_this_week", CollectionsIntent.CollectedThisWeek)]
    [InlineData("overdue_customers", CollectionsIntent.OverdueCustomers)]
    [InlineData("underpaid_customers", CollectionsIntent.UnderpaidCustomers)]
    [InlineData("active_subscriptions", CollectionsIntent.ActiveSubscriptions)]
    [InlineData("i cannot help with that", CollectionsIntent.Unknown)]
    // Unmatched inflows are tenant-less orphans; the tenant-facing assistant must not surface a
    // cross-tenant figure, so that wording is no longer a recognised intent (declined instead).
    [InlineData("unmatched_payments", CollectionsIntent.Unknown)]
    public async Task Classify_MapsModelReplyToIntent(string content, CollectionsIntent expected)
    {
        var result = await CreateClassifier(content).ClassifyAsync("question", CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Classify_WhenProviderErrors_ReturnsUnknown_FailSafe()
    {
        var classifier = CreateClassifier("collected_this_week", HttpStatusCode.InternalServerError);

        var result = await classifier.ClassifyAsync("question", CancellationToken.None);

        result.Should().Be(CollectionsIntent.Unknown);
    }

    [Fact]
    public async Task Classify_PreservesBasePath_WhenBaseUrlHasPathSegment()
    {
        // Regression: a leading slash on the request path dropped a base path like Groq's "/openai",
        // producing https://api.groq.com/v1/... (404). The relative path must append to the base.
        var handler = new StubHandler("{\"choices\":[]}", HttpStatusCode.OK);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.com/openai/") };
        var classifier = new LlmIntentClassifier(
            http, new AiOptions { Model = "m", BaseUrl = "https://api.groq.com/openai/" },
            NullLogger<LlmIntentClassifier>.Instance);

        await classifier.ClassifyAsync("question", CancellationToken.None);

        handler.LastRequestUri.Should().Be("https://api.groq.com/openai/v1/chat/completions");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        private readonly HttpStatusCode _status;

        public StubHandler(string json, HttpStatusCode status)
        {
            _json = json;
            _status = status;
        }

        public string? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
