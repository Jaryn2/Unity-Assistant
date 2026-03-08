using System;

namespace UnityAssistant.Editor.Models
{
    [Serializable]
    public class FeaturePlan
    {
        public string title;
        public string summary;
        public string[] goals;
        public string[] steps;
        public string[] filesToModify;
        public string[] filesToCreate;
        public string[] risks;

        public EditorSetupInstruction[] editorSetup;
    }
}