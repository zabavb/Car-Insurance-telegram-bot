using Car_Insurance.Models;

namespace Car_Insurance.Services.Interfaces;

public interface IOcrService
{
    public Task<ExtractedData> ParsePassportAsync(byte[] jpg, CancellationToken ct = default);
}