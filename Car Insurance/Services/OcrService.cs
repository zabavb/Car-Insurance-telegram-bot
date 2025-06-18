using Car_Insurance.Models;
using Car_Insurance.Options;
using Car_Insurance.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mindee;
using Mindee.Input;
using Mindee.Product.Passport;

namespace Car_Insurance.Services;

/// <summary>
/// Service that handles OCR parsing of passport images using the Mindee API.
/// </summary>
public sealed class OcrService : IOcrService
{
    private readonly MindeeClient _client;
    private readonly ILogger<IOcrService> _log;

    /// <summary>
    /// Constructs a new OCR service with the provided API key.
    /// </summary>
    /// <param name="opt">Mindee options containing the API key.</param>
    /// <param name="log">Logger for diagnostic messages.</param>
    public OcrService(IOptions<OcrOptions> opt, ILogger<OcrService> log)
    {
        _log = log;
        var key = opt.Value.ApiKey ?? throw new InvalidOperationException("MINDEE_API_TOKEN missing.");
        _client = new MindeeClient(key);
    }

    /// <summary>
    /// Sends an image to the Mindee API and extracts information.
    /// </summary>
    /// <param name="jpg">The passport image as a byte array.</param>
    /// <param name="ct">Cancellation token for task cancellation.</param>
    /// <returns>An <see cref="ExtractedData"/> object with parsed values or default "Unknown" values on failure.</returns>
    public async Task<ExtractedData> ParsePassportAsync(byte[] jpg, CancellationToken ct = default)
    {
        // Create temporary file
        var tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
        await File.WriteAllBytesAsync(tmp, jpg, ct);

        try
        {
            // Uploading file to Mindee
            var src = new LocalInputSource(tmp);
            var resp = await _client.ParseAsync<PassportV1>(src);
            var p = resp.Document.Inference.Prediction;
            // Parsing extracted data
            return new ExtractedData(
                passportName: p.GivenNames?.FirstOrDefault()?.ToString() ?? "Unknown",
                passportSurname: p.Surname?.ToString() ?? "Unknown",
                passportId: p.IdNumber.ToString() ?? "Unknown",
                vehicleId: "V‑909091" // Mocking vehicle id 
            );
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Mindee passport parse failed.");
            return new ExtractedData("Unknown", "Unknown", "Unknown", "Unknown");
        }
        finally
        {
            File.Delete(tmp); // Deletion of temporary file anyway
        }
    }
}