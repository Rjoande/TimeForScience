using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace TimeForScience
{
    /// <summary>
    /// Applies the Harmony patches once, at game load. The actual patches
    /// (targeting ModuleScienceExperiment.OnScienceComplete and
    /// DMModuleScienceAnimateGeneric.runExperiment, see notes/stock-science-flow.md
    /// and notes/compat-dmagic.md) are added in a later milestone; this only
    /// wires up the Harmony instance so the dependency and load order are
    /// verified before any gameplay logic is written.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal class TimeForScienceLoader : MonoBehaviour
    {
        internal const string HarmonyId = "com.timeforscience";

        private static bool patched;

        private void Awake()
        {
            if (patched)
            {
                return;
            }
            patched = true;

            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Diagnostic (temporary, see CLAUDE.md registro): lists exactly the
            // methods THIS mod patched, so the coroutine MoveNext targets
            // (resolved at runtime by TargetMethod) are verifiable in the log.
            foreach (MethodBase method in harmony.GetPatchedMethods())
            {
                Debug.Log($"[TimeForScience] patched: {method.DeclaringType?.FullName}.{method.Name}");
            }
        }
    }
}
