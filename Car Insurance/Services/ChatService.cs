using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Car_Insurance.Models;
using Car_Insurance.Options;
using Car_Insurance.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Car_Insurance.Services;

public sealed class ChatService(
    HttpClient http,
    IOptions<ChatOptions> opt,
    ILogger<IChatService> log) : IChatService
{
    private readonly HttpClient _http = http;
    private readonly ILogger<IChatService> _log = log;
    
    private readonly string _apiUrl = opt.Value.ApiUrl
                                      ?? throw new InvalidOperationException("ChatOptions.ApiUrl missing.");

    private readonly string _bearer = opt.Value.ApiToken
                                      ?? throw new InvalidOperationException("HuggingFace:ApiToken missing.");
    
    public async Task<string> AskAsync(string user, CancellationToken ct = default)
    {
        var prompt = $"AI: You are a helpful assistant for a car insurance company. " +
                     $"Answer the user's question politely and clearly. " +
                     $"If you are unsure about the answer, apologize and suggest calling the support line. " +
                     $"Keep the tone friendly and concise.\n\nUser: {user}";

        var body = new
        {
            inputs = prompt,
            parameters = new { max_new_tokens = 150, return_full_text = false, temperature = 0.7, top_p = 0.9 }
        };

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _bearer);

        var resp = await _http.PostAsJsonAsync(_apiUrl, body, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("HF error {Code}: {Body}", resp.StatusCode, json);
            return "🚨 Sorry, I’m having trouble answering right now.";
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<HuggingFaceResponse>>(json);
            return parsed?[0].GeneratedText?.Trim()
                   ?? "⚠️ Sorry, I don’t have an answer.";
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to parse HF response: {Json}", json);
            return "🚨 Sorry, I couldn’t understand. Could you re‑phrase?";
        }
    }
}