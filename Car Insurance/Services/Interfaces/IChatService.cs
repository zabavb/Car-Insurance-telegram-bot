namespace Car_Insurance.Services.Interfaces;

public interface IChatService
{
    public Task<string> AskAsync(string user, CancellationToken ct = default);
}