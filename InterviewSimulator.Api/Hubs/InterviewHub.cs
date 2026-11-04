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
           
            var msg = new InterviewMessage("interviewer", "question", question, DateTime.UtcNow);
            await Clients.Caller.SendAsync("ReceiveMessage", msg, cancellationToken);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // The caller has disconnected; no reply is possible.
        }
        catch (Exception ex)
        {
            // Send error message to caller for debugging
            await Clients.Caller.SendAsync("ReceiveMessage", new InterviewMessage("system", "error", ex.Message, DateTime.UtcNow));

            // Log the full exception on server
            Console.WriteLine(ex.ToString());
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
