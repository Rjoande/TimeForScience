using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TimeForScience
{
    /// <summary>
    /// Interception for DMModuleScienceAnimateGeneric - the MAIN science module
    /// of this install (ReStockPlus converts Goo/Materials Bay to it; all of
    /// Bluedog_DB uses it; 105 modules total, see notes/compat-dmagic.md).
    ///
    /// runExperiment(bool silent) is the exact analogue of the stock
    /// OnScienceComplete: called after canConduct(), the deploy animation and
    /// the ElectricCharge drain, right before data creation and the results
    /// dialog. It is 147 bytes of IL on the installed DLL - safely above Mono's
    /// 20-byte inline limit - so a plain prefix works here (unlike the stock
    /// path, which needed the coroutine MoveNext technique; never patch
    /// DMagic's 8-byte DeployExperiment). All patches in this file are gated by
    /// Prepare(): if the DMagic DLL is absent, nothing is attempted.
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

            // Asteroid science uses synthetic subjects: instant in v1 (G3).
            if (DMagicBridge.AsteroidInPlay(__instance))
            {
                return true;
            }

            ScienceExperiment experiment = DMagicBridge.GetScienceExp(__instance);
            if (experiment == null)
            {
                return true;
            }

            // Consistency with the stock path/whitelist file, even though no
            // built-in exclusion currently maps to a DMagic experimentID.
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
                Debug.Log($"[TimeForScience] {experiment.id}: value ~0 for {subjectId}, running instantly");
                return true;
            }

            double seconds = ScienceTiming.ComputeRunSeconds(scienceValue, __instance.vessel);
            float ecRate = TimeForScienceSettings.ComputeECRate(experiment);
            Debug.Log($"[TimeForScience] DMagic run registered: {subjectId}, value={scienceValue:F2}, {seconds:F1}s");
            scenario.TryRegisterRun(__instance, subjectId, body.name, situation, seconds, showDialog: !silent,
                biome, dataAmount, __instance.xmitDataScalar, scienceValueRatio: 1f, isDMagic: true,
                experimentsLimit: DMagicBridge.GetExperimentsLimit(__instance),
                isUS2Advanced: false, overwrite: false, ecRate: ecRate);
            return false;
        }
    }

    /// <summary>§G2 for DMagic (edge-cases.md): both Reset and Retract while a
    /// run is active abort it cleanly with no data; the original always
    /// proceeds (retracting the instrument is the visual "cancel").</summary>
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
