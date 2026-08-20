namespace TimeForScience
{
    /// <summary>
    /// One tracked experiment run. Subject and duration are frozen at deploy
    /// time. Tracked by part flightID + module index rather than by vessel,
    /// so a run follows its part through docking/undocking.
    /// </summary>
    internal class TimeForScienceRun
    {
        internal uint PartFlightId;
        internal int ModuleIndex;
        internal string ExperimentId;
        internal string SubjectId;
        internal string BodyName;
        internal string Biome;
        internal int Situation; // live completion reads the module's own frozen field instead
        internal double NeededSeconds;
        internal double AccruedSeconds;
        internal double LastTickUT;
        internal bool ShowDialog;

        // Frozen because completing a run on an unloaded vessel has no live
        // module to read them from (all three are non-persistent KSPFields).
        internal float DataAmount;
        internal float XmitDataScalar;
        internal float ScienceValueRatio;
        internal bool IsDMagic;
        internal int ExperimentsLimit;

        internal bool IsUS2Advanced;
        internal bool Overwrite;

        // Frozen from module.rerunnable - non-repeatable experiments (stock
        // Materials Bay/Mystery Goo and modded equivalents) never bank.
        internal bool Rerunnable = true;

        // 0 means the EC feature was off when this run started.
        internal float ECRate;
        // Ramped power-availability ratio (0..1); eases toward what was
        // actually drawable each tick instead of snapping.
        internal float Throttle = 1f;

        // Frozen via a part-module-name check, not a RealBattery API call.
        internal bool HasRealBattery;
        // EC accrued while unloaded, reported to RealBattery once loaded again.
        internal float PendingECDebt;
        // UT the current PendingECDebt balance started accruing.
        internal double DebtSinceUT;

        /// <summary>DMagic and US2Advanced always build ScienceData with ratio 1.</summary>
        internal float EffectiveScienceValueRatio => (IsDMagic || IsUS2Advanced) ? 1f : ScienceValueRatio;

        internal void Save(ConfigNode node)
        {
            node.AddValue("partFlightId", PartFlightId);
            node.AddValue("moduleIndex", ModuleIndex);
            node.AddValue("experimentId", ExperimentId);
            node.AddValue("subjectId", SubjectId);
            node.AddValue("bodyName", BodyName);
            node.AddValue("biome", Biome ?? "");
            node.AddValue("situation", Situation);
            node.AddValue("neededSeconds", NeededSeconds);
            node.AddValue("accruedSeconds", AccruedSeconds);
            node.AddValue("lastTickUT", LastTickUT);
            node.AddValue("showDialog", ShowDialog);
            node.AddValue("dataAmount", DataAmount);
            node.AddValue("xmitDataScalar", XmitDataScalar);
            node.AddValue("scienceValueRatio", ScienceValueRatio);
            node.AddValue("isDMagic", IsDMagic);
            node.AddValue("experimentsLimit", ExperimentsLimit);
            node.AddValue("isUS2Advanced", IsUS2Advanced);
            node.AddValue("overwrite", Overwrite);
            node.AddValue("rerunnable", Rerunnable);
            node.AddValue("ecRate", ECRate);
            node.AddValue("throttle", Throttle);
            node.AddValue("hasRealBattery", HasRealBattery);
            node.AddValue("pendingEcDebt", PendingECDebt);
            node.AddValue("debtSinceUT", DebtSinceUT);
        }

        internal static TimeForScienceRun Load(ConfigNode node)
        {
            var run = new TimeForScienceRun();
            node.TryGetValue("partFlightId", ref run.PartFlightId);
            node.TryGetValue("moduleIndex", ref run.ModuleIndex);
            node.TryGetValue("experimentId", ref run.ExperimentId);
            node.TryGetValue("subjectId", ref run.SubjectId);
            node.TryGetValue("bodyName", ref run.BodyName);
            node.TryGetValue("biome", ref run.Biome);
            node.TryGetValue("situation", ref run.Situation);
            node.TryGetValue("neededSeconds", ref run.NeededSeconds);
            node.TryGetValue("accruedSeconds", ref run.AccruedSeconds);
            node.TryGetValue("lastTickUT", ref run.LastTickUT);
            node.TryGetValue("showDialog", ref run.ShowDialog);
            node.TryGetValue("dataAmount", ref run.DataAmount);
            node.TryGetValue("xmitDataScalar", ref run.XmitDataScalar);
            node.TryGetValue("scienceValueRatio", ref run.ScienceValueRatio);
            node.TryGetValue("isDMagic", ref run.IsDMagic);
            node.TryGetValue("experimentsLimit", ref run.ExperimentsLimit);
            node.TryGetValue("isUS2Advanced", ref run.IsUS2Advanced);
            node.TryGetValue("overwrite", ref run.Overwrite);
            node.TryGetValue("rerunnable", ref run.Rerunnable);
            node.TryGetValue("ecRate", ref run.ECRate);
            node.TryGetValue("throttle", ref run.Throttle);
            node.TryGetValue("hasRealBattery", ref run.HasRealBattery);
            node.TryGetValue("pendingEcDebt", ref run.PendingECDebt);
            node.TryGetValue("debtSinceUT", ref run.DebtSinceUT);
            return run;
        }
    }
}
