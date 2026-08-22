using System.Reflection;
using HarmonyLib;

namespace TimeForScience
{
    /// <summary>
    /// runExperiment(bool silent) is DMagic's analogue of stock's
    /// OnScienceComplete - called right before data creation and the results
    /// dialog, well above Mono's inline limit so a plain prefix works here.
    /// All patches gated by Prepare(): no-op if DMagic isn't installed.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_DMagic_RunExperiment
    {
        private static bool Prepare()
        {
            return DMagicBridge.Available;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(DMagicBridge.ModuleType, "runExperiment");
        }

        private static bool Prefix(ModuleScienceExperiment __instance, bool silent)
        {
            TimeForScienceScenario scenario = TimeForScienceScenario.Instance;
            if (scenario == null || scenario.IsCompleting(__instance))
            {
                return true;
            }

            if (__instance.part == null || __instance.vessel == null)
            {
                return true;
            }

            // Anti-double-start: a click on the countdown button (or an action
            // group re-trigger) must neither restart nor duplicate the run.
            if (scenario.HasRun(__instance))
            {
                return false;
            }

            // Asteroid science uses synthetic subjects: instant, no timer.
            if (DMagicBridge.AsteroidInPlay(__instance))
            {
                return true;
            }

            ScienceExperiment experiment = DMagicBridge.GetScienceExp(__instance);
            if (experiment == null)
            {
                return true;
            }

            if (ScienceExclusions.IsExcludedFromTimer(experiment.id))
            {
                return true;
            }

            ExperimentSituations situation = DMagicBridge.GetSituation(__instance);
            CelestialBody body = __instance.vessel.mainBody;
            string biome = DMagicBridge.GetBiome(__instance, situation);
            string subjectId = ScienceTiming.ComputeSubjectId(experiment, body, situation, biome);

            // Mirror makeScience: DMagic scales the data amount by
            // totalScienceLevel (RP-0 hook, 1.0 in this install) and does not
            // apply scienceValueRatio.
            float dataAmount = experiment.baseValue * experiment.dataScale * DMagicBridge.GetTotalScienceLevel(__instance);
            float scienceValue = ScienceTiming.ComputeScienceValueForData(dataAmount, experiment, body, situation, biome, subjectId, 1f);

            if (scienceValue < ScienceTiming.ScienceEpsilon)
            {
                return true;
            }

            double seconds = ScienceTiming.ComputeRunSeconds(scienceValue, __instance.vessel);
            float ecRate = TimeForScienceSettings.ComputeECRate(experiment);
            scenario.TryRegisterRun(__instance, subjectId, body.name, situation, seconds, showDialog: !silent,
                biome, dataAmount, __instance.xmitDataScalar, scienceValueRatio: 1f, isDMagic: true,
                experimentsLimit: DMagicBridge.GetExperimentsLimit(__instance),
                isUS2Advanced: false, overwrite: false, ecRate: ecRate);
            return false;
        }
    }

    /// <summary>Reset and Retract while a run is active abort it cleanly with
    /// no data; the original always proceeds (retracting the instrument is
    /// the visual "cancel").</summary>
    [HarmonyPatch]
    internal static class Patch_DMagic_ResetExperiment
    {
        private static bool Prepare()
        {
            return DMagicBridge.Available;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(DMagicBridge.ModuleType, "ResetExperiment");
        }

        private static void Prefix(ModuleScienceExperiment __instance)
        {
            TimeForScienceScenario.Instance?.AbortRun(__instance, "#LOC_T4S_AbortedReset");
        }
    }

    [HarmonyPatch]
    internal static class Patch_DMagic_RetractEvent
    {
        private static bool Prepare()
        {
            return DMagicBridge.Available;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(DMagicBridge.ModuleType, "retractEvent");
        }

        private static void Prefix(ModuleScienceExperiment __instance)
        {
            TimeForScienceScenario.Instance?.AbortRun(__instance, "#LOC_T4S_AbortedReset");
        }
    }
}
