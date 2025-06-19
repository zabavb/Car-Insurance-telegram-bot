using Car_Insurance.Models;
using Car_Insurance.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Car_Insurance.Services;

/// <summary>
/// The main hosted service that manages Telegram Bot operations.
/// Handles messages, document uploads, conversation flow, and AI integration.
/// </summary>
/// <param name="bot">Injected Telegram bot client.</param>
/// <param name="store">In-memory conversation store for tracking user state.</param>
/// <param name="ocr">OCR service for parsing passport documents.</param>
/// <param name="policy">Service that generates policy PDF files.</param>
/// <param name="chat">Service that integrates with an AI assistant.</param>
/// <param name="log">Logger instance for diagnostic and error logging.</param>
public sealed class BotService(
    ITelegramBotClient bot,
    IConversationStore store,
    IOcrService ocr,
    IPolicyService policy,
    IChatService chat,
    ILogger<BotService> log)
    : IHostedService
{
    private readonly ITelegramBotClient _bot = bot;
    private readonly IConversationStore _store = store;
    private readonly IOcrService _ocr = ocr;
    private readonly IPolicyService _policy = policy;
    private readonly IChatService _chat = chat;
    private readonly ILogger<BotService> _log = log;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Initializes and starts the Telegram bot update listener.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the service gracefully.</param>
    /// <returns>A completed task when the bot listener starts successfully.</returns>
    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = [] },
            cancellationToken: _cts.Token);

        _log.LogInformation("Bot service started.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the Telegram bot
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the service gracefully.</param>
    /// <returns>A task that completes once the service stops.</returns>
    public async Task StopAsync(CancellationToken ct)
    {
        await _cts?.CancelAsync()!;
        _log.LogInformation("Bot service stopped.");
    }

    /// <summary>
    /// Handles all incoming updates from Telegram, including messages and callback queries.
    /// </summary>
    /// <param name="bot">Telegram bot client used to send and receive messages.</param>
    /// <param name="u">The received update from Telegram.</param>
    /// <param name="ct">Cancellation token for graceful task cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update u, CancellationToken ct)
    {
        switch (u.Type)
        {
            case UpdateType.CallbackQuery when u.CallbackQuery == null:
                return;

            // Checking user's response on correctness of extracted data from documents 
            case UpdateType.CallbackQuery:
            {
                var callback = u.CallbackQuery;
                var chatId = callback.Message!.Chat.Id;

                if (callback.Data == "confirm_yes") // If correct
                {
                    await bot.SendMessage(chatId,
                        "✅ Information Confirmed!",
                        cancellationToken: ct);
                    await bot.SendMessage(chatId,
                        "The price for car insurance is 100 USD. Do you agree? (Yes/No)",
                        cancellationToken: ct);

                    var state = _store.Get(chatId);
                    _store.Save(chatId, state with { CurrentStage = Stage.WaitingPrice }); // Step forward 
                }
                else if (callback.Data == "confirm_no") // If incorrect
                {
                    await bot.SendMessage(chatId,
                        "❌ Information Incorrect. Please resubmit your documents.",
                        cancellationToken: ct);
                    var state = _store.Get(chatId);
                    _store.Save(chatId, state with { CurrentStage = Stage.WaitingPassport }); // Repeat image attachment
                }

                await bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
                return;
            }

            // If user sent text message (most of the time)
            case UpdateType.Message:
            {
                var msg = u.Message!;
                var state = _store.Get(msg.Chat.Id);

                // If message is text and doesn't match required flow triggers (like 'yes')
                // use AI assistant to respond to it
                if (msg.Text is not null &&
                    !msg.Text.Equals("yes", StringComparison.OrdinalIgnoreCase) &&
                    !msg.Text.Equals("no", StringComparison.OrdinalIgnoreCase) &&
                    !msg.Text.StartsWith('/')) // Avoid pre-defined inputs (Example: "/start")
                {
                    var resp = await _chat.AskAsync(msg.Text, state.CurrentStage, ct);
                    await bot.SendMessage(msg.Chat.Id, resp, cancellationToken: ct);
                    return;
                }


                switch (state.CurrentStage)
                {
                    // Receiving first image
                    case Stage.WaitingPassport when msg.Photo is { Length: > 0 }:
                        var passport = await DownloadAsync(msg.Photo[^1], ct); // Downloading only first attachment
                        _store.Save(msg.Chat.Id, state with
                        {
                            CurrentStage = Stage.WaitingVehicleDoc,
                            Passport = passport
                        });

                        await bot.SendMessage(msg.Chat.Id,
                            "✅ Passport received. Now send your vehicle ID document.",
                            cancellationToken: ct);
                        break;

                    // Receiving second image
                    case Stage.WaitingVehicleDoc when msg.Photo is { Length: > 0 }:
                        var vehicle = await DownloadAsync(msg.Photo[^1], ct);
                        _store.Save(msg.Chat.Id, state with
                        {
                            CurrentStage = Stage.WaitingPrice,
                            VehicleDoc = vehicle
                        });

                        await bot.SendMessage(msg.Chat.Id,
                            "✅ Vehicle document received.",
                            cancellationToken: ct);

                        await bot.SendMessage(msg.Chat.Id,
                            "🟣 Extracting data from documents, please wait.",
                            cancellationToken: ct);

                        // Extracting data from images
                        var extracted = await _ocr.ParsePassportAsync(state.Passport!, ct);

                        var text = $"✅ We extracted the following information:\n" +
                                   $"- Name: {extracted.PassportName}\n" +
                                   $"- Surname: {extracted.PassportSurname}\n" +
                                   $"- Passport IDs: {extracted.PassportId}\n" +
                                   $"- Vehicle IDs: {extracted.VehicleId}\n\n" +
                                   $"✨ Is this correct?";

                        // Asking user if everything correct
                        var markup = new InlineKeyboardMarkup(
                        [
                            [
                                InlineKeyboardButton.WithCallbackData("Yes", "confirm_yes"),
                                InlineKeyboardButton.WithCallbackData("No", "confirm_no")
                            ]
                        ]);

                        await bot.SendMessage(msg.Chat.Id,
                            text,
                            replyMarkup: markup,
                            cancellationToken: ct);
                        break;

                    // If user agreed with the price
                    case Stage.WaitingPrice when msg.Text?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true:
                        _store.Save(msg.Chat.Id, state with { CurrentStage = Stage.Complete });

                        await bot.SendMessage(msg.Chat.Id,
                            "🎉 Great! Your policy will be issued shortly.",
                            cancellationToken: ct);
                        // Proceed forward
                        var policy = _policy.Generate();

                        await bot.SendDocument(msg.Chat.Id,
                            new InputFileStream(new MemoryStream(policy), "CarInsurancePolicy.pdf"),
                            caption: "Here’s your car insurance policy.",
                            cancellationToken: ct);

                        await bot.SendMessage(msg.Chat.Id,
                            "✨ Your policy is all set! If you have any questions, just let me know.",
                            cancellationToken: ct);
                        break;

                    // If user haven't agreed with the price
                    case Stage.WaitingPrice when msg.Text?.Equals("yes", StringComparison.OrdinalIgnoreCase) == false:
                        _store.Save(msg.Chat.Id, state with { CurrentStage = Stage.WaitingPrice });
                        // Asking to agree anyway
                        await bot.SendMessage(
                            msg.Chat.Id,
                            "❌ Sorry, we can’t proceed without your confirmation.\n" +
                            "Please, agree with the price for car insurance of 100 USD.\n\n" +
                            "Do you agree? (Yes/No)",
                            cancellationToken: ct);
                        break;

                    // Joining AI assistant to conversation
                    /*case Stage.Complete:
                        var resp = await _chat.AskAsync(msg.Text!, ct); // Sending user's question to AI assistant
                        await bot.SendMessage(msg.Chat.Id,
                            resp,
                            cancellationToken: ct);
                        break;*/

                    default:
                        // The beginning
                        if (msg.Text == "/start")
                        {
                            await bot.SendMessage(msg.Chat.Id,
                                "👋 Welcome! I'll help you process your car insurance.\n\n" +
                                "Please submit a photo of your Passport first.",
                                cancellationToken: ct);
                        }
                        // In case of incorrect user's typo or misunderstanding we are asking to follow bot's instructions
                        else
                        {
                            await bot.SendMessage(msg.Chat.Id,
                                "❌ Incorrect answer... Please follow instructions!",
                                cancellationToken: ct);
                        }

                        break;
                }

                break;
            }
        }
    }


    /// <summary>
    /// Logs and handles unexpected errors during bot execution.
    /// </summary>
    /// <param name="_">Unused: the bot client.</param>
    /// <param name="ex">The exception that was thrown.</param>
    /// <param name="__">Unused: cancellation token.</param>
    /// <returns>A completed task after logging the error.</returns>
    private Task HandleErrorAsync(ITelegramBotClient _, Exception ex, CancellationToken __)
    {
        var err = ex switch
        {
            ApiRequestException api => $"Telegram API error:\n[{api.ErrorCode}] {api.Message}",
            _ => ex.ToString()
        };
        _log.LogWarning(err);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Downloads a user-submitted photo from Telegram.
    /// </summary>
    /// <param name="photo">The specific size variant of the photo to download.</param>
    /// <param name="ct">Cancellation token for graceful task cancellation.</param>
    /// <returns>A byte array containing the downloaded image.</returns>
    private async Task<byte[]> DownloadAsync(PhotoSize photo, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        var f = await _bot.GetFile(photo.FileId, ct);
        await _bot.DownloadFile(f.FilePath!, ms, ct);
        return ms.ToArray();
    }
}