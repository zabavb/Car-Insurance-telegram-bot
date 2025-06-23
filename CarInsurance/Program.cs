using CarInsurance.Options;
using CarInsurance.Services;
using CarInsurance.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

var host = Host.CreateDefaultBuilder(args)
    // Configurations for using credentials from appsettings.Development.json file
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables(); // JSON can be overridden by env vars
    })
    .ConfigureServices((ctx, services) =>
    {
        // Configurations for external APIs
        // HuggingFace API (AI assistant)
        services.AddOptions<ChatOptions>()
            .Bind(ctx.Configuration.GetSection("HuggingFace"))
            .ValidateDataAnnotations()
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiToken), "ApiToken required")
            .ValidateOnStart();
        // Mindee API (data extraction from image)
        services.AddOptions<OcrOptions>()
            .Bind(ctx.Configuration.GetSection("Mindee"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "ApiKey required")
            .ValidateOnStart();

        // Telegram bot
        var botKey = ctx.Configuration["Telegram:BotToken"]
                     ?? throw new InvalidOperationException("Telegram:BotToken missing");
        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botKey));

        // Handling DI via registration
        services.AddSingleton<IConversationStore, ConversationStore>();
        services.AddSingleton<IPolicyService, PolicyService>();
        services.AddSingleton<IOcrService, OcrService>();

        services.AddHttpClient<IChatService, ChatService>();
        services.AddHostedService<BotService>();
    })
    // Configuration of logging
    .ConfigureLogging(b => b
        .AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        }))
    .UseConsoleLifetime() // Allows Ctrl-C for exit
    .Build();

await host.RunAsync();