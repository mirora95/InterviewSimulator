using System.Security.Claims;
using InterviewSimulator.Api.Hubs;
using InterviewSimulator.Api.Models;
using InterviewSimulator.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace InterviewSimulator.Tests;

public class InterviewStartupErrorTests
{
    [Fact]
    public async Task FailureAfterDisconnect_DoesNotSendAnError()
    {
        using var disconnected = new CancellationTokenSource();
        var caller = new CallerProxy((_, _, _) => throw new InvalidOperationException("Caller is disconnected"));
        var hub = CreateHub(disconnected, caller, () => disconnected.Cancel());

        await hub.StartInterview("Developer", "Junior");

        Assert.Equal(0, caller.SendCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErrorReply_UsesConnectionToken_AndToleratesDisconnectDuringSend(bool disconnectDuringSend)
    {
        using var disconnected = new CancellationTokenSource();
        CancellationToken? sentToken = null;
        var caller = new CallerProxy((method, args, token) =>
        {
            sentToken = token;
            Assert.Equal("ReceiveMessage", method);
            var message = Assert.IsType<InterviewMessage>(Assert.Single(args));
            Assert.Equal("error", message.Type);
            Assert.Equal("Gemini failed", message.Text);
            if (!disconnectDuringSend) return Task.CompletedTask;
            disconnected.Cancel();
            return Task.FromCanceled(disconnected.Token);
        });
        var hub = CreateHub(disconnected, caller, () => {});

        await hub.StartInterview("Developer", "Junior");

        Assert.Equal(1, caller.SendCount);
        Assert.Equal(disconnected.Token, sentToken);
    }

    private static InterviewHub CreateHub(CancellationTokenSource disconnected, CallerProxy caller, Action beforeFailure) => new(new FailingGemini(beforeFailure))
    {
        Context = new CallerContext(disconnected),
        Clients = new CallerClients(caller)
    };

    private sealed class FailingGemini(Action beforeFailure) : IGeminiService
    {
        public Task<string> GenerateQuestionAsync(string role, string level, string sessionId, CancellationToken cancellationToken = default)
        {
            beforeFailure();
            return Task.FromException<string>(new InvalidOperationException("Gemini failed"));
        }
        public Task<string> EvaluateAnswerAsync(string question, string answer, string sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GenerateFollowUpAsync(string question, string answer, string sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CallerProxy(Func<string, object?[], CancellationToken, Task> send) : IClientProxy
    {
        public int SendCount { get; private set; }
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            SendCount++;
            return send(method, args, cancellationToken);
        }
    }

    private sealed class CallerContext(CancellationTokenSource disconnected) : HubCallerContext
    {
        public override string ConnectionId => "test-connection";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => disconnected.Token;
        public override void Abort() => disconnected.Cancel();
    }

    private sealed class CallerClients(IClientProxy caller) : IHubCallerClients
    {
        public IClientProxy Caller => caller;
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy Others => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy OthersInGroup(string groupName) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }
}
