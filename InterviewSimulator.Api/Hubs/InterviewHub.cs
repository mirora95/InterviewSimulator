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

            var question = await _gemini.GenerateQuestionAsync(role, level, sessionId);
           
            var msg = new InterviewMessage("interviewer", "question", question, DateTime.UtcNow);
            await Clients.Caller.SendAsync("ReceiveMessage", msg);
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
        var sessionId = Context.ConnectionId;

        // Immediately send "thinking" message
        await Clients.Caller.SendAsync("ReceiveMessage", new InterviewMessage("system", "info", "Evaluating...", DateTime.UtcNow));

        // Ask Gemini to evaluate
        var evaluation = await _gemini.EvaluateAnswerAsync(questionText, answerText, sessionId);

        // Send evaluation back
        var evalMsg = new InterviewMessage("interviewer", "evaluation", evaluation, DateTime.UtcNow);
        await Clients.Caller.SendAsync("ReceiveMessage", evalMsg);

        // Optionally ask a follow-up question
        var followUp = await _gemini.GenerateFollowUpAsync(questionText, answerText, sessionId);
        if (!string.IsNullOrEmpty(followUp))
        {
            var qMsg = new InterviewMessage("interviewer", "question", followUp, DateTime.UtcNow);
            await Clients.Caller.SendAsync("ReceiveMessage", qMsg);
        }

        // Persist transcript & scores to DB as needed
    }
}
