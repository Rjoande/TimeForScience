using System;

namespace TimeForScience
{
    /// <summary>
    /// Shared math for freezing a science subject at deploy time and turning
    /// its value into a run duration, used identically by the stock, DMagic
    /// and US2 deploy paths.
    /// </summary>
    internal static class ScienceTiming
    {
        // Below this, a run is considered "free": instantaneous, untouched.
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

        /// <summary>Variant with an explicit data amount, for modules that
        /// scale it themselves (DMagic multiplies by totalScienceLevel).</summary>
        internal static float ComputeScienceValueForData(float dataAmount, ScienceExperiment experiment, CelestialBody body, ExperimentSituations situation, string biome, string subjectId, float scienceValueRatio)
        {
            ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(subjectId)
                ?? new ScienceSubject(experiment, situation, body, biome, "");

            return ResearchAndDevelopment.GetScienceValue(dataAmount, scienceValueRatio, subject, 1f);
        }

        /// <summary>Placeholder balancing: one second of run time per point of
        /// science value, regardless of situation.</summary>
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
            // All three equal for now.
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
        /// only the leading units that are zero. No stock utility fits:
        /// KSPUtil.PrintTimeCompact uses the in-game calendar (6-hour Kerbin
        /// days) rather than plain h/m/s letters.
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
