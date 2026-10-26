using System.Net;
using System.Text;
using InterviewSimulator.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace InterviewSimulator.Tests;

public class GeminiServiceTests
{
    [Theory]
    [InlineData(null, "https://generativelanguage.googleapis.com/v1beta/models/test:generateContent?key=test-only")]
    [InlineData("https://example.test/custom", "https://example.test/custom/models/test:generateContent?key=test-only")]
    public async Task GenerateQuestion_UsesDefaultOrConfiguredEndpoint(string? baseUrl, string expectedUrl)
    {
        var handler = new ResponseHandler("""{"candidates":[{"content":{"parts":[{"text":"One question"}]}}]}""");
        using var client = new HttpClient(handler);
        var options = new GeminiOptions { ApiKey = "test-only", Model = "models/test" };
        if (baseUrl is not null) options.BaseUrl = baseUrl;
        var service = new GeminiService(new TestClientFactory(client), Options.Create(options));

        await service.GenerateQuestionAsync("C# developer", "Junior", "test-session");

        Assert.Equal(expectedUrl, handler.RequestUri?.AbsoluteUri);
    }

    [Theory]
    [InlineData("[{\"text\":\"One question\"}]", "One question")]
    [InlineData("[{\"text\":\"Explain \"},{\"text\":\"interfaces.\"}]", "Explain interfaces.")]
    [InlineData("[{\"thoughtSignature\":\"opaque\"},{\"text\":\"The answer\"}]", "The answer")]
    [InlineData("[{\"thought\":true,\"text\":\"A thought summary\"},{\"text\":\"Final \"},{\"thought\":false,\"text\":\"answer\"}]", "Final answer")]
    [InlineData("[{\"thought\":true,\"text\":\"A thought summary\"},{\"thoughtSignature\":\"opaque\"}]", "")]
    [InlineData("[{\"text\":null},{\"text\":\"\"}]", "")]
    public async Task GenerateQuestion_ReturnsAllAnswerTextFromFirstCandidate(string parts, string expected)
    {
        var response = """{"candidates":[{"content":{"parts":""" + parts
            + """}},{"content":{"parts":[{"text":"Another candidate"}]}}]}""";
        using var client = new HttpClient(new ResponseHandler(response));
        var service = new GeminiService(new TestClientFactory(client), Options.Create(new GeminiOptions
        {
            ApiKey = "test-only",
            BaseUrl = "https://example.test/v1beta",
            Model = "models/test"
        }));

        var actual = await service.GenerateQuestionAsync("C# developer", "Junior", "test-session");

        Assert.Equal(expected, actual);
    }

    private sealed class TestClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class ResponseHandler(string response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
        }
    }
}
