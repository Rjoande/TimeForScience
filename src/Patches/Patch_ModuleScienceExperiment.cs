using System.Reflection;
using HarmonyLib;

namespace TimeForScience
{
    /// <summary>
    /// Stock's OnScienceComplete/resetExperiment stubs are too small for
    /// Harmony to detour (Mono inlines them), so this patches their coroutine
    /// MoveNext instead - state 0 is the run's first step, before any stock
    /// completion logic runs.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_OnScienceCompleteDelay_MoveNext
    {
        private static FieldInfo stateField;
        private static FieldInfo thisField;

        private static MethodBase TargetMethod()
        {
            MethodBase moveNext = AccessTools.EnumeratorMoveNext(
                AccessTools.Method(typeof(ModuleScienceExperiment), "OnScienceCompleteDelay"));
            stateField = AccessTools.Field(moveNext.DeclaringType, "<>1__state");
            thisField = AccessTools.Field(moveNext.DeclaringType, "<>4__this");
            return moveNext;
        }

        private static bool Prefix(object __instance, ref bool __result)
        {
            // Only the coroutine's very first step is the "experiment starts
            // now" moment; later resumes (dialog delay loop) pass through.
            if ((int)stateField.GetValue(__instance) != 0)
            {
                return true;
            }

            var module = thisField.GetValue(__instance) as ModuleScienceExperiment;
            if (module == null)
            {
                return true;
            }

            // No GetType() check needed: this coroutine is private/non-virtual,
            // so subclasses only reach it via an explicit base call - DMagic/
            // US2 have their own independent deploy flow and their own patch.
            TimeForScienceScenario scenario = TimeForScienceScenario.Instance;
            if (scenario == null)
            {
                return true;
            }

            if (scenario.IsCompleting(module))
            {
                return true;
            }

            if (module.part == null || module.vessel == null || module.experiment == null)
            {
                return true;
            }

            // Anti-double-start: a click on the countdown button (or an action
            // group / [x]_Science! re-trigger) must neither restart nor
            // duplicate the run - swallow the deploy entirely.
            if (scenario.HasRun(module))
            {
                __result = false;
                return false;
            }

            if (ScienceExclusions.IsExcludedFromTimer(module.experimentID))
            {
                return true;
            }

            ExperimentSituations situation = StockReflection.GetFrozenSituation(module);
            CelestialBody body = module.vessel.mainBody;
            string biome = ScienceTiming.ComputeBiome(module.vessel, module.experiment, situation);
            string subjectId = ScienceTiming.ComputeSubjectId(module.experiment, body, situation, biome);
            float scienceValue = ScienceTiming.ComputeScienceValue(module.experiment, body, situation, biome, subjectId, module.scienceValueRatio);

            if (scienceValue < ScienceTiming.ScienceEpsilon)
            {
                return true;
            }

            double seconds = ScienceTiming.ComputeRunSeconds(scienceValue, module.vessel);
            bool showDialogAfter = StockReflection.GetShowDialogAfter(module);
            float dataAmount = module.experiment.baseValue * module.experiment.dataScale;
            float ecRate = TimeForScienceSettings.ComputeECRate(module.experiment);

            scenario.TryRegisterRun(module, subjectId, body.name, situation, seconds, showDialogAfter,
                biome, dataAmount, module.xmitDataScalar, module.scienceValueRatio, isDMagic: false,
                experimentsLimit: 1, isUS2Advanced: false, overwrite: false, ecRate: ecRate);

            // Report "enumerator finished": the coroutine ends before running
            // any of its body, so no data/dialog/Deployed happens now.
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// Aborts an active run before the stock reset proceeds - harmless on an
    /// undeployed module, so it doubles as our "cancel" animation for free.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_ResetExperiment_MoveNext
    {
        private static FieldInfo stateField;
        private static FieldInfo thisField;

        private static MethodBase TargetMethod()
        {
            MethodBase moveNext = AccessTools.EnumeratorMoveNext(
                AccessTools.Method(typeof(ModuleScienceExperiment), "resetExperiment"));
            stateField = AccessTools.Field(moveNext.DeclaringType, "<>1__state");
            thisField = AccessTools.Field(moveNext.DeclaringType, "<>4__this");
            return moveNext;
        }

        private static void Prefix(object __instance)
        {
            if ((int)stateField.GetValue(__instance) != 0)
            {
                return;
            }

            var module = thisField.GetValue(__instance) as ModuleScienceExperiment;
            if (module == null)
            {
                return;
            }

            // No GetType() restriction: see Patch_OnScienceCompleteDelay_MoveNext.
            TimeForScienceScenario.Instance?.AbortRun(module, "#LOC_T4S_AbortedReset");
        }
    }
}
