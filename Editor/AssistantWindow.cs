using UnityEditor;
using UnityEngine;
using System.Text;
using System.Linq;
using UnityAssistant.Editor.Models;
using UnityAssistant.Editor.Services;

namespace UnityAssistant.Editor
{
    public class AssistantWindow : EditorWindow
    {
        private const string WindowStateKey = "UnityAssistant.WindowState";

        private enum Tab
        {
            Assistant = 0,
            Plan = 1,
            Patches = 2,
            Index = 3,
            Settings = 4,
            HowToUse = 5
        }

        private Tab currentTab = Tab.Assistant;

        private string prompt = "";
        private string output = "Assistant response will appear here.";

        private string apiKeyInput = "";
        private string planningModelInput = "";
        private string implementationModelInput = "";

        private string selectedScriptPath = "";
        private string selectedScriptPreview = "";

        private string symbolSearchInput = "";
        private string editablePlanDocument = "";

        private bool isSending = false;
        private bool hasApprovedPlan = false;

        private Vector2 assistantScroll;
        private Vector2 planScroll;
        private Vector2 patchesScroll;
        private Vector2 indexScroll;
        private Vector2 settingsScroll;
        private Vector2 howToUseScroll;

        private Vector2 promptScroll;
        private Vector2 outputScroll;
        private Vector2 selectedScriptScroll;
        private Vector2 symbolSearchScroll;
        private Vector2 patchOldScroll;
        private Vector2 patchNewScroll;
        private Vector2 planDocumentScroll;

        private AssistantResponse lastResponse;
        private AssistantResponse lastPlanResponse;
        private FeaturePlan approvedPlan;
        private LastAppliedChange lastAppliedChange;

        private int selectedPatchIndex = -1;

        [MenuItem("Window/Unity Assistant")]
        public static void ShowWindow()
        {
            AssistantWindow window = GetWindow<AssistantWindow>("Unity Assistant");
            window.apiKeyInput = OpenAISettings.ApiKey;
            window.planningModelInput = OpenAISettings.PlanningModel;
            window.implementationModelInput = OpenAISettings.ImplementationModel;
            window.minSize = new Vector2(920, 700);
            window.LoadWindowState();
            window.Repaint();
        }

        private void OnEnable()
        {
            apiKeyInput = OpenAISettings.ApiKey;
            planningModelInput = OpenAISettings.PlanningModel;
            implementationModelInput = OpenAISettings.ImplementationModel;
            LoadWindowState();
        }

        private void OnDisable()
        {
            SaveWindowState();
        }

        private void OnSelectionChange()
        {
            if (ProjectScanner.GetSelectedScriptPath() != null)
            {
                RefreshSelectedScript();
                SaveWindowState();
                Repaint();
            }
        }

