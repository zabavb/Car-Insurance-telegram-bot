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

    public async Task StopAsync(CancellationToken ct)
    {
        await _cts?.CancelAsync()!;
        _log.LogInformation("Bot service stopped.");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update u, CancellationToken ct)
    {
        switch (u.Type)
        {
            //==========================================
            case UpdateType.CallbackQuery when u.CallbackQuery == null:
                return;
            case UpdateType.CallbackQuery:
            {
                var callback = u.CallbackQuery;
                var chatId = callback.Message!.Chat.Id;

                if (callback.Data == "confirm_yes")
                {
                    await bot.SendMessage(chatId,
                        "✅ Information Confirmed!",
                        cancellationToken: ct);
                    await bot.SendMessage(chatId,
                        "The price for car insurance is 100 USD. Do you agree? (Yes/No)",
                        cancellationToken: ct);

                    var state = _store.Get(chatId);
                    _store.Save(chatId, state with { CurrentStage = Stage.WaitingPrice });
                }
                else if (callback.Data == "confirm_no")
                {
                    await bot.SendMessage(chatId,
                        "❌ Information Incorrect. Please resubmit your documents.",
                        cancellationToken: ct);
                    var state = _store.Get(chatId);
                    _store.Save(chatId, state with { CurrentStage = Stage.WaitingPassport });
                }

                await bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
                return;
            }
            
            case UpdateType.Message:
            {
                var msg = u.Message!;
                var state = _store.Get(msg.Chat.Id);

                switch (state.CurrentStage)
                {
                    // ---------- First image ----------
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

                    // ---------- Second image ----------
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

                        var extracted = await _ocr.ParsePassportAsync(state.Passport!, ct);

                        var text = $"✅ We extracted the following information:\n" +
                                   $"- Name: {extracted.PassportName}\n" +
                                   $"- Surname: {extracted.PassportSurname}\n" +
                                   $"- Passport IDs: {extracted.PassportId}\n" +
                                   $"- Vehicle IDs: {extracted.VehicleId}\n\n" +
                                   $"✨ Is this correct?";

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

                    // ---------- Price confirmation ----------
                    case Stage.WaitingPrice when msg.Text?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true:
                        _store.Save(msg.Chat.Id, state with { CurrentStage = Stage.Complete });

                        await bot.SendMessage(msg.Chat.Id,
                            "🎉 Great! Your policy will be issued shortly.",
                            cancellationToken: ct);

                        var policy = _policy.Generate();

                        await bot.SendDocument(msg.Chat.Id,
                            new InputFileStream(new MemoryStream(policy), "CarInsurancePolicy.pdf"),
                            caption: "Here’s your car insurance policy.",
                            cancellationToken: ct);

                        await bot.SendMessage(msg.Chat.Id,
                            "✨ Your policy is all set! If you have any questions, just let me know.",
                            cancellationToken: ct);
                        break;
                    case Stage.WaitingPrice when msg.Text?.Equals("yes", StringComparison.OrdinalIgnoreCase) == false:
                        _store.Save(msg.Chat.Id, state with { CurrentStage = Stage.WaitingPrice });

                        await bot.SendMessage(
                            msg.Chat.Id,
                            "❌ Sorry, we can’t proceed without your confirmation.\n" +
                            "Please, agree with the price for car insurance of 100 USD.\n\n" +
                            "Do you agree? (Yes/No)",
                            cancellationToken: ct);
                        break;
                    case Stage.Complete:
                        var resp = await _chat.AskAsync(msg.Text!, ct);
                        await bot.SendMessage(msg.Chat.Id,
                            resp,
                            cancellationToken: ct);
                        break;
                    default:
                        if (msg.Text == "/start")
                        {
                            await bot.SendMessage(msg.Chat.Id,
                                "👋 Welcome! I'll help you process your car insurance.\n\n" +
                                "Please submit a photo of your Passport first.",
                                cancellationToken: ct);
                        }
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

    // ===== Error handler =====
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

    // ===== Helpers =====
    private async Task<byte[]> DownloadAsync(PhotoSize photo, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        var f = await _bot.GetFile(photo.FileId, ct);
        await _bot.DownloadFile(f.FilePath!, ms, ct);
        return ms.ToArray();
    }
}