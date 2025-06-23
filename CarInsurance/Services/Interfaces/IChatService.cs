using CarInsurance.Models;

namespace CarInsurance.Services.Interfaces;

public interface IChatService
{
    public Task<string> AskAsync(string user, Stage currentStage, CancellationToken ct = default);
}