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

            new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
