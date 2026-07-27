using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TimeForScience
{
    /// <summary>
    /// Interception point for stock experiments. The obvious targets
    /// (OnScienceComplete, resetExperiment) are 18/14 bytes of IL - under
    /// Mono's 20-byte JIT inline limit - so a detour on them is never reached
    /// (verified in-game 2026-07-11; see notes/stock-science-flow.md §5c).
    /// Instead we patch the coroutine state machines' MoveNext: Unity invokes
    /// them through IEnumerator interface dispatch, which can never be inlined.
    ///
    /// The first MoveNext (state 0) runs synchronously inside StartCoroutine,
    /// at the exact moment the small stub would have run: past every stock
    /// pre-check, the overwrite-memory dialog and the fx animations, and
    /// before Deployed is set. A run worth ~0 science is left alone (instant,
    /// decision A2); otherwise the completion is deferred to
    /// TimeForScienceScenario, which re-invokes OnScienceComplete under an
    /// IsCompleting guard so the untouched stock completion (popup, GameEvents,
    /// persistence) runs later.
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

            // No GetType() restriction here on purpose (removed 2026-07-18,
            // see notes/compat-us2.md): OnScienceCompleteDelay/resetExperiment
            // are private and non-virtual, so a subclass can only ever reach
            // this coroutine by explicitly calling into the base
            // implementation (base.DeployExperiment()/base.ResetExperiment()),
            // which makes our interception exactly as correct for it as for a
            // plain stock module. Subclasses with their own independent deploy
            // flow (DMagic, Universal Storage 2's USAdvancedScience) never call
            // into this coroutine at all, guard or no guard - they get their
            // own dedicated patch instead. USSimpleScience (US2's
            // ThermoBaroWedge/AccelGravWedge/AdvancedMicroSatWedge) is the
            // discovered case that DOES chain to the real base method and was
            // wrongly excluded by the old exact-type check.
            TimeForScienceScenario scenario = TimeForScienceScenario.Instance;
            if (scenario == null)
            {
                Debug.LogWarning("[TimeForScience] TimeForScienceScenario.Instance is NULL, experiment runs untimed");
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

            // EVA report and surface sample run on the kerbalEVA part and stay
            // instant (decision H, edge-cases.md). Crew report is a different
            // experimentID on the command part and IS timed like everything else
            // (it's excluded from EC only, see ScienceExclusions). Also honors
            // any custom experimentID added via Config/Exclusions.cfg.
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
                Debug.Log($"[TimeForScience] {module.experimentID}: value ~0 for {subjectId}, running instantly");
                return true;
            }

            double seconds = ScienceTiming.ComputeRunSeconds(scienceValue, module.vessel);
            bool showDialogAfter = StockReflection.GetShowDialogAfter(module);
            float dataAmount = module.experiment.baseValue * module.experiment.dataScale;
            float ecRate = TimeForScienceSettings.ComputeECRate(module.experiment);

            Debug.Log($"[TimeForScience] stock run registered: {subjectId}, value={scienceValue:F2}, {seconds:F1}s");
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
    /// User decision (edge-cases.md §G2): resetting an experiment while a run
    /// is active aborts it with no data. Same MoveNext technique as above (the
    /// resetExperiment stub is 14 bytes of IL, inlined). Side-effect only: the
    /// stock reset always proceeds - on an undeployed module it is harmless
    /// and doubles as our "cancel" animation for free.
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
