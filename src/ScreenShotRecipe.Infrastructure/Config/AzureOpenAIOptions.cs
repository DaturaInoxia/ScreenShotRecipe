namespace ScreenShotRecipe.Infrastructure.Config
{
    /// <summary>
    /// Configuration options for Azure OpenAI / OpenAI API credentials.
    /// Secrets should be stored in user-secrets (development), environment variables (Docker),
    /// or a secure vault (cloud deployment) - NOT in source control.
    /// </summary>
    /// <remarks>
    /// Configuration sources (in order of precedence):
    /// 1. Environment variables: AZURE_OPENAI__ENDPOINT, AZURE_OPENAI__APIKEY, etc.
    /// 2. User secrets (dotnet user-secrets): for local development
    /// 3. appsettings.{Environment}.json
    /// 4. appsettings.json (non-secret values only)
    /// </remarks>
    public class AzureOpenAIOptions
    {
        public const string SectionName = "AzureOpenAI";

        /// <summary>
        /// Azure OpenAI endpoint URL (e.g., "https://your-resource.openai.azure.com/")
        /// Environment variable: AZURE_OPENAI__ENDPOINT
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Azure OpenAI API key.
        /// Environment variable: AZURE_OPENAI__APIKEY
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Azure OpenAI deployment name for GPT-4o (e.g., "gpt-4o")
        /// Environment variable: AZURE_OPENAI__DEPLOYMENTNAME
        /// </summary>
        public string? DeploymentName { get; set; }

        /// <summary>
        /// Alternative: Direct OpenAI API key (when not using Azure).
        /// Environment variable: AZURE_OPENAI__OPENAIAPIKEY
        /// </summary>
        public string? OpenAIApiKey { get; set; }

        /// <summary>
        /// Model name for direct OpenAI API (default: "gpt-4o").
        /// Environment variable: AZURE_OPENAI__OPENAIMODEL
        /// </summary>
        public string OpenAIModel { get; set; } = "gpt-4o";

        /// <summary>
        /// Maximum tokens for the response (default: 4096).
        /// Environment variable: AZURE_OPENAI__MAXTOKENS
        /// </summary>
        public int MaxTokens { get; set; } = 4096;

        /// <summary>
        /// Temperature for generation (0.0-2.0, lower = more deterministic, default: 0.2).
        /// Environment variable: AZURE_OPENAI__TEMPERATURE
        /// </summary>
        public double Temperature { get; set; } = 0.2;

        /// <summary>
        /// Request timeout in seconds (default: 120).
        /// Environment variable: AZURE_OPENAI__TIMEOUTSECONDS
        /// </summary>
        public int TimeoutSeconds { get; set; } = 120;

        /// <summary>
        /// Whether Azure OpenAI is configured (endpoint and API key present).
        /// </summary>
        public bool UseAzure => !string.IsNullOrEmpty(Endpoint) && !string.IsNullOrEmpty(ApiKey);

        /// <summary>
        /// Whether any API credentials are configured (Azure or direct OpenAI).
        /// </summary>
        public bool IsConfigured => UseAzure || !string.IsNullOrEmpty(OpenAIApiKey);

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        /// <param name="errors">List of validation errors if invalid.</param>
        /// <returns>True if configuration is valid, false otherwise.</returns>
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (!IsConfigured)
            {
                errors.Add("No API credentials configured. Set either Azure OpenAI (Endpoint + ApiKey) or OpenAIApiKey.");
                return false;
            }

            if (UseAzure)
            {
                if (string.IsNullOrEmpty(DeploymentName))
                    errors.Add("DeploymentName is required when using Azure OpenAI");
            }

            if (MaxTokens < 100 || MaxTokens > 128000)
                errors.Add("MaxTokens must be between 100 and 128000");

            if (Temperature < 0 || Temperature > 2)
                errors.Add("Temperature must be between 0 and 2");

            if (TimeoutSeconds < 10 || TimeoutSeconds > 600)
                errors.Add("TimeoutSeconds must be between 10 and 600");

            return errors.Count == 0;
        }

        /// <summary>
        /// Gets a display-safe summary of the configuration (no secrets).
        /// </summary>
        public string GetSafeConfigSummary()
        {
            if (!IsConfigured)
                return "Not configured";

            if (UseAzure)
            {
                var endpoint = Endpoint ?? "unknown";
                var maskedEndpoint = endpoint.Length > 30 
                    ? endpoint.Substring(0, 30) + "..." 
                    : endpoint;
                return $"Azure OpenAI: {maskedEndpoint}, Deployment: {DeploymentName ?? "not set"}";
            }

            return $"OpenAI API: {OpenAIModel}, MaxTokens: {MaxTokens}";
        }
    }
}
