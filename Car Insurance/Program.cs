using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Car_Insurance;

public class Program
{
    private static string? _botToken;
    private static TelegramBotClient? _bot;

    private static async Task Main()
    {
        DotNetEnv.Env.TraversePath().Load();
        _botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        using var cts = new CancellationTokenSource();

        if (_botToken != null)
        {
            _bot = new TelegramBotClient(_botToken, cancellationToken: cts.Token);
            var me = await _bot.GetMe(cancellationToken: cts.Token);
            Console.WriteLine($"[Success] - [LAUNCH] - [{DateTime.UtcNow}]\n\t@{me.Username}");
            _bot.OnMessage += HandleMessageAsync;
            Console.WriteLine("Press Enter to quit.");
            Console.ReadLine();
        }
        else
            Console.WriteLine($"[FAILED] - [Token] - [{DateTime.UtcNow}]\n\tBot token not found.");

        await cts.CancelAsync();
    }

    private static async Task HandleMessageAsync(Message msg, UpdateType _)
    {
        if (msg.Text is null)
            return;
        // Stage 1
        if (msg.Text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _bot!.SendMessage(
                msg.Chat,
                "👋 Welcome! I’m your assistant for purchasing car insurance.\n\n" +
                "📄 I’ll walk you through the whole process step‑by‑step. \n\n" +
                "ℹ️ Type /help at any time for assistance.");
        }
        else
            await _bot!.SendMessage(
                msg.Chat,
                "Please type /start to begin."
            );
    }
}