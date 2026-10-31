using Microsoft.Extensions.Options;
using System.Text.Json;

namespace InterviewSimulator.Api.Services;

public class GeminiService : IGeminiService
{
    private readonly IHttpClientFactory _http;
    private readonly GeminiOptions _opts;

    public GeminiService(IHttpClientFactory http, IOptions<GeminiOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
    }

    private HttpClient CreateClient()
    {
        var c = _http.CreateClient("gemini");
        return c;
    }

    public async Task<string> GenerateQuestionAsync(string role, string level, string sessionId)
    {
        var prompt = $@"You are an expert interviewer for role: {role}, level: {level}.
Generate one concise technical interview question and include tags and difficulty.
Respond only with JSON: {{ ""type"": ""question"", ""text"": ""..."" }}";
        return await CallGeminiAsync(prompt);
    }

    public async Task<string> GenerateFollowUpAsync(string question, string answer, string sessionId)
    {
        var prompt = $@"Given question: {question}
Candidate answer: {answer}
If a clarifying or follow-up question would help, produce it. Otherwise output an empty string.";
        var response = await CallGeminiAsync(prompt);

        try
        {
            // JSON mode can encode an absent follow-up as "" instead of an empty response.
            return JsonSerializer.Deserialize<string>(response) ?? string.Empty;
        }
        catch (JsonException)
        {
            return response;
        }
    }

    public async Task<string> EvaluateAnswerAsync(string question, string answer, string sessionId)
    {
        var prompt = $@"Evaluate the candidate answer to this question.
Question: {question}
Answer: {answer}

Use the rubric: correctness 0-5, clarity 0-3, depth 0-2, bestPractices 0-2.
Return JSON: {{ ""type"":""evaluation"",""scores"":{{""correctness"":X,""clarity"":Y,""depth"":Z,""bestPractices"":W}},""total"":N,""feedback"":""..."" }}";
        return await CallGeminiAsync(prompt);
    }

    private async Task<string> CallGeminiAsync(string prompt)
    {
        var client = CreateClient();

        // Correct URL with ?key=
        var url = $"{_opts.BaseUrl}/{_opts.Model}:generateContent?key={_opts.ApiKey}";

        var body = new
        {
            contents = new[]
        {
            new
            {
                parts = new[] { new { text = prompt } }
            }
        },
            generationConfig = new
            {
                temperature = _opts.Temperature, // Ensure these properties exist in GeminiOptions
                maxOutputTokens = _opts.MaxOutputTokens, // Ensure these properties exist in GeminiOptions
                responseMimeType = "application/json"
            }
        };

        var resp = await client.PostAsJsonAsync(url, body);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Gemini API error: {resp.StatusCode} - {err}");
        }

        using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        // Extract the text
        if (doc.RootElement.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0)
        {
            if (cands[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
            {
                // A candidate can contain multiple answer parts alongside thought or non-text parts.
                var textParts = parts.EnumerateArray()
                    .Where(part => !part.TryGetProperty("thought", out var thought) || thought.ValueKind != JsonValueKind.True)
                    .Where(part => part.TryGetProperty("text", out _))
                    .Select(part => part.GetProperty("text").GetString());
                return string.Concat(textParts);
            }
        }

        return await resp.Content.ReadAsStringAsync();
    }
}
