using System.Threading.Channels;
using InterviewSimulator.Api.Hubs;
using InterviewSimulator.Api.Models;
using InterviewSimulator.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InterviewSimulator.Tests;

public class InterviewHubTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Can you give an example?")]
    public async Task SubmitAnswer_AcceptsBrowserArgumentsAndUsesCurrentConnection(string followUp)
    {
        var gemini = new RecordingGeminiService(followUp);
        using var server = CreateServer(gemini);
        await using var connection = CreateConnection(server);
        var messages = Channel.CreateUnbounded<InterviewMessage>();
        connection.On<InterviewMessage>("ReceiveMessage", message => { messages.Writer.TryWrite(message); });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await connection.StartAsync(timeout.Token);
        await connection.InvokeAsync("StartInterview", "C# Developer", "Junior", timeout.Token);

        // Match the two arguments sent by wwwroot/interview.js.
        await connection.InvokeAsync("SubmitAnswer", "What is an interface?", "A contract.", timeout.Token);

        var connectionId = Assert.IsType<string>(connection.ConnectionId);
        Assert.Equal(connectionId, gemini.StartSessionId);
        Assert.False(string.IsNullOrEmpty(gemini.StartSessionId));
        Assert.Equal(("What is an interface?", "A contract.", connectionId), gemini.Evaluation);
        Assert.Equal(gemini.Evaluation, gemini.FollowUp);
        var expected = new List<(string From, string Type, string Text)>
        {
            ("interviewer", "question", "What is an interface?"),
            ("system", "info", "Evaluating..."),
            ("interviewer", "evaluation", "Good answer.")
        };
        if (followUp.Length > 0)
            expected.Add(("interviewer", "question", followUp));
        var actual = new List<(string From, string Type, string Text)>();
        foreach (var _ in expected)
        {
            var message = await messages.Reader.ReadAsync(timeout.Token);
            actual.Add((message.From, message.Type, message.Text));
        }
        Assert.Equal(expected, actual);
        Assert.False(messages.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("evaluation")]
    [InlineData("follow-up")]
    public async Task SubmitAnswer_ProviderFailureRejectsInvocationAndAllowsRetry(string failureStage)
    {
        using var server = CreateServer(new RecordingGeminiService("", failureStage));
        await using var connection = CreateConnection(server);
        var messages = Channel.CreateUnbounded<InterviewMessage>();
        connection.On<InterviewMessage>("ReceiveMessage", message => { messages.Writer.TryWrite(message); });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await connection.StartAsync(timeout.Token);
        await connection.InvokeAsync("StartInterview", "Developer", "Junior", timeout.Token);

        var error = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
            connection.InvokeAsync("SubmitAnswer", "What is an interface?", "A contract.", timeout.Token));

        Assert.DoesNotContain("Synthetic provider detail", error.Message);
        Assert.Equal(HubConnectionState.Connected, connection.State);
        var expected = new List<string> { "question", "info" };
        if (failureStage == "follow-up") expected.Add("evaluation");
        foreach (var type in expected)
        {
            var message = await messages.Reader.ReadAsync(timeout.Token);
            Assert.Equal(type, message.Type);
        }
        Assert.False(messages.Reader.TryRead(out _));

        await connection.InvokeAsync("SubmitAnswer", "What is an interface?", "A contract.", timeout.Token);

        Assert.Equal("info", (await messages.Reader.ReadAsync(timeout.Token)).Type);
        Assert.Equal("evaluation", (await messages.Reader.ReadAsync(timeout.Token)).Type);
        Assert.False(messages.Reader.TryRead(out _));
    }

    private static TestServer CreateServer(IGeminiService gemini) => new(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            services.AddSignalR();
            services.AddSingleton(gemini);
        })
        .Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapHub<InterviewHub>("/hubs/interview"));
        }));

    private static HubConnection CreateConnection(TestServer server) => new HubConnectionBuilder()
        .WithUrl("http://localhost/hubs/interview", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => server.CreateHandler();
        })
        .Build();

    private sealed class RecordingGeminiService(string followUp, string? failureStage = null) : IGeminiService
    {
        private bool _failed;

        public string? StartSessionId { get; private set; }
        public (string Question, string Answer, string SessionId)? Evaluation { get; private set; }
        public (string Question, string Answer, string SessionId)? FollowUp { get; private set; }

        public Task<string> GenerateQuestionAsync(string role, string level, string sessionId, CancellationToken cancellationToken = default)
        {
            StartSessionId = sessionId;
            return Task.FromResult("What is an interface?");
        }

        public Task<string> EvaluateAnswerAsync(string question, string answer, string sessionId, CancellationToken cancellationToken = default)
        {
            Evaluation = (question, answer, sessionId);
            if (!_failed && failureStage == "evaluation")
            {
                _failed = true;
                throw new InvalidOperationException("Synthetic provider detail");
            }
            return Task.FromResult("Good answer.");
        }

        public Task<string> GenerateFollowUpAsync(string question, string answer, string sessionId, CancellationToken cancellationToken = default)
        {
            FollowUp = (question, answer, sessionId);
            if (!_failed && failureStage == "follow-up")
            {
                _failed = true;
                throw new InvalidOperationException("Synthetic provider detail");
            }
            return Task.FromResult(followUp);
        }
    }
}
