using System.Reflection;
using HarmonyLib;

namespace TimeForScience
{
    /// <summary>
    /// Cached reflection access to the private ModuleScienceExperiment members we
    /// need but can't reach directly - the module itself stays stock, never
    /// subclassed or renamed (CLAUDE.md constraint #4).
    /// </summary>
    internal static class StockReflection
    {
        private static readonly FieldInfo MseSituationField =
            AccessTools.Field(typeof(ModuleScienceExperiment), "situation");

        private static readonly FieldInfo MseShowDialogAfterField =
            AccessTools.Field(typeof(ModuleScienceExperiment), "showDialogAfter");

        private static readonly MethodInfo MseOnScienceCompleteMethod =
            AccessTools.Method(typeof(ModuleScienceExperiment), "OnScienceComplete");

        private static readonly MethodInfo MseUpdateModuleUIMethod =
            AccessTools.Method(typeof(ModuleScienceExperiment), "updateModuleUI");

        internal static ExperimentSituations GetFrozenSituation(ModuleScienceExperiment module)
        {
            return (ExperimentSituations)MseSituationField.GetValue(module);
        }

        internal static bool GetShowDialogAfter(ModuleScienceExperiment module)
        {
            return (bool)MseShowDialogAfterField.GetValue(module);
        }

        internal static void SetShowDialogAfter(ModuleScienceExperiment module, bool value)
        {
            MseShowDialogAfterField.SetValue(module, value);
        }

        /// <summary>Re-invokes the exact stock method our patch suppressed, so the
        /// real completion (subject/data/dialog/GameEvents) runs untouched.</summary>
        internal static void InvokeOnScienceComplete(ModuleScienceExperiment module)
        {
            MseOnScienceCompleteMethod.Invoke(module, null);
        }

        /// <summary>Lets stock recompute every PAW event/action state from
        /// Deployed/Inoperable/resultsDialog, instead of us guessing it.</summary>
        internal static void UpdateModuleUI(ModuleScienceExperiment module)
        {
            MseUpdateModuleUIMethod.Invoke(module, null);
        }
    }
}
