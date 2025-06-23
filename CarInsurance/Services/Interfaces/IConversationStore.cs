using CarInsurance.Models;

namespace CarInsurance.Services.Interfaces;

public interface IConversationStore
{
    public ConversationState Get(long chatId);
    public void Save(long chatId, ConversationState state);
}