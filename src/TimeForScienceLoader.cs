using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace TimeForScience
{
    /// <summary>Applies all Harmony patches once, at game load.</summary>
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

            // Diagnostic: lists exactly the methods this mod patched, so the
            // runtime-resolved coroutine MoveNext targets are verifiable in the log.
            foreach (MethodBase method in harmony.GetPatchedMethods())
            {
                Debug.Log($"[TimeForScience] patched: {method.DeclaringType?.FullName}.{method.Name}");
            }
        }
    }
}
