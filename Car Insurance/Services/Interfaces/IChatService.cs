using Car_Insurance.Models;

namespace Car_Insurance.Services.Interfaces;

public interface IChatService
{
    public Task<string> AskAsync(string user, Stage currentStage, CancellationToken ct = default);
}