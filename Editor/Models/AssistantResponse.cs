using System;

namespace UnityAssistant.Editor.Models
{
    [Serializable]
    public class AssistantResponse
    {
        public string mode; // "plan" or "patch"
        public string summary;
        public string[] warnings;
        public string[] relevantFiles;
        public string nextAction;

        public FeaturePlan plan;
        public string planDocument;

        public FilePatch[] patches;
        public EditorSetupInstruction[] editorSetup;
    }
}