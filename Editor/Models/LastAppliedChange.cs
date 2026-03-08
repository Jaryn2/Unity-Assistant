using System;

namespace UnityAssistant.Editor.Models
{
    [Serializable]
    public class LastAppliedChange
    {
        public string filePath;
        public string previousContent;
        public bool fileExistedBefore;
    }
}