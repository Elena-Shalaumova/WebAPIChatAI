using System.Collections.Generic;

namespace WebAPIChatAI.Services
{
    public static class ModelCapabilities
    {
        private static readonly HashSet<string> VisionModels =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "qwen3-vl:2b",
                "qwen3-vl:2b-instruct-q4_K_M"
                //vision-модели
            };

        public static bool SupportsImages(string modelName)
            => VisionModels.Contains(modelName);
    }
}