        private void OnFocus()
        {
            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTabs();
            GUILayout.Space(8);

            switch (currentTab)
            {
                case Tab.Assistant:
                    DrawAssistantTab();
                    break;
                case Tab.Plan:
                    DrawPlanTab();
                    break;
                case Tab.Patches:
                    DrawPatchesTab();
                    break;
                case Tab.Index:
                    DrawIndexTab();
                    break;
                case Tab.Settings:
                    DrawSettingsTab();
                    break;
                case Tab.HowToUse:
                    DrawHowToUseTab();
                    break;
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Unity Assistant", EditorStyles.boldLabel);

            string status = isSending ? "Busy" : "Ready";
            string contextMode = string.IsNullOrWhiteSpace(selectedScriptPath) ? "Project-wide" : "Script-focused";
            string selected = string.IsNullOrWhiteSpace(selectedScriptPath) ? "None" : selectedScriptPath;

            EditorGUILayout.LabelField("Status", status);
            EditorGUILayout.LabelField("Context Mode", contextMode);
            EditorGUILayout.LabelField("Selected Script", selected);

            EditorGUILayout.EndVertical();
        }

        private void DrawTabs()
        {
            string[] tabNames =
            {
                "Assistant",
                "Plan",
                "Patches",
                "Index",
                "Settings",
                "How To Use"
            };

            currentTab = (Tab)GUILayout.Toolbar((int)currentTab, tabNames, GUILayout.Height(28));
        }

        private void DrawAssistantTab()
        {
            assistantScroll = EditorGUILayout.BeginScrollView(assistantScroll);

            DrawSelectedScriptCard();
            GUILayout.Space(8);

            DrawPromptCard();
            GUILayout.Space(8);

            DrawActionButtonsCard();
            GUILayout.Space(8);

            DrawOutputCard();

            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPlanTab()
        {
            planScroll = EditorGUILayout.BeginScrollView(planScroll);

            DrawEditablePlanDocumentCard();
            GUILayout.Space(8);

            DrawEditorSetupCard();

            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPatchesTab()
        {
            patchesScroll = EditorGUILayout.BeginScrollView(patchesScroll);

            DrawPatchesCard();

            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawIndexTab()
        {
            indexScroll = EditorGUILayout.BeginScrollView(indexScroll);

            DrawCodeIndexCard();

            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSettingsTab()
        {
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);

            DrawSettingsCard();

            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHowToUseTab()
        {
            howToUseScroll = EditorGUILayout.BeginScrollView(howToUseScroll);

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("How To Use Unity Assistant", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Planning generates an editable plan document. You can manually edit that document before implementation.",
                MessageType.Info
            );
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            DrawHelpSection(
                "Planning Workflow",
                new[]
                {
                    "1. Enter a rough idea or feature request in the Assistant tab.",
                    "2. Click Generate Plan.",
                    "3. Go to the Plan tab.",
                    "4. Review the structured plan and the editable plan document.",
                    "5. Edit the plan document by hand if needed.",
                    "6. Click Approve Edited Plan.",
                    "7. Click Implement Approved Plan to generate patches from the edited plan document."
                });

            DrawHelpSection(
                "Editable Plan Document",
                new[]
                {
                    "The plan document is plain text so you can rewrite it manually.",
                    "You can add or remove steps, change file targets, and rewrite requirements.",
                    "Implementation uses the edited plan document, not just the original generated plan."
                });

            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHelpSection(string title, string[] lines)
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(title, EditorStyles.boldLabel);

            if (lines != null)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    EditorGUILayout.LabelField("• " + lines[i], EditorStyles.wordWrappedLabel);
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(6);
        }

        private void DrawSelectedScriptCard()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("Selected Script (Optional Context)", EditorStyles.boldLabel);

            EditorGUILayout.TextField(
                "Path",
                string.IsNullOrWhiteSpace(selectedScriptPath)
                    ? "No script selected. The assistant will use project-wide context."
                    : selectedScriptPath
            );

            EditorGUILayout.HelpBox(
                "Tip: select a script if you want to focus the request on a specific area. Leave it empty for project-wide planning.",
                MessageType.None
            );

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Use Current Project Selection", GUILayout.Height(26)))
            {
                string newPath = ProjectScanner.GetSelectedScriptPath();
                if (!string.IsNullOrWhiteSpace(newPath))
                {
                    selectedScriptPath = newPath;
                    selectedScriptPreview = TruncatePreview(ProjectScanner.GetSelectedScriptContent());
                    SaveWindowState();
                }
            }

            if (GUILayout.Button("Clear Optional Script", GUILayout.Height(26)))
            {
                selectedScriptPath = "";
                selectedScriptPreview = "";
                SaveWindowState();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Preview", EditorStyles.miniBoldLabel);

            selectedScriptScroll = EditorGUILayout.BeginScrollView(selectedScriptScroll, GUILayout.Height(160));
            EditorGUILayout.TextArea(
                string.IsNullOrWhiteSpace(selectedScriptPreview)
                    ? "No script selected. The assistant will use project-wide context."
                    : selectedScriptPreview,
                GUILayout.ExpandHeight(true)
            );
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawPromptCard()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("Prompt", EditorStyles.boldLabel);

            promptScroll = EditorGUILayout.BeginScrollView(promptScroll, GUILayout.Height(140));
            string newPrompt = EditorGUILayout.TextArea(prompt, GUILayout.ExpandHeight(true));
            if (newPrompt != prompt)
            {
                prompt = newPrompt;
                SaveWindowState();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtonsCard()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !isSending;
            if (GUILayout.Button("Generate Plan", GUILayout.Height(34)))
            {
                GeneratePlan();
            }

            GUI.enabled = !isSending && hasApprovedPlan && !string.IsNullOrWhiteSpace(editablePlanDocument);
            if (GUILayout.Button("Implement Approved Plan", GUILayout.Height(34)))
            {
                ImplementApprovedPlan();
            }

            GUI.enabled = !isSending && lastAppliedChange != null;
            if (GUILayout.Button("Revert Last Change", GUILayout.Height(34)))
            {
                RevertLastChange();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !isSending;
            if (GUILayout.Button("Go To Plan Tab", GUILayout.Height(28)))
            {
                currentTab = Tab.Plan;
            }

            if (GUILayout.Button("Go To Patches Tab", GUILayout.Height(28)))
            {
                currentTab = Tab.Patches;
            }

            if (GUILayout.Button("How To Use", GUILayout.Height(28)))
            {
                currentTab = Tab.HowToUse;
            }

            if (GUILayout.Button("Clear Session", GUILayout.Height(28)))
            {
                output = "";
                lastResponse = null;
                lastPlanResponse = null;
                approvedPlan = null;
                editablePlanDocument = "";
                hasApprovedPlan = false;
                selectedPatchIndex = -1;
                SaveWindowState();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Planning generates an editable plan document. Implementation uses that edited plan document.",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawOutputCard()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("Output", EditorStyles.boldLabel);

            outputScroll = EditorGUILayout.BeginScrollView(outputScroll, GUILayout.Height(220));
            EditorGUILayout.TextArea(output, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawEditablePlanDocumentCard()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Editable Plan Document", EditorStyles.boldLabel);

            if (lastPlanResponse == null && string.IsNullOrWhiteSpace(editablePlanDocument))
            {
                EditorGUILayout.HelpBox("No plan generated yet.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.HelpBox(
                "Edit this plan by hand before implementation. Implementation will use this text as the approved plan.",
                MessageType.Info
            );

            planDocumentScroll = EditorGUILayout.BeginScrollView(planDocumentScroll, GUILayout.Height(420));
            string newPlanDocument = EditorGUILayout.TextArea(
                string.IsNullOrWhiteSpace(editablePlanDocument) ? "" : editablePlanDocument,
                GUILayout.ExpandHeight(true)
            );
            EditorGUILayout.EndScrollView();

            if (newPlanDocument != editablePlanDocument)
            {
                editablePlanDocument = newPlanDocument;
                SaveWindowState();
            }

            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset From Generated Plan", GUILayout.Height(28)))
            {
                editablePlanDocument = BuildPlanDocumentFromCurrentPlan();
                SaveWindowState();
            }

            GUI.enabled = !string.IsNullOrWhiteSpace(editablePlanDocument);

            if (GUILayout.Button(hasApprovedPlan ? "Plan Approved" : "Approve Edited Plan", GUILayout.Height(28)))
            {
                if (lastPlanResponse != null && !string.IsNullOrWhiteSpace(editablePlanDocument))
                {
                    approvedPlan = lastPlanResponse.plan;
                    hasApprovedPlan = true;
                    output = "Edited plan approved. You can now click 'Implement Approved Plan'.";
                    SaveWindowState();
                }
            }

            GUI.enabled = true;

            if (GUILayout.Button("Clear Plan", GUILayout.Height(28)))
            {
                approvedPlan = null;
                lastPlanResponse = null;
                editablePlanDocument = "";
                hasApprovedPlan = false;
                SaveWindowState();
            }

            if (GUILayout.Button("Go To Assistant Tab", GUILayout.Height(28)))
            {
                currentTab = Tab.Assistant;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private string BuildPlanDocumentFromCurrentPlan()
        {
            if (lastPlanResponse == null)
                return "";

            if (!string.IsNullOrWhiteSpace(lastPlanResponse.planDocument))
                return lastPlanResponse.planDocument;

            if (lastPlanResponse.plan == null)
                return "";

            FeaturePlan plan = lastPlanResponse.plan;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Title:");
            sb.AppendLine(plan.title ?? "");
            sb.AppendLine();

            sb.AppendLine("Summary:");
            sb.AppendLine(plan.summary ?? "");
            sb.AppendLine();

            AppendDocList(sb, "Goals", plan.goals);
            AppendDocList(sb, "Implementation Steps", plan.steps);
            AppendDocList(sb, "Files To Modify", plan.filesToModify);
            AppendDocList(sb, "Files To Create", plan.filesToCreate);
            AppendDocList(sb, "Risks", plan.risks);

            if (plan.editorSetup != null && plan.editorSetup.Length > 0)
            {
                sb.AppendLine("Editor Setup:");
                for (int i = 0; i < plan.editorSetup.Length; i++)
                {
                    var step = plan.editorSetup[i];
                    sb.AppendLine($"- Target: {step.target}");
                    sb.AppendLine($"  Action: {step.action}");
                    sb.AppendLine($"  Details: {step.details}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void AppendDocList(StringBuilder sb, string label, string[] values)
        {
            sb.AppendLine(label + ":");
            if (values == null || values.Length == 0)
            {
                sb.AppendLine("- none");
            }
            else
            {
                for (int i = 0; i < values.Length; i++)
                {
                    sb.AppendLine("- " + values[i]);
                }
            }
            sb.AppendLine();
        }

        private void DrawEditorSetupCard()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Editor Setup Instructions", EditorStyles.boldLabel);

            EditorSetupInstruction[] instructions = null;

            if (lastResponse != null && lastResponse.editorSetup != null && lastResponse.editorSetup.Length > 0)
            {
                instructions = lastResponse.editorSetup;
            }
            else if (lastPlanResponse != null && lastPlanResponse.plan != null &&
                     lastPlanResponse.plan.editorSetup != null &&
                     lastPlanResponse.plan.editorSetup.Length > 0)
            {
                instructions = lastPlanResponse.plan.editorSetup;
            }

            if (instructions == null || instructions.Length == 0)
            {
                EditorGUILayout.HelpBox("No editor setup instructions returned.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            for (int i = 0; i < instructions.Length; i++)
            {
                EditorSetupInstruction step = instructions[i];

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Target", string.IsNullOrWhiteSpace(step.target) ? "(none)" : step.target);
                EditorGUILayout.LabelField("Action", string.IsNullOrWhiteSpace(step.action) ? "(none)" : step.action);

                GUILayout.Label("Details", EditorStyles.miniBoldLabel);
                EditorGUILayout.TextArea(
                    string.IsNullOrWhiteSpace(step.details) ? "(none)" : step.details,
                    GUILayout.MinHeight(45)
                );

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPatchesCard()
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

            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            GUILayout.Label("Patch List", EditorStyles.miniBoldLabel);

            for (int i = 0; i < lastResponse.patches.Length; i++)
            {
                FilePatch patch = lastResponse.patches[i];
                string patchType = string.IsNullOrWhiteSpace(patch.originalContent) ? "[NEW]" : "[EDIT]";
                string label = $"{i + 1}. {patchType} {patch.filePath}";

                if (GUILayout.Button(label, GUILayout.Height(28)))
                {
                    selectedPatchIndex = i;
                    SaveWindowState();
                }
            }

            GUILayout.Space(10);

            if (selectedPatchIndex >= 0 && selectedPatchIndex < lastResponse.patches.Length)
            {
                if (GUILayout.Button("Apply Selected Patch", GUILayout.Height(32)))
                {
                    ApplySelectedPatch();
                }
            }

            if (GUILayout.Button("Apply All Patches", GUILayout.Height(32)))
            {
                ApplyAllPatches();
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
                patchOldScroll = EditorGUILayout.BeginScrollView(patchOldScroll, GUILayout.Height(260));
                EditorGUILayout.TextArea(patch.originalContent ?? "", GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                GUILayout.Label("New", EditorStyles.miniBoldLabel);
                patchNewScroll = EditorGUILayout.BeginScrollView(patchNewScroll, GUILayout.Height(260));
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

        private void DrawCodeIndexCard()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("Code Index", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            string newSearch = EditorGUILayout.TextField("Search", symbolSearchInput);
            if (newSearch != symbolSearchInput)
            {
                symbolSearchInput = newSearch;
                SaveWindowState();
            }

            if (GUILayout.Button("Rebuild Index", GUILayout.Width(120)))
            {
                CodeIndex.RebuildIndex();
            }

            EditorGUILayout.EndHorizontal();

            symbolSearchScroll = EditorGUILayout.BeginScrollView(symbolSearchScroll, GUILayout.Height(420));

            ScriptIndexEntry[] results = string.IsNullOrWhiteSpace(symbolSearchInput)
                ? new ScriptIndexEntry[0]
                : CodeIndex.Search(symbolSearchInput, 10);

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

                    if (GUILayout.Button("Use This Script", GUILayout.Height(26)))
                    {
                        selectedScriptPath = result.path;
                        selectedScriptPreview = TruncatePreview(LoadFileContent(result.path));
                        currentTab = Tab.Assistant;
                        SaveWindowState();
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawSettingsCard()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("OpenAI Settings", EditorStyles.boldLabel);

            apiKeyInput = EditorGUILayout.PasswordField("API Key", apiKeyInput);
            planningModelInput = EditorGUILayout.TextField("Planning Model", planningModelInput);
            implementationModelInput = EditorGUILayout.TextField("Implementation Model", implementationModelInput);

            GUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save Settings", GUILayout.Height(30)))
            {
                OpenAISettings.ApiKey = apiKeyInput;
                OpenAISettings.PlanningModel = planningModelInput;
                OpenAISettings.ImplementationModel = implementationModelInput;
            }

            if (GUILayout.Button("Reload Settings", GUILayout.Height(30)))
            {
                apiKeyInput = OpenAISettings.ApiKey;
                planningModelInput = OpenAISettings.PlanningModel;
                implementationModelInput = OpenAISettings.ImplementationModel;
            }

            if (GUILayout.Button("Reset Saved State", GUILayout.Height(30)))
            {
                ResetSavedState();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);

            EditorGUILayout.HelpBox(
                "Planning uses the planning model and generates an editable plan document. Implementation uses the implementation model.",
                MessageType.Info
            );

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

        private async void GeneratePlan()
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                output = "Enter a feature request first.";
                SaveWindowState();
                return;
            }

            isSending = true;
            output = "Generating implementation plan...";
            SaveWindowState();
            Repaint();

            try
            {
                string selectedPath = selectedScriptPath;
                string selectedContent = !string.IsNullOrWhiteSpace(selectedPath)
                    ? LoadFileContent(selectedPath)
                    : "";

                selectedScriptPreview = TruncatePreview(selectedContent);

                AssistantRequest request = new AssistantRequest
                {
                    prompt = prompt,
                    selectedScriptPath = selectedPath ?? "",
                    selectedScriptContent = selectedContent ?? "",
                    relevantScripts = CodeIndex.GetRelevantScriptsUsingIndex(prompt, selectedPath, 4),
                    consoleMessages = ConsoleReader.GetRelevantMessages(4),
                    manifestJson = ShouldIncludeManifest(prompt) ? ProjectScanner.GetManifestJson() : null,
                    asmdefs = ShouldIncludeAsmdefs(prompt) ? ProjectScanner.GetAllAsmdefs() : new AsmdefFileData[0]
                };

                AssistantResponse response = await ApiClient.SendPlanningRequestAsync(request);

                lastPlanResponse = response;
                approvedPlan = null;
                hasApprovedPlan = false;
                editablePlanDocument = !string.IsNullOrWhiteSpace(response.planDocument)
                    ? response.planDocument
                    : BuildPlanDocumentFromCurrentPlanResponse(response);

                output = FormatPlanResponse(response, request);
                currentTab = Tab.Plan;
                SaveWindowState();
            }
            catch (System.Exception ex)
            {
                output = "Plan generation failed.\n\n" + ex.Message;
                Debug.LogException(ex);
                SaveWindowState();
            }

            isSending = false;
            Repaint();
        }

        private string BuildPlanDocumentFromCurrentPlanResponse(AssistantResponse response)
        {
            if (response == null || response.plan == null)
                return "";

            FeaturePlan plan = response.plan;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Title:");
            sb.AppendLine(plan.title ?? "");
            sb.AppendLine();

            sb.AppendLine("Summary:");
            sb.AppendLine(plan.summary ?? "");
            sb.AppendLine();

            AppendDocList(sb, "Goals", plan.goals);
            AppendDocList(sb, "Implementation Steps", plan.steps);
            AppendDocList(sb, "Files To Modify", plan.filesToModify);
            AppendDocList(sb, "Files To Create", plan.filesToCreate);
            AppendDocList(sb, "Risks", plan.risks);

            if (plan.editorSetup != null && plan.editorSetup.Length > 0)
            {
                sb.AppendLine("Editor Setup:");
                for (int i = 0; i < plan.editorSetup.Length; i++)
                {
                    var step = plan.editorSetup[i];
                    sb.AppendLine($"- Target: {step.target}");
                    sb.AppendLine($"  Action: {step.action}");
                    sb.AppendLine($"  Details: {step.details}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private async void ImplementApprovedPlan()
        {
            if (!hasApprovedPlan || string.IsNullOrWhiteSpace(editablePlanDocument))
            {
                output = "No approved edited plan available.";
                SaveWindowState();
                return;
            }

            isSending = true;
            output = "Implementing approved edited plan...";
            lastResponse = null;
            selectedPatchIndex = -1;
            SaveWindowState();
            Repaint();

            try
            {
                string selectedPath = selectedScriptPath;
                string selectedContent = !string.IsNullOrWhiteSpace(selectedPath)
                    ? LoadFileContent(selectedPath)
                    : "";

                selectedScriptPreview = TruncatePreview(selectedContent);

                AssistantRequest request = new AssistantRequest
                {
                    prompt = prompt,
                    selectedScriptPath = selectedPath ?? "",
                    selectedScriptContent = selectedContent ?? "",
                    relevantScripts = CodeIndex.GetRelevantScriptsUsingIndex(prompt, selectedPath, 6),
                    consoleMessages = ConsoleReader.GetRelevantMessages(4),
                    manifestJson = ShouldIncludeManifest(prompt) ? ProjectScanner.GetManifestJson() : null,
                    asmdefs = ShouldIncludeAsmdefs(prompt) ? ProjectScanner.GetAllAsmdefs() : new AsmdefFileData[0]
                };

                AssistantResponse response = await ApiClient.SendImplementationRequestAsync(request, editablePlanDocument);
                lastResponse = response;
                output = FormatPatchResponse(response, request);
                currentTab = Tab.Patches;
                SaveWindowState();
            }
            catch (System.Exception ex)
            {
                output = "Implementation failed.\n\n" + ex.Message;
                Debug.LogException(ex);
                SaveWindowState();
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
                lastAppliedChange = PatchApplier.ApplyPatch(lastResponse.patches[selectedPatchIndex]);
                output += "\n\nApplied patch: " + lastResponse.patches[selectedPatchIndex].filePath;
                RefreshSelectedScript();
                SaveWindowState();
            }
            catch (System.Exception ex)
            {
                output += "\n\nFailed to apply patch:\n" + ex.Message;
                Debug.LogException(ex);
                SaveWindowState();
            }
        }

        private void ApplyAllPatches()
        {
            if (lastResponse == null || lastResponse.patches == null || lastResponse.patches.Length == 0)
            {
                output += "\n\nNo patches available to apply.";
                SaveWindowState();
                return;
            }

            try
            {
                FilePatch[] orderedPatches = lastResponse.patches
                    .OrderBy(p => string.IsNullOrWhiteSpace(p.originalContent) ? 0 : 1)
                    .ThenBy(p => p.filePath)
                    .ToArray();

                int appliedCount = 0;

                for (int i = 0; i < orderedPatches.Length; i++)
                {
                    FilePatch patch = orderedPatches[i];
                    lastAppliedChange = PatchApplier.ApplyPatch(patch);
                    appliedCount++;
                }

                output += $"\n\nApplied all patches successfully. Count: {appliedCount}";
                RefreshSelectedScript();
                SaveWindowState();
            }
            catch (System.Exception ex)
            {
                output += "\n\nFailed while applying all patches:\n" + ex.Message;
                Debug.LogException(ex);
                SaveWindowState();
            }
        }

        private void RevertLastChange()
        {
            if (lastAppliedChange == null)
            {
                output = "No last change available to revert.";
                SaveWindowState();
                return;
            }

            try
            {
                PatchApplier.RevertLastChange(lastAppliedChange);
                output += "\n\nReverted last change: " + lastAppliedChange.filePath;

                lastAppliedChange = null;
                RefreshSelectedScript();
                SaveWindowState();
            }
            catch (System.Exception ex)
            {
                output += "\n\nFailed to revert last change:\n" + ex.Message;
                Debug.LogException(ex);
                SaveWindowState();
            }
        }

        private void RefreshSelectedScript()
        {
            if (string.IsNullOrWhiteSpace(selectedScriptPath))
            {
                selectedScriptPreview = "";
                return;
            }

            selectedScriptPreview = TruncatePreview(LoadFileContent(selectedScriptPath));
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
            sb.AppendLine("- Context mode: " + (string.IsNullOrWhiteSpace(request.selectedScriptPath) ? "Project-wide" : "Script-focused"));
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
            }

            if (!string.IsNullOrWhiteSpace(response.nextAction))
            {
                sb.AppendLine();
                sb.AppendLine("Next Action:");
                sb.AppendLine(response.nextAction);
            }

            if (!string.IsNullOrWhiteSpace(response.planDocument))
            {
                sb.AppendLine();
                sb.AppendLine("Editable plan document generated.");
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
            sb.AppendLine("- Context mode: " + (string.IsNullOrWhiteSpace(request.selectedScriptPath) ? "Project-wide" : "Script-focused"));
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

        private void SaveWindowState()
        {
            AssistantWindowState state = new AssistantWindowState
            {
                prompt = prompt,
                output = output,
                selectedScriptPath = selectedScriptPath,
                selectedScriptPreview = selectedScriptPreview,
                symbolSearchInput = symbolSearchInput,
                hasApprovedPlan = hasApprovedPlan,
                selectedPatchIndex = selectedPatchIndex,
                lastResponse = lastResponse,
                lastPlanResponse = lastPlanResponse,
                approvedPlan = approvedPlan,
                editablePlanDocument = editablePlanDocument,
                lastAppliedChange = lastAppliedChange
            };

            string json = JsonUtility.ToJson(state);
            EditorPrefs.SetString(WindowStateKey, json);
        }

        private void LoadWindowState()
        {
            if (!EditorPrefs.HasKey(WindowStateKey))
                return;

            string json = EditorPrefs.GetString(WindowStateKey, "");
            if (string.IsNullOrWhiteSpace(json))
                return;

            AssistantWindowState state = JsonUtility.FromJson<AssistantWindowState>(json);
            if (state == null)
                return;

            prompt = state.prompt ?? "";
            output = state.output ?? "Assistant response will appear here.";
            selectedScriptPath = state.selectedScriptPath ?? "";
            selectedScriptPreview = state.selectedScriptPreview ?? "";
            symbolSearchInput = state.symbolSearchInput ?? "";
            hasApprovedPlan = state.hasApprovedPlan;
            selectedPatchIndex = state.selectedPatchIndex;
            lastResponse = state.lastResponse;
            lastPlanResponse = state.lastPlanResponse;
            approvedPlan = state.approvedPlan;
            editablePlanDocument = state.editablePlanDocument ?? "";
            lastAppliedChange = state.lastAppliedChange;
        }

        private void ResetSavedState()
        {
            EditorPrefs.DeleteKey(WindowStateKey);

            prompt = "";
            output = "Assistant response will appear here.";
            selectedScriptPath = "";
            selectedScriptPreview = "";
            symbolSearchInput = "";
            lastResponse = null;
            lastPlanResponse = null;
            approvedPlan = null;
            editablePlanDocument = "";
            hasApprovedPlan = false;
            selectedPatchIndex = -1;
            lastAppliedChange = null;
        }
    }
}