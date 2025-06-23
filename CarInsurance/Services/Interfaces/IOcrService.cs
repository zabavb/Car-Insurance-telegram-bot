using CarInsurance.Models;

namespace CarInsurance.Services.Interfaces;

public interface IOcrService
{
    public Task<ExtractedData> ParsePassportAsync(byte[] jpg, CancellationToken ct = default);
}