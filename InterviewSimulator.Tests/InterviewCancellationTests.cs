using System.Net;
using System.Text;
using InterviewSimulator.Api.Hubs;
using InterviewSimulator.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InterviewSimulator.Tests;

public class InterviewCancellationTests
{
    [Theory]
    [InlineData("StartInterview", 1)]
    [InlineData("SubmitAnswer", 1)]
    [InlineData("SubmitAnswer", 2)]
    public async Task Disconnect_CancelsThePendingGeminiRequest(string method, int pendingRequest)
    {
        var handler = new PendingResponseHandler(pendingRequest);
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSignalR();
                services.Configure<GeminiOptions>(options =>
                {
                    options.ApiKey = "test-only";
                    options.BaseUrl = "https://example.test/v1beta";
                    options.Model = "models/test";
                });
                services.AddHttpClient("gemini").ConfigurePrimaryHttpMessageHandler(() => handler);
                services.AddSingleton<IGeminiService, GeminiService>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapHub<InterviewHub>("/hubs/interview"));
            }));
        await using var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/interview", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await connection.StartAsync(timeout.Token);
        await connection.SendAsync(method, "Question or role", "Answer or level", timeout.Token);
        await handler.Started.Task.WaitAsync(timeout.Token);

        var stopping = connection.StopAsync(timeout.Token);
        try
        {
            await handler.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            handler.Release.TrySetResult();
            await stopping;
        }

        Assert.Equal(pendingRequest, handler.RequestCount);
    }

    private sealed class PendingResponseHandler(int pendingRequest) : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (++RequestCount == pendingRequest)
            {
                Started.TrySetResult();
                try
                {
                    await Release.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Cancelled.TrySetResult();
                    throw;
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"A result"}]}}]}""", Encoding.UTF8, "application/json")
            };
        }
    }
}
