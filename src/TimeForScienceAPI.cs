namespace TimeForScience
{
    // API proposed by celino (Evandro Celino da Silva) for [x] Science! integration.

    /// <summary>Public, reflection-friendly entry point for other mods to show
    /// TimeForScience run durations without copying its formula.</summary>
    public static class TimeForScienceAPI
    {
        /// <summary>Bumped only on breaking changes to the members below.</summary>
        public const int ApiVersion = 1;

        /// <summary>Seconds a stock-module run of this subject would take right
        /// now; 0 when instantaneous (mod inactive, excluded, or ~no science).</summary>
        public static double EstimateSeconds(ScienceExperiment experiment, ScienceSubject subject, ExperimentSituations situation)
        {
            if (experiment == null || subject == null || !TimeForScienceSettings.ModActive)
            {
                return 0;
            }

            if (ScienceExclusions.IsExcludedFromTimer(experiment.id))
            {
                return 0;
            }

            float dataAmount = experiment.baseValue * experiment.dataScale;
            float scienceValue = ResearchAndDevelopment.GetScienceValue(dataAmount, 1f, subject, 1f);
            if (scienceValue < ScienceTiming.ScienceEpsilon)
            {
                return 0;
            }

            return ScienceTiming.ComputeRunSeconds(scienceValue, situation);
        }

        /// <summary>Formats seconds as "1h 12m 54s"/"1m 39s"/"39s", the same
        /// format TimeForScience uses on part action window buttons.</summary>
        public static string FormatDuration(double seconds)
        {
            return ScienceTiming.FormatRemaining(seconds);
        }
    }
}
