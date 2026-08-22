using System.Reflection;
using HarmonyLib;

namespace TimeForScience
{
    /// <summary>
    /// RunExperiment(bool,bool) is US2's analogue of DMagic's runExperiment -
    /// called right before ScienceData creation, well above Mono's inline
    /// limit so a plain prefix works. Gated by Prepare(): no-op without US2.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_US2_RunExperiment
    {
        private static bool Prepare()
        {
            return US2Bridge.Available;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(US2Bridge.ModuleType, "RunExperiment", new[] { typeof(bool), typeof(bool) });
        }

        private static bool Prefix(ModuleScienceExperiment __instance, bool silent, bool overwrite)
        {
            TimeForScienceScenario scenario = TimeForScienceScenario.Instance;
            if (scenario == null || scenario.IsCompleting(__instance))
            {
                return true;
            }

            if (__instance.part == null || __instance.vessel == null || __instance.experiment == null)
            {
                return true;
            }

            // Anti-double-start: a click on the countdown button (or an action
            // group re-trigger) must neither restart nor duplicate the run.
            if (scenario.HasRun(__instance))
            {
                return false;
            }

            if (ScienceExclusions.IsExcludedFromTimer(__instance.experiment.id))
            {
                return true;
            }

            ExperimentSituations situation = ScienceTiming.ComputeCurrentSituation(__instance.vessel);
            CelestialBody body = __instance.vessel.mainBody;
            string biome = ScienceTiming.ComputeBiome(__instance.vessel, __instance.experiment, situation);
            string subjectId = ScienceTiming.ComputeSubjectId(__instance.experiment, body, situation, biome);

            // Mirrors MakeData(): plain baseValue*dataScale, no per-instance
            // scaling, and ratio 1 (like DMagic, MakeData doesn't apply it).
            float dataAmount = __instance.experiment.baseValue * __instance.experiment.dataScale;
            float scienceValue = ScienceTiming.ComputeScienceValueForData(dataAmount, __instance.experiment, body, situation, biome, subjectId, 1f);

            if (scienceValue < ScienceTiming.ScienceEpsilon)
            {
                return true;
            }

            double seconds = ScienceTiming.ComputeRunSeconds(scienceValue, __instance.vessel);
            float ecRate = TimeForScienceSettings.ComputeECRate(__instance.experiment);
            scenario.TryRegisterRun(__instance, subjectId, body.name, situation, seconds, showDialog: !silent,
                biome, dataAmount, __instance.xmitDataScalar, scienceValueRatio: 1f, isDMagic: false,
                experimentsLimit: US2Bridge.GetExperimentsLimit(__instance),
                isUS2Advanced: true, overwrite: overwrite, ecRate: ecRate);
            return false;
        }
    }

    /// <summary>Resetting while a run is active aborts it cleanly with no
    /// data; the original always proceeds.</summary>
    [HarmonyPatch]
    internal static class Patch_US2_ResetExperiment
    {
        private static bool Prepare()
        {
            return US2Bridge.Available;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(US2Bridge.ModuleType, "ResetExperiment");
        }

        private static void Prefix(ModuleScienceExperiment __instance)
        {
            TimeForScienceScenario.Instance?.AbortRun(__instance, "#LOC_T4S_AbortedReset");
        }
    }
}
