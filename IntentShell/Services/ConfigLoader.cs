using Microsoft.Extensions.Configuration;
using System;
using EasyCommand.Services; // add at top of file
namespace EasyCommand.Services;


public static class ConfigLoader
{
    /// <summary>
    /// Loads configuration with safe defaults.
    ///
    /// API key precedence:
    /// 1) Environment variable OPENAI_API_KEY
    /// 2) User Secrets (dev)
    /// 3) appsettings.json (should remain empty in source control)
    /// </summary>
    public static AppConfig Load(string basePath)
    {
        var cfg = LoadAllowMissingKey(basePath);
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is not configured. Set OPENAI_API_KEY environment variable or use User Secrets (OpenAI:ApiKey)."
            );
        }
        return cfg;
    }

    /// <summary>
    /// Loads configuration but does not throw when the API key is missing.
    /// This enables a first-run UI flow where the app prompts for the key.
    /// </summary>
public static AppConfig LoadAllowMissingKey(string basePath)
{
    IConfiguration config = new ConfigurationBuilder()
        .SetBasePath(basePath)
        .AddJsonFile("appsettings.json", optional: true)
        .Build();

string? envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
string? storedKey = ApiKeyStore.Load();

string apiKey =
    (!string.IsNullOrWhiteSpace(envKey) ? envKey.Trim() : null)
    ?? storedKey
    ?? (config["OpenAI:ApiKey"] ?? string.Empty).Trim();

string model = (config["OpenAI:Model"] ?? "gpt-4o-mini").Trim();
string defaultPrompt = (config["OpenAI:DefaultPrompt"] ?? string.Empty).Trim();

// IMPORTANT: match your AppConfig parameter order
return new AppConfig(apiKey, model, defaultPrompt);
}
}
