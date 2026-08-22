using System;
using System.Reflection;
using HarmonyLib;

namespace TimeForScience
{
    /// <summary>
    /// Soft-dependency access to US2's USAdvancedScience: only RunExperiment
    /// and experimentsLimit need resolving, other fields are public/inherited.
    /// USSimpleScience needs no bridge - it uses the plain stock coroutine.
    /// </summary>
    internal static class US2Bridge
    {
        internal static readonly Type ModuleType;

        private static readonly MethodInfo RunExperimentMethod; // private void RunExperiment(bool silent, bool overwrite)
        private static readonly FieldInfo ExperimentsLimitField; // int, declared on USAdvancedScience itself

        static US2Bridge()
        {
            ModuleType = AccessTools.TypeByName("UniversalStorage2.USAdvancedScience");
            if (ModuleType == null)
            {
                return;
            }

            RunExperimentMethod = AccessTools.Method(ModuleType, "RunExperiment", new[] { typeof(bool), typeof(bool) });
            ExperimentsLimitField = AccessTools.Field(ModuleType, "experimentsLimit");
        }

        internal static bool Available => ModuleType != null && RunExperimentMethod != null;

        internal static bool IsUS2Advanced(PartModule module)
        {
            return Available && ModuleType.IsInstanceOfType(module);
        }

        internal static int GetExperimentsLimit(ModuleScienceExperiment module)
        {
            return ExperimentsLimitField != null ? (int)ExperimentsLimitField.GetValue(module) : 1;
        }

        /// <summary>Re-invokes the exact method our patch suppressed, under the
        /// scenario's IsCompleting guard, so the untouched US2 completion
        /// (data, dialog, sample/greeble animations, GameEvents) runs.</summary>
        internal static void InvokeRunExperiment(ModuleScienceExperiment module, bool silent, bool overwrite)
        {
            RunExperimentMethod.Invoke(module, new object[] { silent, overwrite });
        }
    }
}
