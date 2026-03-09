using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityAssistant.Editor.Models;

namespace UnityAssistant.Editor.Services
{
    public static class ApiClient
    {
        private const string Endpoint = "https://api.openai.com/v1/responses";

        public static async Task<AssistantResponse> SendPlanningRequestAsync(AssistantRequest request)
        {
            string prompt = BuildPlanningPrompt(request);
            return await SendRequestInternalAsync(
                prompt,
                OpenAISettings.PlanningModel,
                700
            );
        }

        public static async Task<AssistantResponse> SendImplementationRequestAsync(
            AssistantRequest request,
            FeaturePlan approvedPlan)
        {
            string prompt = BuildImplementationPrompt(request, approvedPlan);
            return await SendRequestInternalAsync(
                prompt,
                OpenAISettings.ImplementationModel,
                2200
            );
        }

        private static async Task<AssistantResponse> SendRequestInternalAsync(
            string prompt,
            string model,
            int maxOutputTokens)
        {
            if (string.IsNullOrWhiteSpace(OpenAISettings.ApiKey))
            {
                throw new Exception("Missing OpenAI API key. Set it in the Unity Assistant window first.");
            }

            string bodyJson = BuildRequestJson(prompt, model, maxOutputTokens);

            using UnityWebRequest webRequest = new UnityWebRequest(Endpoint, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);

            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", "Bearer " + OpenAISettings.ApiKey);

            var operation = webRequest.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    "OpenAI request failed: " + webRequest.error +
                    "\n\nResponse:\n" + webRequest.downloadHandler.text
                );
            }

            string rawResponse = webRequest.downloadHandler.text;

            ResponsesApiResponse apiResponse = JsonUtility.FromJson<ResponsesApiResponse>(rawResponse);
            string outputText = ExtractOutputText(apiResponse);

            if (string.IsNullOrWhiteSpace(outputText))
            {
                throw new Exception("Model returned no usable text content.\n\nRaw response:\n" + rawResponse);
            }

            AssistantResponse parsed = JsonUtility.FromJson<AssistantResponse>(outputText);

            if (parsed == null)
            {
                throw new Exception("Failed to parse model JSON.\n\nModel output:\n" + outputText);
            }

