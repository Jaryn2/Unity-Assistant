using System;

namespace UnityAssistant.Editor.Models
{
    [Serializable]
    public class FilePatch
    {
        public string filePath;
        public string originalContent;
        public string newContent;
        public string reason;
    }
}