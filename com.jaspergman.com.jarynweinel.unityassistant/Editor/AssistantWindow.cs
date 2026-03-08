using UnityEditor;
using UnityEngine;
using System.Text;
using UnityAssistant.Editor.Models;
using UnityAssistant.Editor.Services;

namespace UnityAssistant.Editor
{
    public class AssistantWindow : EditorWindow
    {
        private string prompt = "";
        private string output = "Assistant response will appear here.";

        private string apiKeyInput = "";
        private string modelInput = "";

        private string selectedScriptPath = "";
        private string selectedScriptPreview = "";

        private string symbolSearchInput = "";

        private bool isSending = false;
        private bool hasApprovedPlan = false;

        private Vector2 promptScroll;
        private Vector2 outputScroll;
        private Vector2 selectedScriptScroll;
        private Vector2 symbolSearchScroll;
        private Vector2 patchOldScroll;
        private Vector2 patchNewScroll;

        private AssistantResponse lastResponse;
        private AssistantResponse lastPlanResponse;
        private FeaturePlan approvedPlan;

        private int selectedPatchIndex = -1;

        [MenuItem("Window/Unity Assistant")]
        public static void ShowWindow()
        {
            AssistantWindow window = GetWindow<AssistantWindow>("Unity Assistant");
            window.apiKeyInput = OpenAISettings.ApiKey;
            window.modelInput = OpenAISettings.Model;
            window.RefreshSelectedScript();
            window.minSize = new Vector2(980, 920);
        }

        private void OnSelectionChange()
        {
            RefreshSelectedScript();
            Repaint();
        }

        private void OnFocus()
        {
            RefreshSelectedScript();
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Label("Unity Assistant", EditorStyles.boldLabel);
            GUILayout.Space(10);

            DrawOpenAISettings();
            GUILayout.Space(10);

            DrawSelectedScriptSection();
            GUILayout.Space(10);

            DrawCodeIndexSection();
            GUILayout.Space(10);

            DrawPrompt();
            GUILayout.Space(10);

            DrawButtons();
            GUILayout.Space(10);

            DrawOutput();
            GUILayout.Space(10);

            DrawPlanSection();
            GUILayout.Space(10);

            DrawPatchSection();
        }

        private void DrawOpenAISettings()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("OpenAI Settings", EditorStyles.boldLabel);

            apiKeyInput = EditorGUILayout.PasswordField("API Key", apiKeyInput);
            modelInput = EditorGUILayout.TextField("Model", modelInput);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save Settings"))
            {
                OpenAISettings.ApiKey = apiKeyInput;
                OpenAISettings.Model = modelInput;
            }

