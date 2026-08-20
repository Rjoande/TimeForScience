using System.Collections;
using System.Reflection;
using KSP.Localization;

namespace TimeForScience
{
    /// <summary>
    /// Difficulty Settings for the optional Electric Charge consumption
    /// feature. Off by default. Applies in any game mode - EC is a
    /// resource-management concern, not tied to career progression.
    /// </summary>
    public class TimeForScienceSettings : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#LOC_T4S_ModName");
        public override GameParameters.GameMode GameMode =>
            GameParameters.GameMode.CAREER | GameParameters.GameMode.SCIENCE | GameParameters.GameMode.SANDBOX;
        public override string Section => Localizer.Format("#LOC_T4S_ModName");
        public override string DisplaySection => Localizer.Format("#LOC_T4S_ModName");
        public override int SectionOrder => 1;
        public override bool HasPresets => true;

        [GameParameters.CustomParameterUI("#LOC_T4S_Settings_EnableEC", toolTip = "#LOC_T4S_Settings_EnableEC_Tooltip")]
        public bool EnableECConsumption = false;

        // addTextField = false: the stock text box (DialogGUITextInput) always
        // renders at flexible/full-row width regardless of any attribute here,
        // overflowing the Difficulty Settings column - slider-only avoids it.
        [GameParameters.CustomFloatParameterUI("#LOC_T4S_Settings_ECRate", minValue = 0f, maxValue = 0.1f, stepCount = 100, displayFormat = "0.000", addTextField = false, toolTip = "#LOC_T4S_Settings_ECRate_Tooltip")]
        public float ECPerScienceRate = 0.01f;

        [GameParameters.CustomParameterUI("#LOC_T4S_Settings_ECAsRate", toolTip = "#LOC_T4S_Settings_ECAsRate_Tooltip")]
        public bool ShowECRateInsteadOfTotal = false;

        [GameParameters.CustomParameterUI("#LOC_T4S_Settings_EnableBanking", toolTip = "#LOC_T4S_Settings_EnableBanking_Tooltip")]
        public bool EnableBankedProgress = false;

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            return true;
        }

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            // Rate and rate-display only mean anything with EC consumption
            // on - greyed out rather than hidden, so the dependency is
            // visible (same pattern as SituationalAwareness's SaSettings).
            if (member.Name == nameof(ECPerScienceRate) || member.Name == nameof(ShowECRateInsteadOfTotal))
            {
                return EnableECConsumption;
            }
            return true;
        }

        public override IList ValidValues(MemberInfo member)
        {
            return null;
        }

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            // Custom is left untouched - it's meant to preserve whatever the
            // player already set. EC settings follow the stock difficulty
            // scale (off on Easy, harsher rate on Hard); banked progress is
            // a convenience rather than a difficulty knob, so it's only on
            // for Easy and off everywhere else, matching the shipped default.
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    EnableECConsumption = false;
                    ECPerScienceRate = 0.01f;
                    EnableBankedProgress = true;
                    break;
                case GameParameters.Preset.Normal:
                    EnableECConsumption = false;
                    ECPerScienceRate = 0.01f;
                    EnableBankedProgress = false;
                    break;
                case GameParameters.Preset.Moderate:
                    EnableECConsumption = true;
                    ECPerScienceRate = 0.01f;
                    EnableBankedProgress = false;
                    break;
                case GameParameters.Preset.Hard:
                    EnableECConsumption = true;
                    ECPerScienceRate = 0.02f;
                    EnableBankedProgress = false;
                    break;
            }
        }

        /// <summary>EC/s for this experiment right now, or 0 if the feature
        /// is off - a single frozen value doubles as "disabled" and
        /// "rate".</summary>
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
        /// EC/s instead of a lump total.</summary>
        internal static bool ShowECAsRate
        {
            get
            {
                TimeForScienceSettings settings = HighLogic.CurrentGame?.Parameters.CustomParams<TimeForScienceSettings>();
                return settings != null && settings.ShowECRateInsteadOfTotal;
            }
        }

        /// <summary>Whether time on a different biome (same situation) gets
        /// banked instead of wasted while a run is active elsewhere.</summary>
        internal static bool BankedProgressEnabled
        {
            get
            {
                TimeForScienceSettings settings = HighLogic.CurrentGame?.Parameters.CustomParams<TimeForScienceSettings>();
                return settings != null && settings.EnableBankedProgress;
            }
        }
    }
}
