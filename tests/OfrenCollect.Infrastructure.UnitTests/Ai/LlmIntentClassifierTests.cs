using System.Net;
using System.Text;
using FluentAssertions;
using OfrenCollect.Application.Assistant;
using OfrenCollect.Infrastructure.Ai;

namespace OfrenCollect.Infrastructure.UnitTests.Ai;

public class LlmIntentClassifierTests
{
    private static LlmIntentClassifier CreateClassifier(string content, HttpStatusCode status = HttpStatusCode.OK)
    {
        var json = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"" + content + "\"}}]}";
        var http = new HttpClient(new StubHandler(json, status)) { BaseAddress = new Uri("http://localhost:11434") };
        return new LlmIntentClassifier(http, new AiOptions { Model = "test", BaseUrl = "http://localhost:11434" });
    }

    [Theory]
    [InlineData("collected_this_week", CollectionsIntent.CollectedThisWeek)]
    [InlineData("overdue_customers", CollectionsIntent.OverdueCustomers)]
    [InlineData("underpaid_customers", CollectionsIntent.UnderpaidCustomers)]
    [InlineData("active_subscriptions", CollectionsIntent.ActiveSubscriptions)]
    [InlineData("unmatched_payments", CollectionsIntent.UnmatchedPayments)]
    [InlineData("i cannot help with that", CollectionsIntent.Unknown)]
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

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        private readonly HttpStatusCode _status;

        public StubHandler(string json, HttpStatusCode status)
        {
            _json = json;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            });
    }
}
