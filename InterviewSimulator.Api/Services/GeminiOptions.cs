namespace InterviewSimulator.Api.Services;

public class GeminiOptions
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public string Model { get; set; } = "models/gemini-2.5-flash";
    public int MaxOutputTokens { get; set; } = 512;
    public double Temperature { get; set; } = 0.2;
}
