using System.Text.Json.Serialization;

namespace CarInsurance.Models;

public class HuggingFaceResponse
{
    [JsonPropertyName("generated_text")] public string? GeneratedText { get; set; }
}