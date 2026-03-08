using System;
using System.IO;
using UnityEditor;
using UnityAssistant.Editor.Models;

namespace UnityAssistant.Editor.Services
{
    public static class PatchApplier
    {
        public static LastAppliedChange ApplyPatch(FilePatch patch)
        {
            if (patch == null)
                throw new Exception("Patch is null.");

            if (string.IsNullOrWhiteSpace(patch.filePath))
                throw new Exception("Patch file path is missing.");

            if (patch.newContent == null)
                throw new Exception("Patch new content is null.");

            string fullPath = Path.GetFullPath(patch.filePath);
            string normalized = fullPath.Replace("\\", "/");

            if (!normalized.Contains("/Assets/"))
                throw new Exception("Only files inside Assets are allowed in this prototype.");

            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new Exception("Invalid patch directory.");

            bool fileExistedBefore = File.Exists(fullPath);
            string previousContent = fileExistedBefore ? File.ReadAllText(fullPath) : "";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, patch.newContent);
            AssetDatabase.Refresh();

            return new LastAppliedChange
            {
                filePath = patch.filePath,
                previousContent = previousContent,
                fileExistedBefore = fileExistedBefore
            };
        }

        public static void RevertLastChange(LastAppliedChange change)
        {
            if (change == null)
                throw new Exception("No last change to revert.");

            if (string.IsNullOrWhiteSpace(change.filePath))
                throw new Exception("Last change file path is missing.");

            string fullPath = Path.GetFullPath(change.filePath);
            string normalized = fullPath.Replace("\\", "/");

            if (!normalized.Contains("/Assets/"))
                throw new Exception("Only files inside Assets are allowed in this prototype.");

            if (change.fileExistedBefore)
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, change.previousContent ?? "");
            }
            else
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                string metaPath = fullPath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }

            AssetDatabase.Refresh();
        }
    }
}