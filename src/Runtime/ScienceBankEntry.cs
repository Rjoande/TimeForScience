namespace TimeForScience
{
    /// <summary>Banked observation time for a subject not currently being
    /// run, keyed by the exact module and subject it applies to. Carries its
    /// own frozen subject components (mirroring TimeForScienceRun) so its
    /// remaining time can be recomputed fresh for display without a live
    /// module - a bank entry can outlive the run that created it.</summary>
    internal class ScienceBankEntry
    {
        internal uint PartFlightId;
        internal int ModuleIndex;
        internal string SubjectId;
        internal string ExperimentId;
        internal string BodyName;
        internal int Situation;
        internal string Biome;
        internal float ScienceValueRatio;
        internal double BankedSeconds;

        internal void Save(ConfigNode node)
        {
            node.AddValue("partFlightId", PartFlightId);
            node.AddValue("moduleIndex", ModuleIndex);
            node.AddValue("subjectId", SubjectId);
            node.AddValue("experimentId", ExperimentId);
            node.AddValue("bodyName", BodyName);
            node.AddValue("situation", Situation);
            node.AddValue("biome", Biome ?? "");
            node.AddValue("scienceValueRatio", ScienceValueRatio);
            node.AddValue("bankedSeconds", BankedSeconds);
        }

        internal static ScienceBankEntry Load(ConfigNode node)
        {
            var entry = new ScienceBankEntry();
            node.TryGetValue("partFlightId", ref entry.PartFlightId);
            node.TryGetValue("moduleIndex", ref entry.ModuleIndex);
            node.TryGetValue("subjectId", ref entry.SubjectId);
            node.TryGetValue("experimentId", ref entry.ExperimentId);
            node.TryGetValue("bodyName", ref entry.BodyName);
            node.TryGetValue("situation", ref entry.Situation);
            node.TryGetValue("biome", ref entry.Biome);
            node.TryGetValue("scienceValueRatio", ref entry.ScienceValueRatio);
            node.TryGetValue("bankedSeconds", ref entry.BankedSeconds);
            return entry;
        }
    }
}
