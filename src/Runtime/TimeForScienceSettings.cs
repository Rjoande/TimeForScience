using System.Collections;
using System.Reflection;
using KSP.Localization;

namespace TimeForScience
{
    /// <summary>Difficulty Settings for EC consumption and banked progress,
    /// both off by default, plus the Sandbox opt-in (off: mod inert).</summary>
    public class TimeForScienceSettings : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#LOC_T4S_ModName");
        public override GameParameters.GameMode GameMode =>
            GameParameters.GameMode.CAREER | GameParameters.GameMode.SCIENCE | GameParameters.GameMode.SANDBOX;
        public override string Section => Localizer.Format("#LOC_T4S_ModName");
        public override string DisplaySection => Localizer.Format("#LOC_T4S_ModName");
        public override int SectionOrder => 1;
        public override bool HasPresets => true;

        // Sandbox has no R&D: every run would cost a first-time value forever.
        // Shown only in Sandbox games; untouched by presets.
        [GameParameters.CustomParameterUI("#LOC_T4S_Settings_EnableInSandbox", toolTip = "#LOC_T4S_Settings_EnableInSandbox_Tooltip", gameMode = GameParameters.GameMode.SANDBOX)]
        public bool EnableInSandbox = false;

        [GameParameters.CustomParameterUI("#LOC_T4S_Settings_EnableEC", toolTip = "#LOC_T4S_Settings_EnableEC_Tooltip")]
        public bool EnableECConsumption = false;

        // addTextField = false: the stock text box always renders at full-row
        // width whatever this attribute says, overflowing the settings column.
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
            // Rate and rate-display only mean anything with EC consumption on;
            // greyed out rather than hidden, so the dependency stays visible.
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
            // Custom is left untouched. EC follows the stock difficulty scale;
            // banked progress is a convenience, not a difficulty knob, so
            // it's only on for Easy.
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

        /// <summary>EC/s for this experiment right now, or 0 if the feature is
        /// off - one frozen value doubles as "disabled" and "rate".</summary>
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

        /// <summary>False only in a Sandbox game without the opt-in: the mod then
        /// leaves every experiment to stock and ticks nothing.</summary>
        internal static bool ModActive
        {
            get
            {
                Game game = HighLogic.CurrentGame;
                if (game == null || game.Mode != Game.Modes.SANDBOX)
                {
                    return true;
                }

                TimeForScienceSettings settings = game.Parameters.CustomParams<TimeForScienceSettings>();
                return settings != null && settings.EnableInSandbox;
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
