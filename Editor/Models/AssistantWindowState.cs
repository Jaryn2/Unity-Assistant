using System;

namespace UnityAssistant.Editor.Models
{
    [Serializable]
    public class AssistantWindowState
    {
        public string prompt;
        public string output;

        public string selectedScriptPath;
        public string selectedScriptPreview;

        public string symbolSearchInput;

        public bool hasApprovedPlan;
        public int selectedPatchIndex;

        public AssistantResponse lastResponse;
        public AssistantResponse lastPlanResponse;
        public FeaturePlan approvedPlan;

        public LastAppliedChange lastAppliedChange;
    }
}