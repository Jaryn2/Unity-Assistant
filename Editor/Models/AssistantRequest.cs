using System;
using UnityAssistant.Editor.Services;

namespace UnityAssistant.Editor.Models
{
    [Serializable]
    public class AssistantRequest
    {
        public string prompt;
        public string selectedScriptPath;
        public string selectedScriptContent;

        public ScriptFileData[] relevantScripts;
        public ConsoleMessageData[] consoleMessages;

        public string manifestJson;
        public AsmdefFileData[] asmdefs;
    }
}