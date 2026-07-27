namespace TimeForScience
{
    /// <summary>
    /// One tracked experiment run: the subject and duration are frozen at deploy
    /// time (edge-cases.md, stock-science-flow.md §5); only the accrued time and
    /// the part's live location change afterwards. Tracked by part flightID +
    /// module index rather than by vessel, so the run follows the part through
    /// docking/undocking (decision G).
    ///
    /// Biome/DataAmount/XmitDataScalar/ScienceValueRatio/IsDMagic/ExperimentsLimit
    /// are frozen here (not just derivable from a live module) because STEP 5
    /// needs to complete a run on an unloaded vessel, where no live PartModule
    /// exists to read xmitDataScalar/totalScienceLevel/experimentsLimit from -
    /// all three are non-persistent KSPFields (design-time constants from the
    /// part config), so they must be captured while the module is still alive.
    /// </summary>
    internal class TimeForScienceRun
    {
        internal uint PartFlightId;
        internal int ModuleIndex;
        internal string ExperimentId;
        internal string SubjectId;
        internal string BodyName;
        internal string Biome;
        internal int Situation; // informational only; live completion reads the module's own frozen field
        internal double NeededSeconds;
        internal double AccruedSeconds;
        internal double LastTickUT;
        internal bool ShowDialog;

        internal float DataAmount;
        internal float XmitDataScalar;
        internal float ScienceValueRatio;
        internal bool IsDMagic;
        internal int ExperimentsLimit;

        // Universal Storage 2's USAdvancedScience (notes/compat-us2.md):
        // architecturally a near-twin of DMagic, but a distinct type/method
        // signature, hence its own flags rather than folding into IsDMagic.
        internal bool IsUS2Advanced;
        internal bool Overwrite;

        // Electric Charge consumption (user request 2026-07-19, off by
        // default - see notes/ec-consumption.md). ECRate is EC/s, frozen at
        // registration same as the other config-time constants above; 0 means
        // "feature disabled when this run started", not just "free experiment".
        // Throttle (0..1) is the ramped power-availability ratio; starts at 1
        // (assume full power until proven otherwise) and eases toward whatever
        // was actually drawable each tick rather than snapping, so a brief
        // shortfall doesn't stall progress outright and a big background/warp
        // tick still behaves like a plain average (see TimeForScienceScenario).
        internal float ECRate;
        internal float Throttle = 1f;

        /// <summary>Both DMagic and US2Advanced build their ScienceData with a
        /// hardcoded ratio of 1 (they don't forward scienceValueRatio at all),
        /// unlike plain stock/USSimpleScience which do - see makeScience()/
        /// MakeData() in their respective notes.</summary>
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
            node.AddValue("ecRate", ECRate);
            node.AddValue("throttle", Throttle);
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
            node.TryGetValue("ecRate", ref run.ECRate);
            node.TryGetValue("throttle", ref run.Throttle);
            return run;
        }
    }
}
