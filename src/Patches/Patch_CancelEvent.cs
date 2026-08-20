using HarmonyLib;
using KSP.Localization;

namespace TimeForScience
{
    /// <summary>
    /// Injects two sets of custom PAW elements, hidden by default and toggled
    /// by TimeForScienceScenario: a "Cancel observation" button, and a fixed
    /// pool of inert rows (grouped into a collapsible "Banked biomes"
    /// section) that TimeForScienceScenario relabels/shows or hides each
    /// refresh. ModuleScienceExperiment.OnStart is a large method, nowhere
    /// near Mono's inline limit, so a plain postfix is safe here - unlike the
    /// deploy/reset coroutines.
    ///
    /// DMagic's own OnStart calls base.OnStart(state) before its own setup, so
    /// this single postfix reaches DMagic instances too without a separate
    /// patch. Deliberately NOT restricted by GetType(): the injected elements
    /// are harmless (stay inactive forever) on any ModuleScienceExperiment-
    /// family module we never register a run for.
    /// </summary>
    [HarmonyPatch(typeof(ModuleScienceExperiment), "OnStart")]
    internal static class Patch_InjectCancelEvent
    {
        internal const string CancelEventName = "T4S_CancelObservation";

        // A KSPField-style list would need a real backing FieldInfo we don't
        // have on a stock module; a fixed pool of inert BaseEvents sharing
        // one groupName is the same technique already proven for the cancel
        // button, just repeated. UIPartActionWindow groups purely by the
        // group's name string, and skips inactive events entirely, so an
        // all-inactive pool never even creates the group.
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
