using System;
using System.Reflection;
using HarmonyLib;

namespace TimeForScience
{
    /// <summary>
    /// Soft-dependency access to DMModuleScienceAnimateGeneric, resolved by
    /// name (no compile-time reference to the DMagic DLL); tries both the
    /// suffixed and un-suffixed namespace across DMagic versions.
    /// </summary>
    internal static class DMagicBridge
    {
        internal static readonly Type ModuleType;

        private static readonly MethodInfo GetSituationMethod;   // ExperimentSituations getSituation()
        private static readonly MethodInfo GetBiomeMethod;       // string getBiome(ExperimentSituations)
        private static readonly MethodInfo RunExperimentMethod;  // void runExperiment(bool silent)
        private static readonly FieldInfo ScienceExpField;       // ScienceExperiment scienceExp
        private static readonly FieldInfo TotalScienceLevelField;// float (RP-0 scaling, 1.0 default)
        private static readonly FieldInfo ExperimentsLimitField; // int, non-persistent (part-config constant)
        private static readonly FieldInfo AsteroidReportsField;  // bool
        private static readonly PropertyInfo AsteroidGrappledProp; // static, on DMAsteroidScienceGen
        private static readonly PropertyInfo AsteroidNearProp;

        static DMagicBridge()
        {
            ModuleType = AccessTools.TypeByName("DMModuleScienceAnimateGeneric_NM.DMModuleScienceAnimateGeneric")
                ?? AccessTools.TypeByName("DMModuleScienceAnimateGeneric.DMModuleScienceAnimateGeneric");
            if (ModuleType == null)
            {
                return;
            }

            GetSituationMethod = AccessTools.Method(ModuleType, "getSituation");
            GetBiomeMethod = AccessTools.Method(ModuleType, "getBiome");
            RunExperimentMethod = AccessTools.Method(ModuleType, "runExperiment");
            ScienceExpField = AccessTools.Field(ModuleType, "scienceExp");
            TotalScienceLevelField = AccessTools.Field(ModuleType, "totalScienceLevel");
            ExperimentsLimitField = AccessTools.Field(ModuleType, "experimentsLimit");
            AsteroidReportsField = AccessTools.Field(ModuleType, "asteroidReports");

            Type asteroidType = AccessTools.TypeByName(ModuleType.Namespace + ".DMAsteroidScienceGen");
            if (asteroidType != null)
            {
                AsteroidGrappledProp = AccessTools.Property(asteroidType, "AsteroidGrappled");
                AsteroidNearProp = AccessTools.Property(asteroidType, "AsteroidNear");
            }
        }

        internal static bool Available =>
            ModuleType != null && GetSituationMethod != null && GetBiomeMethod != null &&
            RunExperimentMethod != null && ScienceExpField != null;

        internal static bool IsDMagic(PartModule module)
        {
            return Available && ModuleType.IsInstanceOfType(module);
        }

        internal static ScienceExperiment GetScienceExp(ModuleScienceExperiment module)
        {
            return (ScienceExperiment)ScienceExpField.GetValue(module);
        }

        internal static ExperimentSituations GetSituation(ModuleScienceExperiment module)
        {
            return (ExperimentSituations)GetSituationMethod.Invoke(module, null);
        }

        internal static string GetBiome(ModuleScienceExperiment module, ExperimentSituations situation)
        {
            return (string)GetBiomeMethod.Invoke(module, new object[] { situation }) ?? "";
        }

        internal static float GetTotalScienceLevel(ModuleScienceExperiment module)
        {
            return TotalScienceLevelField != null ? (float)TotalScienceLevelField.GetValue(module) : 1f;
        }

        /// <summary>Non-persistent (part-config constant): frozen into the run at
        /// registration so background completion doesn't need a live module to
        /// know whether this is a single-slot or multi-slot experiment.</summary>
        internal static int GetExperimentsLimit(ModuleScienceExperiment module)
        {
            return ExperimentsLimitField != null ? (int)ExperimentsLimitField.GetValue(module) : 1;
        }

        /// <summary>Asteroid science uses synthetic subjects
        /// (DMAsteroidScienceGen): bypassed entirely.</summary>
        internal static bool AsteroidInPlay(ModuleScienceExperiment module)
        {
            if (AsteroidReportsField == null || !(bool)AsteroidReportsField.GetValue(module))
            {
                return false;
            }

            bool grappled = AsteroidGrappledProp != null && (bool)AsteroidGrappledProp.GetValue(null);
            bool near = AsteroidNearProp != null && (bool)AsteroidNearProp.GetValue(null);
            return grappled || near;
        }

        /// <summary>Re-invokes the exact DMagic method our patch suppressed, under
        /// the scenario's IsCompleting guard, so the untouched DMagic completion
        /// (data, dialog, sample animations, GameEvents) runs.</summary>
        internal static void InvokeRunExperiment(ModuleScienceExperiment module, bool silent)
        {
            RunExperimentMethod.Invoke(module, new object[] { silent });
        }
    }
}
