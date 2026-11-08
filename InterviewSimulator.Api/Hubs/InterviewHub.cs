using InterviewSimulator.Api.Models;
using InterviewSimulator.Api.Services;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace InterviewSimulator.Api.Hubs;

public class InterviewHub : Hub
{
    private readonly IGeminiService _gemini;

    public InterviewHub(IGeminiService gemini)
    {
        _gemini = gemini;
    }

    // Called by client to start session
    public async Task StartInterview(string role, string level)
    {
        try
        {
            var sessionId = Context.ConnectionId;
            var cancellationToken = Context.ConnectionAborted;

            var question = await _gemini.GenerateQuestionAsync(role, level, sessionId, cancellationToken);
            if (string.IsNullOrWhiteSpace(question))
                throw new InvalidOperationException("No interview question was generated. Please try starting again.");
           
            var msg = new InterviewMessage("interviewer", "question", question, DateTime.UtcNow);
            await Clients.Caller.SendAsync("ReceiveMessage", msg, cancellationToken);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // The caller has disconnected; no reply is possible.
        }
        catch (Exception ex)
        {
            // Log the full exception on server
            Console.WriteLine(ex.ToString());

            var cancellationToken = Context.ConnectionAborted;
            if (cancellationToken.IsCancellationRequested) return;

            try
            {
                await Clients.Caller.SendAsync("ReceiveMessage", new InterviewMessage("system", "error", ex.Message, DateTime.UtcNow), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller disconnected while the error reply was being sent.
            }
        }
    }

    // Client sends candidate answer
    public async Task SubmitAnswer(string questionText, string answerText)
    {
        try
        {
            var sessionId = Context.ConnectionId;
            var cancellationToken = Context.ConnectionAborted;

            // Immediately send "thinking" message
            await Clients.Caller.SendAsync("ReceiveMessage", new InterviewMessage("system", "info", "Evaluating...", DateTime.UtcNow), cancellationToken);

            // Ask Gemini to evaluate
            var evaluation = await _gemini.EvaluateAnswerAsync(questionText, answerText, sessionId, cancellationToken);

            // Send evaluation back
            var evalMsg = new InterviewMessage("interviewer", "evaluation", evaluation, DateTime.UtcNow);
            await Clients.Caller.SendAsync("ReceiveMessage", evalMsg, cancellationToken);

            // Optionally ask a follow-up question
            var followUp = await _gemini.GenerateFollowUpAsync(questionText, answerText, sessionId, cancellationToken);
            if (!string.IsNullOrEmpty(followUp))
            {
                var qMsg = new InterviewMessage("interviewer", "question", followUp, DateTime.UtcNow);
                await Clients.Caller.SendAsync("ReceiveMessage", qMsg, cancellationToken);
            }

            // Persist transcript & scores to DB as needed
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // The caller has disconnected; no reply is possible.
        }
    }
}
