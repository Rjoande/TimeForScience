using System.Collections.Generic;
using UnityEngine;

namespace TimeForScience
{
    /// <summary>
    /// Configurable exclusions from timing and/or EC consumption, by
    /// experimentID (user request 2026-07-21). No hardcoded IDs in code on
    /// purpose (simplified 2026-07-21): the stock defaults (crewReport
    /// excluded from EC only - decision H, edge-cases.md - and evaReport/
    /// surfaceSample excluded from both timer and EC, since they're quick
    /// personal observations rather than instrument runs) ship as active
    /// entries in GameData/TimeForScience/Config/Exclusions.cfg, the exact
    /// same mechanism a custom/modded experimentID uses. One list, one file,
    /// no duplication between C# and cfg.
    ///
    /// Entries come from any TIMEFORSCIENCE_EXCLUSION node in GameData,
    /// loaded lazily on first use rather than from a startup KSPAddon, so
    /// there's no scene-timing dependency on GameDatabase being fully
    /// populated.
    /// </summary>
    internal static class ScienceExclusions
    {
        private static readonly HashSet<string> timerExclusions = new HashSet<string>();
        private static readonly HashSet<string> ecExclusions = new HashSet<string>();
        private static bool loaded;

        internal static bool IsExcludedFromTimer(string experimentId)
        {
            EnsureLoaded();
            return timerExclusions.Contains(experimentId);
        }

        internal static bool IsExcludedFromEC(string experimentId)
        {
            EnsureLoaded();
            return ecExclusions.Contains(experimentId);
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("TIMEFORSCIENCE_EXCLUSION"))
            {
                string id = node.GetValue("experimentID");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                bool excludeTimer = false;
                bool excludeEc = false;
                node.TryGetValue("excludeFromTimer", ref excludeTimer);
                node.TryGetValue("excludeFromEC", ref excludeEc);

                if (excludeTimer)
                {
                    timerExclusions.Add(id);
                }
                if (excludeEc)
                {
                    ecExclusions.Add(id);
                }
            }

            Debug.Log($"[TimeForScience] Exclusions loaded: {timerExclusions.Count} from timer, {ecExclusions.Count} from EC");
        }
    }
}
