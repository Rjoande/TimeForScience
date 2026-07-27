using HarmonyLib;
using KSP.Localization;

namespace TimeForScience
{
    /// <summary>
    /// Injects a custom "Cancel observation" PAW button, hidden by default and
    /// toggled by TimeForScienceScenario while a run is active for that exact
    /// module (user request, edge-cases.md §G4: the normal Deploy button stays
    /// inert during the countdown). ModuleScienceExperiment.OnStart is a large
    /// method (nowhere near Mono's 20-byte inline limit, see §5c), so a plain
    /// postfix is safe here - unlike the deploy/reset coroutines.
    ///
    /// DMagic's own OnStart calls base.OnStart(state) before its own setup, so
    /// this single postfix reaches DMagic instances too without a separate
    /// patch. Deliberately NOT restricted by GetType(): the injected event is
    /// harmless (stays inactive forever) on any ModuleScienceExperiment-family
    /// module we never register a run for.
    /// </summary>
    [HarmonyPatch(typeof(ModuleScienceExperiment), "OnStart")]
    internal static class Patch_InjectCancelEvent
    {
        internal const string CancelEventName = "T4S_CancelObservation";

        private static void Postfix(ModuleScienceExperiment __instance)
        {
            if (__instance.Events[CancelEventName] != null)
            {
                return;
            }

            ModuleScienceExperiment module = __instance;
            var cancelEvent = new BaseEvent(module.Events, CancelEventName, () => OnCancelClicked(module), new KSPEvent
            {
                guiActive = true,
                // Also reachable from EVA proximity (user request 2026-07-15),
                // same range/EVA-only convention stock uses for
                // DeployExperimentExternal/CollectDataExternalEvent.
                guiActiveUnfocused = true,
                externalToEVAOnly = true,
                unfocusedRange = module.interactionRange,
                active = false,
                guiName = Localizer.Format("#LOC_T4S_Cancel"),
            });
            module.Events.Add(cancelEvent);
        }

        private static void OnCancelClicked(ModuleScienceExperiment module)
        {
            // Same abort path as §G2 (Reset/Retract while a run is active):
            // reuses the existing ModuleScienceExperiment overload of AbortRun,
            // which looks the run up by module and no-ops if there isn't one.
            TimeForScienceScenario.Instance?.AbortRun(module, "#LOC_T4S_AbortedReset");
        }
    }
}
