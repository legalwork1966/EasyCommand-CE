namespace IntentShell.Services;

public sealed record AppConfig(
    string ApiKey,
    string Model,
    string DefaultPrompt
);
