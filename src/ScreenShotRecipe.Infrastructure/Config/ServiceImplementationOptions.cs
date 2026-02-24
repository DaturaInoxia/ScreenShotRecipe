namespace ScreenShotRecipe.Infrastructure.Config
{
    /// <summary>
    /// Configuration options for controlling fake vs. real service implementations.
    /// Allows control via appsettings.json or environment variables.
    /// </summary>
    public class ServiceImplementationOptions
    {
        public const string SectionName = "ServiceImplementation";

        /// <summary>
        /// If true, use real external services (GPT-4o for OCR, etc.).
        /// If false, use fake/mock implementations (default for testing).
        /// </summary>
        public bool UseRealOcr { get; set; } = false;

        /// <summary>
        /// If true, use real LLM parser (Azure OpenAI, GPT-4o, etc.).
        /// If false, use fake parser that parses OCR text deterministically.
        /// </summary>
        public bool UseRealLlmParser { get; set; } = false;

        /// <summary>
        /// Override confidence level for fake LLM parser (1-100, null = auto-calculate).
        /// Useful for testing different parsing quality scenarios.
        /// Example: 75 = return recipes with 75% confidence
        /// </summary>
        public int? FakeLLMParserConfidenceOverride { get; set; }

        /// <summary>
        /// Environment variable override for UseRealOcr (format: upper-case with underscores).
        /// Example: "OCR_USE_REAL=true" or "OCR_USE_REAL=false"
        /// </summary>
        public bool? EnvironmentOverrideUseRealOcr { get; set; }

        /// <summary>
        /// Environment variable override for UseRealLlmParser.
        /// Example: "PARSER_USE_REAL=true"
        /// </summary>
        public bool? EnvironmentOverrideUseRealParser { get; set; }

        /// <summary>
        /// Validates configuration options.
        /// </summary>
        public bool IsValid(out List<string> errors)
        {
            errors = new();

            if (FakeLLMParserConfidenceOverride.HasValue)
            {
                if (FakeLLMParserConfidenceOverride < 1 || FakeLLMParserConfidenceOverride > 100)
                    errors.Add("FakeLLMParserConfidenceOverride must be between 1 and 100.");
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Applies environment variable overrides to configuration.
        /// </summary>
        public void ApplyEnvironmentOverrides()
        {
            // Check for OCR_USE_REAL environment variable
            if (bool.TryParse(Environment.GetEnvironmentVariable("OCR_USE_REAL"), out var ocrReal))
            {
                UseRealOcr = ocrReal;
            }

            // Check for PARSER_USE_REAL environment variable
            if (bool.TryParse(Environment.GetEnvironmentVariable("PARSER_USE_REAL"), out var parserReal))
            {
                UseRealLlmParser = parserReal;
            }

            // Check for PARSER_CONFIDENCE_OVERRIDE environment variable
            if (int.TryParse(Environment.GetEnvironmentVariable("PARSER_CONFIDENCE_OVERRIDE"), out var confidence))
            {
                if (confidence >= 1 && confidence <= 100)
                {
                    FakeLLMParserConfidenceOverride = confidence;
                }
            }
        }
    }
}
