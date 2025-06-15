using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Car_Insurance;

public class Program
{
    private static string? _botToken;
    private static string? _mindeeApiKey;
    private static string? _huggingFaceApiKey;

    private static ITelegramBotClient? _bot;
    private const string? MindeePath = "https://api.mindee.net/v1/products/your_api_endpoint";

    private const string HuggingFacePath =
        "https://api-inference.huggingface.co/models/HuggingFaceH4/zephyr-7b-beta";

    // Conversation state flags
    private static bool _awaitingPassport;
    private static bool _awaitingVehicleId;
    private static bool _awaitingPriceConfirmation;

    // Store files
    private static byte[] _passportPicture = [];
    private static byte[] _vehicleIdPicture = [];

    private const string ErrorMessage = "🚨 I am very sorry to inform you, the error was encountered.\n" +
                                        "Please, try again in a few minutes!";

    private static async Task Main()
    {
        DotNetEnv.Env.TraversePath().Load();

        _botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        _mindeeApiKey = Environment.GetEnvironmentVariable("MINDEE_API_TOKEN");
        _huggingFaceApiKey = Environment.GetEnvironmentVariable("HUGGINGFACE_API_TOKEN");

        if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_huggingFaceApiKey))
        {
            Console.WriteLine("[FAILED] Bot or API key not found.");
            return;
        }

        _bot = new TelegramBotClient(_botToken);
        var me = await _bot.GetMe();

        Console.WriteLine($"[Success] Bot started as @{me.Username}");

        var cts = new CancellationTokenSource();

        _bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            cancellationToken: cts.Token);

        Console.WriteLine("\tPress Enter to quit!");
        Console.ReadLine();

        await cts.CancelAsync();
    }

    private static async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken cancellation
    )
    {
        try
        {
            if (update.Type == UpdateType.Message)
            {
                var msg = update.Message;

                if (msg == null) return;

                if (msg.Text == "/start")
                {
                    await bot.SendMessage(
                        msg.Chat.Id,
                        "👋 Welcome! I'll help you process your car insurance.\n\nPlease submit a photo of your Passport first.");

                    _awaitingPassport = true;
                    return;
                }

                if (msg.Photo?.Length > 0)
                {
                    var photo = msg.Photo[^1];
                    var file = await bot.GetFile(photo.FileId, cancellation);
                    var stream = new MemoryStream();
                    await bot.DownloadFile(file.FilePath!, stream, cancellation);
                    stream.Position = 0;

                    if (_awaitingPassport)
                    {
                        _passportPicture = stream.ToArray();
                        _awaitingPassport = false;

                        _awaitingVehicleId = true;

                        await bot.SendMessage(
                            msg.Chat.Id,
                            "✅ Passport received. Now please submit your vehicle identification document.");
                    }
                    else if (_awaitingVehicleId)
                    {
                        _vehicleIdPicture = stream.ToArray();
                        _awaitingVehicleId = false;

                        await bot.SendMessage(
                            msg.Chat.Id,
                            "✅ Vehicle identification document received.");

                        await ExtractAndConfirmData(bot, msg.Chat.Id);
                    }
                }
                else if (_awaitingPriceConfirmation)
                {
                    if (msg.Text!.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                    {
                        _awaitingPriceConfirmation = false;

                        await bot.SendMessage(
                            msg.Chat.Id,
                            "🎉 Thank you! The policy will be issued shortly.");

                        var policy = GeneratePolicyDocument();

                        await bot.SendDocument(
                            msg.Chat.Id,
                            new InputFileStream(new MemoryStream(policy), "CarInsurancePolicy.pdf"),
                            caption: "Here’s your car insurance policy.");

                        await bot.SendMessage(
                            msg.Chat.Id,
                            "✨ Your policy is all set! If you have any questions, just let me know.");

                        await bot.SendMessage(
                            msg.Chat.Id,
                            "🤔 Do you have any questions about your policy or coverage? Please let me know.");
                    }
                    else
                    {
                        await bot.SendMessage(
                            msg.Chat.Id,
                            "❌ Sorry, we can’t proceed without your confirmation.\n" +
                            "Please, agree with the price for car insurance of 100 USD.\n\n" +
                            "Do you agree? (Yes/No)");
                    }
                }
                else
                {
                    var response = await GetHuggingFaceResponse(msg.Text!);
                    await bot.SendMessage(msg.Chat.Id, response);
                }
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                var callback = update.CallbackQuery;

                if (callback == null) return;

                var chatId = callback.Message!.Chat.Id;

                if (callback.Data == "confirm_yes")
                {
                    await bot.SendMessage(chatId, "✅ Information Confirmed!");
                    await bot.SendMessage(chatId, "The price for car insurance is 100 USD. Do you agree? (Yes/No)");
                    _awaitingPriceConfirmation = true;
                }
                else if (callback.Data == "confirm_no")
                {
                    await bot.SendMessage(chatId, "❌ Information Incorrect. Please resubmit your documents.");
                    _awaitingPassport = true;
                }

                await bot.AnswerCallbackQuery(callback.Id);
            }
        }
        catch (Exception)
        {
            var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;

            if (chatId.HasValue)
            {
                await bot.SendMessage(
                    chatId.Value,
                    ErrorMessage
                );
            }
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient bot, Exception exception,
        CancellationToken cancellation)
    {
        var error = $"[Error] {exception}";
        Console.WriteLine(error);
        return Task.CompletedTask;
    }

    private static byte[] GeneratePolicyDocument()
    {
        var policyContent = "This policy covers your vehicle against accidents and theft.";

        QuestPDF.Settings.License = LicenseType.Community;
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Header().AlignCenter().Text("Car Insurance Policy").FontSize(20).Bold();
                page.Content().Text(policyContent);
                page.Footer().AlignCenter().Text($"Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}.");
            });
        });

        using (var stream = new MemoryStream())
        {
            document.GeneratePdf(stream);
            return stream.ToArray();
        }
    }


    private static async Task ExtractAndConfirmData(ITelegramBotClient bot, long chatId)
    {
        await bot.SendMessage(chatId, "🟣 Extracting data from documents, please wait.");

        var extracted = await ExtractWithMindeeAsync(_passportPicture);

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

        await bot.SendMessage(
            chatId,
            text,
            replyMarkup: markup
        );
    }

    private static async Task<ExtractedData> ExtractWithMindeeAsync(byte[] passportPicture)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
        await File.WriteAllBytesAsync(tempPath, passportPicture);


        var mindeeClient = new Mindee.MindeeClient(_mindeeApiKey);
        var inputSource = new Mindee.Input.LocalInputSource(tempPath);

        var response = await mindeeClient.ParseAsync<Mindee.Product.Passport.PassportV1>(inputSource);

        var parsed = response.Document.Inference.Prediction;

        var extracted = new ExtractedData
        {
            PassportName = parsed.GivenNames?.FirstOrDefault()?.ToString() ?? "Unknown",
            PassportSurname = parsed.Surname?.ToString() ?? "Unknown",
            PassportId = parsed.IdNumber.ToString() ?? "Unknown",
            VehicleId = "V-909091" // Mocking vehicle id
        };

        File.Delete(tempPath);
        return extracted;
    }

    // HuggingFace API free plan instead of OpenAI API, in order to avoid charges.
    private static async Task<string> GetHuggingFaceResponse(string userMessage)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _huggingFaceApiKey);

        var prompt = $"AI: You are a helpful assistant for a car insurance company. " +
                     $"Answer the user's question politely and clearly. " +
                     $"If you are unsure about the answer, apologize and suggest calling the support line. " +
                     $"Keep the tone friendly and concise. \n\nUser: {userMessage}";

        var request = new
        {
            inputs = prompt,
            parameters = new
            {
                max_new_tokens = 150,
                return_full_text = false,
                temperature = 0.7,
                top_p = 0.9
            }
        };

        var response = await httpClient.PostAsJsonAsync(HuggingFacePath, request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[ERROR] AI: {response.StatusCode} — {responseBody}");
            return ErrorMessage;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<HuggingFaceResponse>>(responseBody);
            return parsed?[0].GeneratedText?.Trim() ?? "Sorry, it seems I can't help you with a question.\n" +
                "Please contact our support line.";
        }
        catch
        {
            return "Sorry, I couldn't understand that. Please try asking differently.";
        }
    }
}