using System.Collections.Concurrent;
using Car_Insurance.Models;
using Car_Insurance.Services.Interfaces;

namespace Car_Insurance.Services;

public sealed class ConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<long, ConversationState> _chats = new();

    public ConversationState Get(long chatId) =>
        _chats.GetOrAdd(chatId, _ => new ConversationState());

    public void Save(long chatId, ConversationState state) =>
        _chats[chatId] = state;
}