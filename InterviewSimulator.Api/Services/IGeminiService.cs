namespace InterviewSimulator.Api.Services;

public interface IGeminiService
{
    Task<string> GenerateQuestionAsync(string role, string level, string sessionId, CancellationToken cancellationToken = default);
    Task<string> GenerateFollowUpAsync(string question, string answer, string sessionId, CancellationToken cancellationToken = default);
    Task<string> EvaluateAnswerAsync(string question, string answer, string sessionId, CancellationToken cancellationToken = default);
}
