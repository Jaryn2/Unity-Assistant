using UnityEditor;

namespace UnityAssistant.Editor.Services
{
    public static class OpenAISettings
    {
        private const string ApiKeyPrefKey = "UnityAssistant.OpenAI.ApiKey";
        private const string ModelPrefKey = "UnityAssistant.OpenAI.Model";

        public static string ApiKey
        {
            get => EditorPrefs.GetString(ApiKeyPrefKey, "");
            set => EditorPrefs.SetString(ApiKeyPrefKey, value ?? "");
        }

        public static string Model
        {
            get => EditorPrefs.GetString(ModelPrefKey, "gpt-5.4");
            set => EditorPrefs.SetString(ModelPrefKey, string.IsNullOrWhiteSpace(value) ? "gpt-5.4" : value);
        }
    }
}