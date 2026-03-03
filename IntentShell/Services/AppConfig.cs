namespace EasyCommand.Services;

public sealed record AppConfig(
    string ApiKey,
    string Model,
    string DefaultPrompt
);
