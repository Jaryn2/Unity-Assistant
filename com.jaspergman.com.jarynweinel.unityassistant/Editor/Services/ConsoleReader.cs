using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityAssistant.Editor.Services
{
    [InitializeOnLoad]
    public static class ConsoleReader
    {
        private const int MaxEntries = 100;
        private static readonly List<ConsoleMessageData> Messages = new List<ConsoleMessageData>();

        static ConsoleReader()
        {
            Application.logMessageReceived += HandleLogMessageReceived;
        }

        private static void HandleLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            Messages.Add(new ConsoleMessageData
            {
                type = type.ToString(),
                message = condition,
                stackTrace = stackTrace,
                timestamp = DateTime.Now.ToString("O")
            });

            if (Messages.Count > MaxEntries)
            {
                Messages.RemoveAt(0);
            }
        }

        public static ConsoleMessageData[] GetRelevantMessages(int maxMessages = 10)
        {
            return Messages
                .Where(m =>
                    m.type == LogType.Error.ToString() ||
                    m.type == LogType.Exception.ToString() ||
                    m.type == LogType.Assert.ToString() ||
                    m.type == LogType.Warning.ToString())
                .Reverse()
                .Take(maxMessages)
                .Reverse()
                .ToArray();
        }

        public static void ClearCapturedMessages()
        {
            Messages.Clear();
        }
    }

    [Serializable]
    public class ConsoleMessageData
    {
        public string type;
        public string message;
        public string stackTrace;
        public string timestamp;
    }
}