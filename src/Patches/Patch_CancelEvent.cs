using HarmonyLib;
using KSP.Localization;

namespace TimeForScience
{
    /// <summary>
    /// Injects the "Cancel observation" button and the inert "Banked biomes"
    /// row pool, both hidden by default and toggled by TimeForScienceScenario.
    /// OnStart is well above Mono's inline limit, so a plain postfix works
    /// (and reaches DMagic too, since its OnStart calls base.OnStart first).
    /// </summary>
    [HarmonyPatch(typeof(ModuleScienceExperiment), "OnStart")]
    internal static class Patch_InjectCancelEvent
    {
        internal const string CancelEventName = "T4S_CancelObservation";

        // Fixed pool of inert BaseEvents sharing one groupName, same technique
        // as the cancel button: UIPartActionWindow groups purely by that name
        // string and skips inactive events, so an empty pool shows nothing.
        internal const int BankRowCount = 6;
        internal const string BankGroupName = "T4S_Banking";

        internal static string BankRowEventName(int index) => "T4S_BankRow" + index;

        private static void Postfix(ModuleScienceExperiment __instance)
        {
            ModuleScienceExperiment module = __instance;

            if (module.Events[CancelEventName] == null)
            {
                var cancelEvent = new BaseEvent(module.Events, CancelEventName, () => OnCancelClicked(module), new KSPEvent
                {
                    guiActive = true,
                    // Also reachable from EVA proximity, same range/EVA-only
                    // convention stock uses for DeployExperimentExternal.
                    guiActiveUnfocused = true,
                    externalToEVAOnly = true,
                    unfocusedRange = module.interactionRange,
                    active = false,
                    guiName = Localizer.Format("#LOC_T4S_Cancel"),
                });
                module.Events.Add(cancelEvent);
            }

            for (int i = 0; i < BankRowCount; i++)
            {
                string rowName = BankRowEventName(i);
                if (module.Events[rowName] != null)
                {
                    continue;
                }

                var rowEvent = new BaseEvent(module.Events, rowName, () => { }, new KSPEvent
                {
                    guiActive = true,
                    active = false,
                    guiName = "",
                    groupName = BankGroupName,
                    groupDisplayName = "#LOC_T4S_BankingGroup",
                    groupStartCollapsed = false,
                });
                module.Events.Add(rowEvent);
            }
        }

        private static void OnCancelClicked(ModuleScienceExperiment module)
        {
            TimeForScienceScenario.Instance?.AbortRun(module, "#LOC_T4S_AbortedReset");
        }
    }
}
