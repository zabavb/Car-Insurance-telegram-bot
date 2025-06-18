using Car_Insurance.Options;
using Car_Insurance.Services;
using Car_Insurance.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
           .AddEnvironmentVariables(); // JSON can be overridden by env vars
    })
    .ConfigureServices((ctx, services) =>
    {
        // ─── Bind and validate strongly‑typed options ───────────────────────
        services.AddOptions<ChatOptions>()
                .Bind(ctx.Configuration.GetSection("HuggingFace"))
                .ValidateDataAnnotations()
                .Validate(o => !string.IsNullOrWhiteSpace(o.ApiToken), "ApiToken required")
                .ValidateOnStart();

        services.AddOptions<OcrOptions>()
                .Bind(ctx.Configuration.GetSection("Mindee"))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "ApiKey required")
                .ValidateOnStart();

        // ─── Telegram client ────────────────────────────────────────────────
        var botKey = ctx.Configuration["Telegram:BotToken"]
                     ?? throw new InvalidOperationException("Telegram:BotToken missing");
        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botKey));

        // ─── Application services ───────────────────────────────────────────
        services.AddSingleton<IConversationStore, ConversationStore>();
        services.AddSingleton<IPolicyService, PolicyService>();
        services.AddSingleton<IOcrService, OcrService>();

        services.AddHttpClient<IChatService, ChatService>();  // typed HttpClient
        services.AddHostedService<BotService>();
    })
    .ConfigureLogging(b => b
        .AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        }))
    .UseConsoleLifetime()   // Allows Ctrl‑C for exit
    .Build();

await host.RunAsync();
