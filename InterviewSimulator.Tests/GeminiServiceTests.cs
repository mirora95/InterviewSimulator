using System.Net;
using System.Text;
using InterviewSimulator.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace InterviewSimulator.Tests;

public class GeminiServiceTests
{
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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
    }
}