            return parsed;
        }

        private static string BuildRequestJson(string prompt, string model, int maxOutputTokens)
        {
            ResponsesApiRequest payload = new ResponsesApiRequest
            {
                model = model,
                input = prompt,
                text = new ResponsesTextConfig
                {
                    format = new ResponsesTextFormat
                    {
                        type = "json_object"
                    }
                },
                store = false,
                max_output_tokens = maxOutputTokens
            };

            return JsonUtility.ToJson(payload);
        }

        private static string BuildPlanningPrompt(AssistantRequest request)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("You are a Unity feature-planning assistant.");
            sb.AppendLine("Do NOT write code yet.");
            sb.AppendLine("Return JSON only.");
            sb.AppendLine("Return valid JSON with this exact shape:");
            sb.AppendLine("{");
            sb.AppendLine("  \"mode\": \"plan\",");
            sb.AppendLine("  \"summary\": \"string\",");
            sb.AppendLine("  \"warnings\": [\"string\"],");
            sb.AppendLine("  \"relevantFiles\": [\"string\"],");
            sb.AppendLine("  \"nextAction\": \"string\",");
            sb.AppendLine("  \"plan\": {");
            sb.AppendLine("    \"title\": \"string\",");
            sb.AppendLine("    \"summary\": \"string\",");
            sb.AppendLine("    \"goals\": [\"string\"],");
            sb.AppendLine("    \"steps\": [\"string\"],");
            sb.AppendLine("    \"filesToModify\": [\"string\"],");
            sb.AppendLine("    \"filesToCreate\": [\"string\"],");
            sb.AppendLine("    \"risks\": [\"string\"],");
            sb.AppendLine("    \"editorSetup\": [");
            sb.AppendLine("      {");
            sb.AppendLine("        \"target\": \"string\",");
            sb.AppendLine("        \"action\": \"string\",");
            sb.AppendLine("        \"details\": \"string\"");
            sb.AppendLine("      }");
            sb.AppendLine("    ]");
            sb.AppendLine("  },");
            sb.AppendLine("  \"patches\": [],");
            sb.AppendLine("  \"editorSetup\": []");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Plan the feature before implementation.");
            sb.AppendLine("- Prefer modifying existing scripts over creating unnecessary new scripts.");
            sb.AppendLine("- Keep the plan scoped and practical.");
            sb.AppendLine("- Mention likely files to modify or create.");
            sb.AppendLine("- Include editorSetup instructions for anything the user must do manually in Unity.");
            sb.AppendLine("- Do not include code patches in planning mode.");
            sb.AppendLine();

            AppendCommonContext(sb, request, includeFullRelevantScripts: false);

            return sb.ToString();
        }

        private static string BuildImplementationPrompt(AssistantRequest request, FeaturePlan approvedPlan)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("You are a Unity implementation assistant.");
            sb.AppendLine("Implement the APPROVED plan.");
            sb.AppendLine("Return JSON only.");
            sb.AppendLine("Return valid JSON with this exact shape:");
            sb.AppendLine("{");
            sb.AppendLine("  \"mode\": \"patch\",");
            sb.AppendLine("  \"summary\": \"string\",");
            sb.AppendLine("  \"warnings\": [\"string\"],");
            sb.AppendLine("  \"relevantFiles\": [\"string\"],");
            sb.AppendLine("  \"nextAction\": \"string\",");
            sb.AppendLine("  \"plan\": null,");
            sb.AppendLine("  \"patches\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"filePath\": \"string\",");
            sb.AppendLine("      \"originalContent\": \"string\",");
            sb.AppendLine("      \"newContent\": \"string\",");
            sb.AppendLine("      \"reason\": \"string\"");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"editorSetup\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"target\": \"string\",");
            sb.AppendLine("      \"action\": \"string\",");
            sb.AppendLine("      \"details\": \"string\"");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Follow the approved plan closely.");
            sb.AppendLine("- Only include patches if code changes are actually needed.");
            sb.AppendLine("- Only patch files inside Assets.");
            sb.AppendLine("- Preserve formatting as much as possible.");
            sb.AppendLine("- If a new file is needed, include a patch for that file path with originalContent as an empty string.");
            sb.AppendLine("- For each patch, include full originalContent and full newContent.");
            sb.AppendLine("- If you must deviate from the plan, explain why in warnings.");
            sb.AppendLine("- Include editorSetup instructions for any manual Unity editor actions still required after patches are applied.");
            sb.AppendLine();

            sb.AppendLine("Approved plan:");
            if (approvedPlan == null)
            {
                sb.AppendLine("None");
            }
            else
            {
                sb.AppendLine("Title: " + (approvedPlan.title ?? ""));
                sb.AppendLine("Summary: " + (approvedPlan.summary ?? ""));
                AppendStringList(sb, "Goals", approvedPlan.goals);
                AppendStringList(sb, "Steps", approvedPlan.steps);
                AppendStringList(sb, "Files To Modify", approvedPlan.filesToModify);
                AppendStringList(sb, "Files To Create", approvedPlan.filesToCreate);
                AppendStringList(sb, "Risks", approvedPlan.risks);
            }

            sb.AppendLine();
            AppendCommonContext(sb, request, includeFullRelevantScripts: true);

            return sb.ToString();
        }

        private static void AppendCommonContext(StringBuilder sb, AssistantRequest request, bool includeFullRelevantScripts)
        {
            sb.AppendLine("User prompt:");
            sb.AppendLine(request.prompt ?? "");
            sb.AppendLine();

            sb.AppendLine("Selected script path:");
            sb.AppendLine(string.IsNullOrWhiteSpace(request.selectedScriptPath) ? "None" : request.selectedScriptPath);
            sb.AppendLine();

            sb.AppendLine("Selected script content:");
            sb.AppendLine(string.IsNullOrWhiteSpace(request.selectedScriptContent) ? "None" : request.selectedScriptContent);
            sb.AppendLine();

            sb.AppendLine("Recent console messages:");
            if (request.consoleMessages == null || request.consoleMessages.Length == 0)
            {
                sb.AppendLine("None");
            }
            else
            {
                foreach (var msg in request.consoleMessages)
                {
                    sb.AppendLine($"[{msg.type}] {msg.message}");
                    if (!string.IsNullOrWhiteSpace(msg.stackTrace))
                    {
                        sb.AppendLine(msg.stackTrace);
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("Asmdef files:");
            if (request.asmdefs == null || request.asmdefs.Length == 0)
            {
                sb.AppendLine("None");
            }
            else
            {
                foreach (var asm in request.asmdefs)
                {
                    sb.AppendLine($"PATH: {asm.path}");
                    sb.AppendLine(asm.content);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("manifest.json:");
            sb.AppendLine(string.IsNullOrWhiteSpace(request.manifestJson) ? "None" : request.manifestJson);
            sb.AppendLine();

            if (includeFullRelevantScripts)
            {
                sb.AppendLine("Relevant project scripts:");
                if (request.relevantScripts == null || request.relevantScripts.Length == 0)
                {
                    sb.AppendLine("None");
                }
                else
                {
                    foreach (var script in request.relevantScripts)
                    {
                        sb.AppendLine($"PATH: {script.path}");
                        sb.AppendLine(script.content);
                        sb.AppendLine();
                    }
                }
            }
            else
            {
                sb.AppendLine("Relevant project script summaries:");
                if (request.relevantScripts == null || request.relevantScripts.Length == 0)
                {
                    sb.AppendLine("None");
                }
                else
                {
                    foreach (var script in request.relevantScripts)
                    {
                        sb.AppendLine(SummarizeScriptForPrompt(script));
                    }
                }
            }
        }

        private static string SummarizeScriptForPrompt(ScriptFileData script)
        {
            if (script == null)
                return "";

            string content = script.content ?? "";
            string preview = content.Length > 800 ? content.Substring(0, 800) + "\n... [truncated]" : content;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"PATH: {script.path}");
            sb.AppendLine(preview);
            sb.AppendLine();

            return sb.ToString();
        }

        private static void AppendStringList(StringBuilder sb, string label, string[] values)
        {
            sb.AppendLine(label + ":");
            if (values == null || values.Length == 0)
            {
                sb.AppendLine("- none");
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                sb.AppendLine("- " + values[i]);
            }
        }

        private static string ExtractOutputText(ResponsesApiResponse response)
        {
            if (response == null || response.output == null)
                return null;

            for (int i = 0; i < response.output.Length; i++)
            {
                ResponseOutputMessage message = response.output[i];
                if (message == null || message.content == null)
                    continue;

                for (int j = 0; j < message.content.Length; j++)
                {
                    ResponseContentItem item = message.content[j];
                    if (item != null && item.type == "output_text" && !string.IsNullOrWhiteSpace(item.text))
                    {
                        return item.text;
                    }
                }
            }

            return null;
        }

        [Serializable]
        private class ResponsesApiRequest
        {
            public string model;
            public string input;
            public ResponsesTextConfig text;
            public bool store;
            public int max_output_tokens;
        }

        [Serializable]
        private class ResponsesTextConfig
        {
            public ResponsesTextFormat format;
        }

        [Serializable]
        private class ResponsesTextFormat
        {
            public string type;
        }

        [Serializable]
        private class ResponsesApiResponse
        {
            public string id;
            public string status;
            public ResponseOutputMessage[] output;
        }

        [Serializable]
        private class ResponseOutputMessage
        {
            public string id;
            public string type;
            public string role;
            public string status;
            public ResponseContentItem[] content;
        }

        [Serializable]
        private class ResponseContentItem
        {
            public string type;
            public string text;
        }
    }
}