            if (GUILayout.Button("Reload"))
            {
                apiKeyInput = OpenAISettings.ApiKey;
                modelInput = OpenAISettings.Model;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "API key is stored locally using Unity EditorPrefs.",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedScriptSection()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("Selected Script", EditorStyles.boldLabel);

            EditorGUILayout.TextField(
                "Path",
                string.IsNullOrWhiteSpace(selectedScriptPath)
                    ? "No C# script selected in Project window"
                    : selectedScriptPath
            );

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Refresh Selection"))
            {
                RefreshSelectedScript();
            }

            if (GUILayout.Button("Clear Selected Script"))
            {
                selectedScriptPath = "";
                selectedScriptPreview = "";
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Preview", EditorStyles.miniBoldLabel);

            selectedScriptScroll = EditorGUILayout.BeginScrollView(selectedScriptScroll, GUILayout.Height(140));
            EditorGUILayout.TextArea(
                string.IsNullOrWhiteSpace(selectedScriptPreview)
                    ? "Click a .cs file in the Project window to load it here."
                    : selectedScriptPreview,
                GUILayout.ExpandHeight(true)
            );
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawCodeIndexSection()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("Code Index", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            symbolSearchInput = EditorGUILayout.TextField("Search", symbolSearchInput);

            if (GUILayout.Button("Rebuild Index", GUILayout.Width(120)))
            {
                CodeIndex.RebuildIndex();
            }

            EditorGUILayout.EndHorizontal();

            ScriptIndexEntry[] results = string.IsNullOrWhiteSpace(symbolSearchInput)
                ? new ScriptIndexEntry[0]
                : CodeIndex.Search(symbolSearchInput, 10);

            symbolSearchScroll = EditorGUILayout.BeginScrollView(symbolSearchScroll, GUILayout.Height(160));

            if (results.Length == 0)
            {
                EditorGUILayout.LabelField("No results.");
            }
            else
            {
                foreach (ScriptIndexEntry result in results)
                {
                    EditorGUILayout.BeginVertical("box");

                    EditorGUILayout.LabelField("Path", result.path ?? "");
                    EditorGUILayout.LabelField("Class", result.className ?? "");
                    EditorGUILayout.LabelField("Base", result.baseClass ?? "");

                    string methodsText = result.methods == null || result.methods.Length == 0
                        ? "(none)"
                        : string.Join(", ", result.methods);

                    string refsText = result.referencedTypes == null || result.referencedTypes.Length == 0
                        ? "(none)"
                        : string.Join(", ", result.referencedTypes);

                    EditorGUILayout.LabelField("Methods", methodsText);
                    EditorGUILayout.LabelField("References", refsText);

                    if (GUILayout.Button("Use This Script"))
                    {
                        selectedScriptPath = result.path;
                        selectedScriptPreview = TruncatePreview(LoadFileContent(result.path));
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawPrompt()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("Prompt", EditorStyles.boldLabel);

            promptScroll = EditorGUILayout.BeginScrollView(promptScroll, GUILayout.Height(100));
            prompt = EditorGUILayout.TextArea(prompt);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !isSending;

            if (GUILayout.Button("Generate Plan", GUILayout.Height(32)))
            {
                GeneratePlan();
            }

            GUI.enabled = !isSending && hasApprovedPlan;

            if (GUILayout.Button("Implement Approved Plan", GUILayout.Height(32)))
            {
                ImplementApprovedPlan();
            }

            GUI.enabled = !isSending;

            if (GUILayout.Button("Clear", GUILayout.Height(32)))
            {
                output = "";
                lastResponse = null;
                lastPlanResponse = null;
                approvedPlan = null;
                hasApprovedPlan = false;
                selectedPatchIndex = -1;
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawOutput()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("Output", EditorStyles.boldLabel);

            outputScroll = EditorGUILayout.BeginScrollView(outputScroll, GUILayout.Height(220));
            EditorGUILayout.TextArea(output, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawPlanSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Implementation Plan", EditorStyles.boldLabel);

            if (lastPlanResponse == null || lastPlanResponse.plan == null)
            {
                EditorGUILayout.HelpBox("No plan generated yet.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            FeaturePlan plan = lastPlanResponse.plan;

            EditorGUILayout.LabelField("Title", plan.title ?? "");
            EditorGUILayout.LabelField("Summary", plan.summary ?? "");

            GUILayout.Space(6);
            DrawStringList("Goals", plan.goals);
            GUILayout.Space(6);
            DrawStringList("Steps", plan.steps);
            GUILayout.Space(6);
            DrawStringList("Files To Modify", plan.filesToModify);
            GUILayout.Space(6);
            DrawStringList("Files To Create", plan.filesToCreate);
            GUILayout.Space(6);
            DrawStringList("Risks", plan.risks);

            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(hasApprovedPlan ? "Plan Approved" : "Approve Plan", GUILayout.Height(30)))
            {
                approvedPlan = plan;
                hasApprovedPlan = true;
                output = "Plan approved. You can now click 'Implement Approved Plan'.";
            }

            if (GUILayout.Button("Clear Plan", GUILayout.Height(30)))
            {
                approvedPlan = null;
                lastPlanResponse = null;
                hasApprovedPlan = false;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStringList(string label, string[] items)
        {
            GUILayout.Label(label, EditorStyles.miniBoldLabel);

            if (items == null || items.Length == 0)
            {
                EditorGUILayout.LabelField("(none)");
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                EditorGUILayout.LabelField("- " + items[i]);
            }
        }

        private void DrawPatchSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Proposed Patches", EditorStyles.boldLabel);

            if (lastResponse == null || lastResponse.patches == null || lastResponse.patches.Length == 0)
            {
                EditorGUILayout.HelpBox("No patches returned yet.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(260));
            GUILayout.Label("Patch List", EditorStyles.miniBoldLabel);

            for (int i = 0; i < lastResponse.patches.Length; i++)
            {
                FilePatch patch = lastResponse.patches[i];
                string label = $"{i + 1}. {patch.filePath}";

                if (GUILayout.Button(label, GUILayout.Height(28)))
                {
                    selectedPatchIndex = i;
                }
            }

            if (selectedPatchIndex >= 0 && selectedPatchIndex < lastResponse.patches.Length)
            {
                GUILayout.Space(10);

                if (GUILayout.Button("Apply Selected Patch", GUILayout.Height(32)))
                {
                    ApplySelectedPatch();
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();

            if (selectedPatchIndex >= 0 && selectedPatchIndex < lastResponse.patches.Length)
            {
                FilePatch patch = lastResponse.patches[selectedPatchIndex];

                EditorGUILayout.LabelField("File", patch.filePath);
                EditorGUILayout.LabelField(
                    "Reason",
                    string.IsNullOrWhiteSpace(patch.reason) ? "(No reason provided)" : patch.reason
                );

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                GUILayout.Label("Original", EditorStyles.miniBoldLabel);
                patchOldScroll = EditorGUILayout.BeginScrollView(patchOldScroll, GUILayout.Height(280));
                EditorGUILayout.TextArea(patch.originalContent ?? "", GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                GUILayout.Label("New", EditorStyles.miniBoldLabel);
                patchNewScroll = EditorGUILayout.BeginScrollView(patchNewScroll, GUILayout.Height(280));
                EditorGUILayout.TextArea(patch.newContent ?? "", GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Select a patch to preview it.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private async void GeneratePlan()
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                output = "Enter a feature request first.";
                return;
            }

            isSending = true;
            output = "Generating implementation plan...";
            Repaint();

            try
            {
                string selectedPath = !string.IsNullOrWhiteSpace(selectedScriptPath)
                    ? selectedScriptPath
                    : ProjectScanner.GetSelectedScriptPath();

                string selectedContent = !string.IsNullOrWhiteSpace(selectedPath)
                    ? LoadFileContent(selectedPath)
                    : "";

                selectedScriptPath = selectedPath;
                selectedScriptPreview = TruncatePreview(selectedContent);

                AssistantRequest request = new AssistantRequest
                {
                    prompt = prompt,
                    selectedScriptPath = selectedPath,
                    selectedScriptContent = selectedContent,
                    relevantScripts = CodeIndex.GetRelevantScriptsUsingIndex(prompt, selectedPath, 8),
                    consoleMessages = ConsoleReader.GetRelevantMessages(8),
                    manifestJson = ShouldIncludeManifest(prompt) ? ProjectScanner.GetManifestJson() : null,
                    asmdefs = ShouldIncludeAsmdefs(prompt) ? ProjectScanner.GetAllAsmdefs() : new AsmdefFileData[0]
                };

                AssistantResponse response = await ApiClient.SendPlanningRequestAsync(request);

                lastPlanResponse = response;
                approvedPlan = null;
                hasApprovedPlan = false;
                output = FormatPlanResponse(response, request);
            }
            catch (System.Exception ex)
            {
                output = "Plan generation failed.\n\n" + ex.Message;
                Debug.LogException(ex);
            }

            isSending = false;
            Repaint();
        }

        private async void ImplementApprovedPlan()
        {
            if (!hasApprovedPlan || approvedPlan == null)
            {
                output = "No approved plan available.";
                return;
            }

            isSending = true;
            output = "Implementing approved plan...";
            lastResponse = null;
            selectedPatchIndex = -1;
            Repaint();

            try
            {
                string selectedPath = !string.IsNullOrWhiteSpace(selectedScriptPath)
                    ? selectedScriptPath
                    : ProjectScanner.GetSelectedScriptPath();

                string selectedContent = !string.IsNullOrWhiteSpace(selectedPath)
                    ? LoadFileContent(selectedPath)
                    : "";

                selectedScriptPath = selectedPath;
                selectedScriptPreview = TruncatePreview(selectedContent);

                AssistantRequest request = new AssistantRequest
                {
                    prompt = prompt,
                    selectedScriptPath = selectedPath,
                    selectedScriptContent = selectedContent,
                    relevantScripts = CodeIndex.GetRelevantScriptsUsingIndex(prompt, selectedPath, 8),
                    consoleMessages = ConsoleReader.GetRelevantMessages(8),
                    manifestJson = ShouldIncludeManifest(prompt) ? ProjectScanner.GetManifestJson() : null,
                    asmdefs = ShouldIncludeAsmdefs(prompt) ? ProjectScanner.GetAllAsmdefs() : new AsmdefFileData[0]
                };

                AssistantResponse response = await ApiClient.SendImplementationRequestAsync(request, approvedPlan);
                lastResponse = response;
                output = FormatPatchResponse(response, request);
            }
            catch (System.Exception ex)
            {
                output = "Implementation failed.\n\n" + ex.Message;
                Debug.LogException(ex);
            }

            isSending = false;
            Repaint();
        }

        private void ApplySelectedPatch()
        {
            if (lastResponse == null || lastResponse.patches == null)
                return;

            if (selectedPatchIndex < 0 || selectedPatchIndex >= lastResponse.patches.Length)
                return;

            try
            {
                PatchApplier.ApplyPatch(lastResponse.patches[selectedPatchIndex]);
                output += "\n\nApplied patch: " + lastResponse.patches[selectedPatchIndex].filePath;
                RefreshSelectedScript();
            }
            catch (System.Exception ex)
            {
                output += "\n\nFailed to apply patch:\n" + ex.Message;
                Debug.LogException(ex);
            }
        }

        private void RefreshSelectedScript()
        {
            selectedScriptPath = ProjectScanner.GetSelectedScriptPath();
            selectedScriptPreview = TruncatePreview(ProjectScanner.GetSelectedScriptContent());
        }

        private string LoadFileContent(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "";

            string fullPath = System.IO.Path.GetFullPath(relativePath);
            if (!System.IO.File.Exists(fullPath))
                return "";

            return System.IO.File.ReadAllText(fullPath);
        }

        private string TruncatePreview(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            if (content.Length > 2500)
                return content.Substring(0, 2500) + "\n\n... [truncated]";

            return content;
        }

        private bool ShouldIncludeManifest(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
                return false;

            string lower = userPrompt.ToLowerInvariant();
            return lower.Contains("package") ||
                   lower.Contains("manifest") ||
                   lower.Contains("dependency") ||
                   lower.Contains("namespace") ||
                   lower.Contains("compile") ||
                   lower.Contains("assembly");
        }

        private bool ShouldIncludeAsmdefs(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
                return false;

            string lower = userPrompt.ToLowerInvariant();
            return lower.Contains("asmdef") ||
                   lower.Contains("assembly") ||
                   lower.Contains("namespace") ||
                   lower.Contains("compile") ||
                   lower.Contains("reference");
        }

        private string FormatPlanResponse(AssistantResponse response, AssistantRequest request)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Mode: " + (response.mode ?? "plan"));
            sb.AppendLine("Summary:");
            sb.AppendLine(response.summary ?? "(none)");

            sb.AppendLine();
            sb.AppendLine("Context Sent:");
            sb.AppendLine("- Selected script: " + (string.IsNullOrWhiteSpace(request.selectedScriptPath) ? "None" : request.selectedScriptPath));
            sb.AppendLine("- Relevant scripts: " + (request.relevantScripts == null ? 0 : request.relevantScripts.Length));
            sb.AppendLine("- Console messages: " + (request.consoleMessages == null ? 0 : request.consoleMessages.Length));
            sb.AppendLine("- Included manifest: " + (!string.IsNullOrWhiteSpace(request.manifestJson)));
            sb.AppendLine("- Included asmdefs: " + (request.asmdefs == null ? 0 : request.asmdefs.Length));

            if (response.relevantFiles != null && response.relevantFiles.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Relevant Files:");
                foreach (var f in response.relevantFiles)
                    sb.AppendLine("- " + f);
            }

            if (response.warnings != null && response.warnings.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Warnings:");
                foreach (var w in response.warnings)
                    sb.AppendLine("- " + w);
            }

            if (response.plan != null)
            {
                sb.AppendLine();
                sb.AppendLine("Plan Title:");
                sb.AppendLine(response.plan.title ?? "(none)");

                sb.AppendLine();
                sb.AppendLine("Plan Summary:");
                sb.AppendLine(response.plan.summary ?? "(none)");
            }

            if (!string.IsNullOrWhiteSpace(response.nextAction))
            {
                sb.AppendLine();
                sb.AppendLine("Next Action:");
                sb.AppendLine(response.nextAction);
            }

            return sb.ToString();
        }

        private string FormatPatchResponse(AssistantResponse response, AssistantRequest request)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Mode: " + (response.mode ?? "patch"));
            sb.AppendLine("Summary:");
            sb.AppendLine(response.summary ?? "(none)");

            sb.AppendLine();
            sb.AppendLine("Context Sent:");
            sb.AppendLine("- Selected script: " + (string.IsNullOrWhiteSpace(request.selectedScriptPath) ? "None" : request.selectedScriptPath));
            sb.AppendLine("- Relevant scripts: " + (request.relevantScripts == null ? 0 : request.relevantScripts.Length));
            sb.AppendLine("- Console messages: " + (request.consoleMessages == null ? 0 : request.consoleMessages.Length));
            sb.AppendLine("- Included manifest: " + (!string.IsNullOrWhiteSpace(request.manifestJson)));
            sb.AppendLine("- Included asmdefs: " + (request.asmdefs == null ? 0 : request.asmdefs.Length));

            if (response.relevantFiles != null && response.relevantFiles.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Relevant Files:");
                foreach (var f in response.relevantFiles)
                    sb.AppendLine("- " + f);
            }

            if (response.warnings != null && response.warnings.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Warnings:");
                foreach (var w in response.warnings)
                    sb.AppendLine("- " + w);
            }

            if (!string.IsNullOrWhiteSpace(response.nextAction))
            {
                sb.AppendLine();
                sb.AppendLine("Next Action:");
                sb.AppendLine(response.nextAction);
            }

            sb.AppendLine();
            sb.AppendLine("Patch Count:");
            sb.AppendLine(response.patches == null ? "0" : response.patches.Length.ToString());

            return sb.ToString();
        }
    }
}