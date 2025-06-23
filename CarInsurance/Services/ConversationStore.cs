using System.Collections.Concurrent;
using CarInsurance.Models;
using CarInsurance.Services.Interfaces;

namespace CarInsurance.Services;

/// <summary>
/// Store for tracking Telegram chat states.
/// </summary>
public sealed class ConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<long, ConversationState> _chats = new();


    /// <summary>
    /// Retrieves the current state of a user's conversation.
    /// If no state exists, a new one is initialized.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <returns>The current <see cref="ConversationState"/> for the user.</returns>
    public ConversationState Get(long chatId) =>
        _chats.GetOrAdd(chatId, _ => new ConversationState());

    /// <summary>
    /// Saves or updates the conversation state for a given chat ID.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="state">The new state to store.</param>
    public void Save(long chatId, ConversationState state) =>
        _chats[chatId] = state;
}