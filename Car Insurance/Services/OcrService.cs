using Car_Insurance.Models;
using Car_Insurance.Options;
using Car_Insurance.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mindee;
using Mindee.Input;
using Mindee.Product.Passport;

namespace Car_Insurance.Services;

public sealed class OcrService : IOcrService
{
    private readonly MindeeClient _client;
    private readonly ILogger<IOcrService> _log;

    public OcrService(IOptions<OcrOptions> opt, ILogger<OcrService> log)
    {
        _log = log;
        var key = opt.Value.ApiKey ?? throw new InvalidOperationException("MINDEE_API_TOKEN missing.");
        _client = new MindeeClient(key);
    }

    public async Task<ExtractedData> ParsePassportAsync(byte[] jpg, CancellationToken ct = default)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
        await File.WriteAllBytesAsync(tmp, jpg, ct);

        try
        {
            var src = new LocalInputSource(tmp);
            var resp = await _client.ParseAsync<PassportV1>(src);
            var p = resp.Document.Inference.Prediction;

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