using UnityEditor;

namespace UnityAssistant.Editor.Services
{
    public static class OpenAISettings
    {
        private const string ApiKeyPrefKey = "UnityAssistant.OpenAI.ApiKey";
        private const string PlanningModelPrefKey = "UnityAssistant.OpenAI.PlanningModel";
        private const string ImplementationModelPrefKey = "UnityAssistant.OpenAI.ImplementationModel";

        public static string ApiKey
        {
            get => EditorPrefs.GetString(ApiKeyPrefKey, "");
            set => EditorPrefs.SetString(ApiKeyPrefKey, value ?? "");
        }

        public static string PlanningModel
        {
            get => EditorPrefs.GetString(PlanningModelPrefKey, "gpt-5-mini");
            set => EditorPrefs.SetString(
                PlanningModelPrefKey,
                string.IsNullOrWhiteSpace(value) ? "gpt-5-mini" : value
            );
        }

        public static string ImplementationModel
        {
            get => EditorPrefs.GetString(ImplementationModelPrefKey, "gpt-5.4");
            set => EditorPrefs.SetString(
                ImplementationModelPrefKey,
                string.IsNullOrWhiteSpace(value) ? "gpt-5.4" : value
            );
        }
    }
}