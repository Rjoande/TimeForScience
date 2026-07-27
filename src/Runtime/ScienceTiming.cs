using System;

namespace TimeForScience
{
    /// <summary>
    /// Shared math for freezing a science subject at deploy time and turning its
    /// value into a run duration. Used by the stock ModuleScienceExperiment patch
    /// (and, from a later milestone, the DMagic patch) so both compute time the
    /// same way. See notes/stock-science-flow.md and notes/edge-cases.md.
    /// </summary>
    internal static class ScienceTiming
    {
        // Below this, a run is considered "free": the original call proceeds
        // untouched and is instantaneous (decision A2 in edge-cases.md).
        internal const float ScienceEpsilon = 0.01f;

        internal static ExperimentSituations ComputeCurrentSituation(Vessel vessel)
        {
            return ScienceUtil.GetExperimentSituation(vessel);
        }

        internal static string ComputeBiome(Vessel vessel, ScienceExperiment experiment, ExperimentSituations situation)
        {
            if (!experiment.BiomeIsRelevantWhile(situation))
            {
                return "";
            }

            // Mirrors stock OnScienceCompleteDelay, which reads biome from the EVA
            // ladder's host vessel when relevant; a no-op for a normal craft.
            Vessel biomeVessel = vessel.EVALadderVessel;

            if (!string.IsNullOrEmpty(biomeVessel.landedAt))
            {
                return Vessel.GetLandedAtString(biomeVessel.landedAt);
            }

            return ScienceUtil.GetExperimentBiome(biomeVessel.mainBody, biomeVessel.latitude, biomeVessel.longitude);
        }

        internal static string ComputeSubjectId(ScienceExperiment experiment, CelestialBody body, ExperimentSituations situation, string biome)
        {
            return experiment.id + "@" + body.name + situation.ToString() + biome.Replace(" ", "");
        }

        internal static float ComputeScienceValue(ScienceExperiment experiment, CelestialBody body, ExperimentSituations situation, string biome, string subjectId, float scienceValueRatio)
        {
            float dataAmount = experiment.baseValue * experiment.dataScale;
            return ComputeScienceValueForData(dataAmount, experiment, body, situation, biome, subjectId, scienceValueRatio);
        }

        /// <summary>Variant with an explicit data amount, for modules that scale
        /// it themselves (DMagic multiplies by totalScienceLevel, see
        /// notes/compat-dmagic.md).</summary>
        internal static float ComputeScienceValueForData(float dataAmount, ScienceExperiment experiment, CelestialBody body, ExperimentSituations situation, string biome, string subjectId, float scienceValueRatio)
        {
            ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(subjectId)
                ?? new ScienceSubject(experiment, situation, body, biome, "");

            return ResearchAndDevelopment.GetScienceValue(dataAmount, scienceValueRatio, subject, 1f);
        }

        /// <summary>
        /// Placeholder balancing: one second of run time per point of science
        /// value, regardless of situation. Space is meant to eventually run on a
        /// slower (minutes) scale once atmosphere/space/surface are tuned
        /// separately - left open on purpose, see edge-cases.md §J. Testing
        /// default confirmed by the user: seconds everywhere for now.
        /// </summary>
        internal static double ComputeRunSeconds(float scienceValue, Vessel vessel)
        {
            return Math.Max(scienceValue, 0f) * SecondsPerScience(Category(vessel));
        }

        private enum TimeCategory { Space, Atmosphere, Surface }

        private static TimeCategory Category(Vessel vessel)
        {
            switch (vessel.situation)
            {
                case Vessel.Situations.ORBITING:
                case Vessel.Situations.ESCAPING:
                case Vessel.Situations.DOCKED:
                    return TimeCategory.Space;
                case Vessel.Situations.FLYING:
                case Vessel.Situations.SUB_ORBITAL:
                    return TimeCategory.Atmosphere;
                default:
                    return TimeCategory.Surface;
            }
        }

        private static double SecondsPerScience(TimeCategory category)
        {
            // All three equal for now (testing default, edge-cases.md §J).
            switch (category)
            {
                case TimeCategory.Space:
                    return 1.0;
                case TimeCategory.Atmosphere:
                    return 1.0;
                default:
                    return 1.0;
            }
        }

        /// <summary>
        /// Full breakdown duration ("1h 12m 54s", "1m 39s", "39s"), dropping
        /// only the leading units that are zero (user request 2026-07-15,
        /// revised from an earlier single-coarsest-unit format after the
        /// player reported the countdown seeming not to update - most likely
        /// that format's own doing: many small changes stayed within the same
        /// rounded unit and never showed on screen). No stock utility fits:
        /// KSPUtil.PrintTimeCompact uses the in-game calendar (colon-separated,
        /// 6-hour Kerbin days) rather than plain h/m/s letters. Used for both
        /// the idle estimate and the live countdown.
        /// </summary>
        internal static string FormatRemaining(double secondsRemaining)
        {
            int total = (int)Math.Ceiling(Math.Max(secondsRemaining, 0));
            int hours = total / 3600;
            int minutes = total % 3600 / 60;
            int seconds = total % 60;

            if (hours > 0)
            {
                return $"{hours}h {minutes}m {seconds}s";
            }

            if (minutes > 0)
            {
                return $"{minutes}m {seconds}s";
            }

            return $"{seconds}s";
        }
    }
}
