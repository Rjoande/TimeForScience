using System.Collections;
using System.Reflection;
using KSP.Localization;

namespace TimeForScience
{
    /// <summary>
    /// Difficulty Settings for the optional Electric Charge consumption
    /// feature (user request 2026-07-19). Off by default: existing behavior
    /// (free observation) is unchanged unless the player opts in. Applies in
    /// any game mode - EC is a resource-management concern, not tied to
    /// career progression the way the rest of the mod is mode-agnostic too.
    /// </summary>
    public class TimeForScienceSettings : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#LOC_T4S_ModName");
        public override GameParameters.GameMode GameMode =>
            GameParameters.GameMode.CAREER | GameParameters.GameMode.SCIENCE | GameParameters.GameMode.SANDBOX;
        public override string Section => Localizer.Format("#LOC_T4S_ModName");
        public override string DisplaySection => Localizer.Format("#LOC_T4S_ModName");
        public override int SectionOrder => 1;
        public override bool HasPresets => false;

        [GameParameters.CustomParameterUI("#LOC_T4S_Settings_EnableEC", toolTip = "#LOC_T4S_Settings_EnableEC_Tooltip")]
        public bool EnableECConsumption = false;

        [GameParameters.CustomFloatParameterUI("#LOC_T4S_Settings_ECRate", minValue = 0f, maxValue = 0.1f, stepCount = 100, displayFormat = "0.000", addTextField = true, toolTip = "#LOC_T4S_Settings_ECRate_Tooltip")]
        public float ECPerScienceRate = 0.01f;

        // Display choice for the idle Deploy-button estimate (user request
        // 2026-07-21): independent of any specific power-management mod -
        // just a personal preference for reading a lump total vs. a rate.
        [GameParameters.CustomParameterUI("#LOC_T4S_Settings_ECAsRate", toolTip = "#LOC_T4S_Settings_ECAsRate_Tooltip")]
        public bool ShowECRateInsteadOfTotal = false;

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            return true;
        }

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            return true;
        }

        public override IList ValidValues(MemberInfo member)
        {
            return null;
        }

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
        }

        /// <summary>
        /// EC/s for this experiment right now, or 0 if the feature is off. A
        /// single frozen value doubles as "disabled" (0) and "rate", so
        /// TimeForScienceRun only needs one field (ECRate) rather than a
        /// separate enabled flag - see notes/ec-consumption.md.
        /// </summary>
        internal static float ComputeECRate(ScienceExperiment experiment)
        {
            if (experiment == null || HighLogic.CurrentGame == null)
            {
                return 0f;
            }

            if (ScienceExclusions.IsExcludedFromEC(experiment.id))
            {
                return 0f;
            }

            TimeForScienceSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<TimeForScienceSettings>();
            if (settings == null || !settings.EnableECConsumption)
            {
                return 0f;
            }

            return experiment.baseValue * settings.ECPerScienceRate;
        }

        /// <summary>Whether the idle Deploy-button estimate should read as
        /// EC/s instead of a lump total - a plain display preference (user
        /// request 2026-07-21), not tied to any specific power mod.</summary>
        internal static bool ShowECAsRate
        {
            get
            {
                TimeForScienceSettings settings = HighLogic.CurrentGame?.Parameters.CustomParams<TimeForScienceSettings>();
                return settings != null && settings.ShowECRateInsteadOfTotal;
            }
        }
    }
}
