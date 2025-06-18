using Car_Insurance.Models;

namespace Car_Insurance.Services.Interfaces;

public interface IConversationStore
{
    public ConversationState Get(long chatId);
    public void Save(long chatId, ConversationState state);
}