using System.Text.Json.Serialization;

namespace Car_Insurance.Models;

public class HuggingFaceResponse
{
    [JsonPropertyName("generated_text")] public string? GeneratedText { get; set; }
}