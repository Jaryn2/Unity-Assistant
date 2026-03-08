using System;
using System.IO;
using UnityEditor;
using UnityAssistant.Editor.Models;

namespace UnityAssistant.Editor.Services
{
    public static class PatchApplier
    {
        public static void ApplyPatch(FilePatch patch)
        {
            if (patch == null)
                throw new Exception("Patch is null.");

            if (string.IsNullOrWhiteSpace(patch.filePath))
                throw new Exception("Patch file path is missing.");

            if (patch.newContent == null)
                throw new Exception("Patch new content is null.");

            string fullPath = Path.GetFullPath(patch.filePath);

            if (!File.Exists(fullPath))
                throw new Exception("Target file does not exist: " + patch.filePath);

            string normalized = fullPath.Replace("\\", "/");
            if (!normalized.Contains("/Assets/"))
                throw new Exception("Only files inside Assets are allowed in this prototype.");

            File.WriteAllText(fullPath, patch.newContent);
            AssetDatabase.Refresh();
        }
    }
}