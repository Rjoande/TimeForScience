using System.Collections.Generic;
using System.Linq;
using KSP.Localization;
using KSP.UI.Screens;
using UnityEngine;

namespace TimeForScience
{
    /// <summary>
    /// Registry and per-frame tick for hidden experiment runs. Tracks the
    /// active vessel, other loaded-but-inactive vessels, and unloaded vessels
    /// (protovessel-only) alike - a run follows its part wherever it is
    /// (decision G, edge-cases.md). Completion on a live module re-invokes the
    /// untouched stock/DMagic method; completion on an unloaded vessel injects
    /// a ScienceData node straight into the protopart's saved ConfigNode and
    /// posts an inbox message (STEP 5, stock-science-flow.md §5).
    /// </summary>
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER)]
    public class TimeForScienceScenario : ScenarioModule
    {
        internal static TimeForScienceScenario Instance { get; private set; }

        private readonly List<TimeForScienceRun> runs = new List<TimeForScienceRun>();
        private readonly HashSet<ModuleScienceExperiment> completingNow = new HashSet<ModuleScienceExperiment>();

        // Idle Deploy-button label ("Action (3s)"): throttled, not per-frame -
        // it is only an estimate, and DMagic doesn't refresh its own button on
        // situation changes the way stock occasionally does (see RefreshIdleLabels).
        private const float IdleLabelRefreshInterval = 1f;
        private float nextIdleLabelRefresh;

        public override void OnAwake()
        {
            Instance = this;
        }

        public override void OnLoad(ConfigNode node)
        {
            runs.Clear();
            foreach (ConfigNode runNode in node.GetNodes("RUN"))
            {
                runs.Add(TimeForScienceRun.Load(runNode));
            }
        }

        public override void OnSave(ConfigNode node)
        {
            foreach (TimeForScienceRun run in runs)
            {
                run.Save(node.AddNode("RUN"));
            }
        }

        internal bool IsCompleting(ModuleScienceExperiment module)
        {
            return completingNow.Contains(module);
        }

        /// <summary>True while a run is registered for this exact module - the
        /// anti-double-start guard (action groups, [x]_Science! "run all", or the
        /// player clicking the visible countdown button in the PAW).</summary>
        internal bool HasRun(ModuleScienceExperiment module)
        {
            return FindRun(module) != null;
        }

        /// <summary>
        /// Registers a new hidden run for this module; from now until completion
        /// the PAW Deploy button doubles as the live countdown (SetProgressText)
        /// and re-triggers are swallowed by the HasRun guard in the patches. If a
        /// run is already active for this exact module, this is a no-op.
        ///
        /// biome/dataAmount/xmitDataScalar/scienceValueRatio/isDMagic/
        /// experimentsLimit are frozen here rather than re-derived later because
        /// STEP 5 may need to complete this run on an unloaded vessel, where none
        /// of them can be read from a live module (see TimeForScienceRun).
        /// </summary>
        internal void TryRegisterRun(
            ModuleScienceExperiment module,
            string subjectId,
            string bodyName,
            ExperimentSituations situation,
            double neededSeconds,
            bool showDialog,
            string biome,
            float dataAmount,
            float xmitDataScalar,
            float scienceValueRatio,
            bool isDMagic,
            int experimentsLimit,
            bool isUS2Advanced = false,
            bool overwrite = false,
            float ecRate = 0f)
        {
            if (FindRun(module) != null)
            {
                return;
            }

            var run = new TimeForScienceRun
            {
                PartFlightId = module.part.flightID,
                ModuleIndex = module.part.Modules.IndexOf(module),
                ExperimentId = module.experimentID,
                SubjectId = subjectId,
                BodyName = bodyName,
                Biome = biome,
                Situation = (int)situation,
                NeededSeconds = neededSeconds,
                AccruedSeconds = 0,
                LastTickUT = Planetarium.GetUniversalTime(),
                ShowDialog = showDialog,
                DataAmount = dataAmount,
                XmitDataScalar = xmitDataScalar,
                ScienceValueRatio = scienceValueRatio,
                IsDMagic = isDMagic,
                ExperimentsLimit = experimentsLimit,
                IsUS2Advanced = isUS2Advanced,
                Overwrite = overwrite,
                ECRate = ecRate,
            };
            runs.Add(run);

            SetProgressText(module, run, paused: false);

            ScreenMessages.PostScreenMessage(
                Localizer.Format("#LOC_T4S_Started", ExperimentTitle(module), ScienceTiming.FormatRemaining(neededSeconds)),
                5f, ScreenMessageStyle.UPPER_LEFT);
        }

        /// <summary>Aborts a run for a module we still have a live reference to
        /// (Reset/Retract while a run is active, or the explicit Cancel button -
        /// §G2/§G4). No-ops if this module has no active run. Always triggered
        /// by the player acting on the module directly, so the owning vessel is
        /// necessarily the active one - a plain screen message is enough.</summary>
        internal void AbortRun(ModuleScienceExperiment module, string locKey)
        {
            TimeForScienceRun run = FindRun(module);
            if (run != null)
            {
                AbortRunInternal(run, module, module.vessel, locKey);
            }
        }

        /// <summary>
        /// Removes the run and notifies the player: a screen message if its
        /// vessel is the one currently being watched, an inbox message
        /// otherwise (bug found 2026-07-15: a SoI-change abort on a background
        /// vessel only ever posted a screen message, easy to miss - or entirely
        /// invisible - while looking at a different vessel or scene).
        /// </summary>
        private void AbortRunInternal(TimeForScienceRun run, ModuleScienceExperiment liveModule, Vessel ownerVessel, string locKey)
        {
            runs.Remove(run);

            string title = liveModule != null ? ExperimentTitle(liveModule) : ExperimentTitleForRun(run);

            if (ownerVessel != null && ownerVessel == FlightGlobals.ActiveVessel)
            {
                ScreenMessages.PostScreenMessage(Localizer.Format(locKey, title), 6f, ScreenMessageStyle.UPPER_LEFT);
            }
            else
            {
                SendAbortInboxMessage(ownerVessel, title, locKey);
            }

            if (liveModule != null)
            {
                SetCancelActive(liveModule, false);
                StockReflection.UpdateModuleUI(liveModule);
            }
        }

        private static void SendAbortInboxMessage(Vessel vessel, string experimentTitle, string locKey)
        {
            if (MessageSystem.Instance == null)
            {
                return;
            }

            string vesselName = vessel != null ? vessel.vesselName : "?";
            string body = Localizer.Format(locKey, experimentTitle);

            MessageSystem.Instance.AddMessage(new MessageSystem.Message(
                vesselName, body,
                MessageSystemButton.MessageButtonColor.ORANGE,
                MessageSystemButton.ButtonIcons.ALERT));
        }

        private static string ExperimentTitle(ModuleScienceExperiment module)
        {
            // DMagic keeps its ScienceExperiment in its own field; the inherited
            // one is normally set too, but don't rely on it.
            ScienceExperiment experiment = DMagicBridge.IsDMagic(module)
                ? (DMagicBridge.GetScienceExp(module) ?? module.experiment)
                : module.experiment;
            return experiment != null ? experiment.experimentTitle : module.experimentID;
        }

        private static string ExperimentTitleForRun(TimeForScienceRun run)
        {
            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(run.ExperimentId);
            return experiment != null ? experiment.experimentTitle : run.ExperimentId;
        }

        private TimeForScienceRun FindRun(ModuleScienceExperiment module)
        {
            uint flightId = module.part.flightID;
            int moduleIndex = module.part.Modules.IndexOf(module);
            return runs.FirstOrDefault(r => r.PartFlightId == flightId && r.ModuleIndex == moduleIndex);
        }

        private void Update()
        {
            // Ticking must keep running in TrackingStation/SpaceCenter too - that
            // is exactly where players go to warp through a background run (bug
            // found 2026-07-15: gating this to LoadedSceneIsFlight meant nothing
            // ever advanced while parked in the Tracking Station; UT kept moving,
            // so the very next Flight-scene tick just caught the whole gap at
            // once). The idle-label poll stays Flight-only: there is no PAW
            // anywhere else.
            GameScenes scene = HighLogic.LoadedScene;
            bool canTick = scene == GameScenes.FLIGHT || scene == GameScenes.TRACKSTATION || scene == GameScenes.SPACECENTER;
            if (!canTick)
            {
                return;
            }

            if (runs.Count > 0)
            {
                TickRuns();
            }

            if (scene == GameScenes.FLIGHT && Time.time >= nextIdleLabelRefresh)
            {
                nextIdleLabelRefresh = Time.time + IdleLabelRefreshInterval;
                RefreshIdleLabels();
            }
        }

        private void TickRuns()
        {
            double now = Planetarium.GetUniversalTime();

            for (int i = runs.Count - 1; i >= 0; i--)
            {
                TimeForScienceRun run = runs[i];

                if (!TryLocate(run, out Vessel ownerVessel, out ModuleScienceExperiment liveModule, out ProtoPartSnapshot protoPart))
                {
                    // Part gone entirely (destroyed, or its vessel recovered) -
                    // abort with no data, no message (decision G).
                    runs.RemoveAt(i);
                    continue;
                }

                double dt = now - run.LastTickUT;
                run.LastTickUT = now;

                bool sameBody = ownerVessel.mainBody.name == run.BodyName;
                bool situationMatches = false;

                if (sameBody)
                {
                    string currentSubjectId = liveModule != null
                        ? ComputeCurrentSubjectId(liveModule)
                        : ComputeCurrentSubjectIdUnloaded(ownerVessel, run);

                    situationMatches = currentSubjectId == run.SubjectId;
                    if (situationMatches)
                    {
                        double effectiveDt = run.ECRate > 0f
                            ? ApplyPowerThrottle(run, liveModule, ownerVessel, dt)
                            : dt;
                        run.AccruedSeconds += effectiveDt;
                    }
                }

                if (run.AccruedSeconds >= run.NeededSeconds)
                {
                    bool completed = liveModule != null
                        ? CompleteRunLive(liveModule, run, ownerVessel == FlightGlobals.ActiveVessel)
                        : CompleteRunUnloaded(protoPart, run, ownerVessel);

                    if (completed)
                    {
                        runs.RemoveAt(i);
                    }
                    // else: deferred (multi-slot DMagic while unloaded, see
                    // CompleteRunUnloaded) - left in the registry, retried next tick.
                    continue;
                }

                if (!sameBody)
                {
                    runs.RemoveAt(i);
                    AbortRunInternal(run, liveModule, ownerVessel, "#LOC_T4S_AbortedSoIChange");
                    continue;
                }

                if (liveModule != null)
                {
                    SetProgressText(liveModule, run, paused: !situationMatches);
                }
            }
        }

        /// <summary>
        /// Draws run.ECRate * dt from the vessel (plain ElectricCharge,
        /// loaded or not) and eases run.Throttle toward however much of that
        /// was actually available, then returns dt scaled by the
        /// (already-updated) throttle.
        ///
        /// Ramping rather than snapping means a one-off short draw failure
        /// only nudges progress, not halts it, and - since MoveTowards is
        /// bounded by RampPerSecond * dt - a single huge background/warp tick
        /// collapses to a plain instantaneous ratio anyway (there is no
        /// meaningful "ramp" to model minute-by-minute across a multi-hour
        /// gap), so one code path serves both cases.
        /// </summary>
        private const float ThrottleRampPerSecond = 0.5f;

        private static double ApplyPowerThrottle(TimeForScienceRun run, ModuleScienceExperiment liveModule, Vessel vessel, double dt)
        {
            float needed = run.ECRate * (float)dt;
            float drawn = liveModule != null
                ? DrawPowerLive(liveModule.part, needed)
                : DrawPowerUnloaded(vessel, needed);

            float instantRatio = needed > 0f ? Mathf.Clamp01(drawn / needed) : 1f;
            run.Throttle = Mathf.MoveTowards(run.Throttle, instantRatio, ThrottleRampPerSecond * (float)dt);
            return dt * run.Throttle;
        }

        /// <summary>Loaded vessels always draw plain ElectricCharge via
        /// Part.RequestResource - no cross-mod resource bridge (RealBattery's
        /// official API was integrated and then removed 2026-07-21: the
        /// integration effort and resulting bugs outweighed the near-zero
        /// benefit, see notes/ec-consumption.md).</summary>
        private static float DrawPowerLive(Part part, float needed)
        {
            if (needed <= 0f)
            {
                return 0f;
            }

            return (float)part.RequestResource("ElectricCharge", (double)needed);
        }

        /// <summary>Unloaded vessels draw plain ElectricCharge too - same
        /// resource as the loaded case, via direct protovessel depletion
        /// since no live Part exists.</summary>
        private static float DrawPowerUnloaded(Vessel vessel, float needed)
        {
            if (needed <= 0f || vessel?.protoVessel?.protoPartSnapshots == null)
            {
                return 0f;
            }

            return DepleteProtoVesselResource(vessel, "ElectricCharge", needed);
        }

        /// <summary>Treats the whole vessel as one shared pool, same simplification
        /// other background-processing mods make for unloaded resource draws -
        /// no live crossfeed/priority simulation to defer to while unloaded.</summary>
        private static float DepleteProtoVesselResource(Vessel vessel, string resourceName, float amount)
        {
            double remaining = amount;

            foreach (ProtoPartSnapshot part in vessel.protoVessel.protoPartSnapshots)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (part.resources == null)
                {
                    continue;
                }

                foreach (ProtoPartResourceSnapshot res in part.resources)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    if (res.resourceName != resourceName || !res.flowState)
                    {
                        continue;
                    }

                    double take = System.Math.Min(res.amount, remaining);
                    res.amount -= take;
                    remaining -= take;
                }
            }

            return (float)(amount - remaining);
        }

        /// <summary>
        /// Finds whatever currently owns this run's part: a live PartModule on
        /// the active vessel (checked first, by far the common case), a live
        /// PartModule on some other loaded-but-inactive vessel, or - if nothing
        /// is loaded - the ProtoPartSnapshot on an unloaded vessel. Vessel
        /// situation/altitude/lat/lon are maintained by KSP for unloaded
        /// (on-rails) vessels too, so ownerVessel alone is enough to compute the
        /// current subject without a live module (see ComputeCurrentSubjectIdUnloaded).
        /// </summary>
        private static bool TryLocate(TimeForScienceRun run, out Vessel ownerVessel, out ModuleScienceExperiment liveModule, out ProtoPartSnapshot protoPart)
        {
            ownerVessel = null;
            liveModule = null;
            protoPart = null;

            Vessel active = FlightGlobals.ActiveVessel;
            if (active != null && TryLocateOnLoadedVessel(active, run, out liveModule))
            {
                ownerVessel = active;
                return true;
            }

            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                if (vessel == active)
                {
                    continue;
                }

                if (vessel.loaded)
                {
                    if (TryLocateOnLoadedVessel(vessel, run, out liveModule))
                    {
                        ownerVessel = vessel;
                        return true;
                    }
                }
                else if (vessel.protoVessel?.protoPartSnapshots != null)
                {
                    ProtoPartSnapshot candidate = vessel.protoVessel.protoPartSnapshots.Find(p => p.flightID == run.PartFlightId);
                    if (candidate != null)
                    {
                        ownerVessel = vessel;
                        protoPart = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryLocateOnLoadedVessel(Vessel vessel, TimeForScienceRun run, out ModuleScienceExperiment module)
        {
            module = null;
            Part part = vessel.Parts?.Find(p => p.flightID == run.PartFlightId);
            if (part == null)
            {
                return false;
            }

            PartModule candidate = part.Modules.Count > run.ModuleIndex ? part.Modules[run.ModuleIndex] : null;
            module = candidate as ModuleScienceExperiment;
            if (module != null && module.experimentID != run.ExperimentId)
            {
                module = null;
            }
            return module != null;
        }

        /// <summary>Subject the module would observe right now, computed the same
        /// way the module itself will at completion: DMagic modules use their own
        /// situation/biome logic, stock uses ScienceUtil (pause model, edge-cases
        /// A+B: the timer only advances while this matches the frozen subject).</summary>
        private static string ComputeCurrentSubjectId(ModuleScienceExperiment module)
        {
            if (DMagicBridge.IsDMagic(module))
            {
                ScienceExperiment experiment = DMagicBridge.GetScienceExp(module);
                if (experiment == null)
                {
                    return "";
                }
                ExperimentSituations situation = DMagicBridge.GetSituation(module);
                string biome = DMagicBridge.GetBiome(module, situation);
                return ScienceTiming.ComputeSubjectId(experiment, module.vessel.mainBody, situation, biome);
            }

            ExperimentSituations stockSituation = ScienceTiming.ComputeCurrentSituation(module.vessel);
            string stockBiome = ScienceTiming.ComputeBiome(module.vessel, module.experiment, stockSituation);
            return ScienceTiming.ComputeSubjectId(module.experiment, module.vessel.mainBody, stockSituation, stockBiome);
        }

        /// <summary>Same computation as ComputeCurrentSubjectId, but for an
        /// unloaded vessel: no live module of either kind exists, so this always
        /// uses the plain stock formula (ScienceExperiment looked up by ID,
        /// situation/biome from the Vessel object, which KSP keeps updated even
        /// on-rails). Safe for DMagic-sourced runs too, since the one case where
        /// DMagic's own getSituation() would differ - asteroid science - is
        /// already excluded at registration time (decision G3).</summary>
        private static string ComputeCurrentSubjectIdUnloaded(Vessel vessel, TimeForScienceRun run)
        {
            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(run.ExperimentId);
            if (experiment == null)
            {
                return "";
            }

            ExperimentSituations situation = ScienceTiming.ComputeCurrentSituation(vessel);
            string biome = ScienceTiming.ComputeBiome(vessel, experiment, situation);
            return ScienceTiming.ComputeSubjectId(experiment, vessel.mainBody, situation, biome);
        }

        /// <summary>
        /// Completes a run on a live module. Bug found 2026-07-15 (E1): the
        /// stock/DMagic results dialog does not check which vessel is actually
        /// being watched, so re-invoking with the frozen ShowDialog flag popped
        /// a dialog for vessel B's experiment while the player was looking at
        /// vessel A. Fix: only ever show the real dialog when this run's vessel
        /// IS the active one; otherwise force a silent completion (still creates
        /// the data/Deployed state correctly) and send the same inbox
        /// notification an unloaded completion would.
        /// </summary>
        private bool CompleteRunLive(ModuleScienceExperiment module, TimeForScienceRun run, bool isActiveVessel)
        {
            SetCancelActive(module, false);
            bool showDialog = isActiveVessel && run.ShowDialog;

            completingNow.Add(module);
            try
            {
                if (DMagicBridge.IsDMagic(module))
                {
                    DMagicBridge.InvokeRunExperiment(module, silent: !showDialog);
                }
                else if (US2Bridge.IsUS2Advanced(module))
                {
                    US2Bridge.InvokeRunExperiment(module, silent: !showDialog, run.Overwrite);
                }
                else
                {
                    StockReflection.SetShowDialogAfter(module, showDialog);
                    StockReflection.InvokeOnScienceComplete(module);
                }
            }
            finally
            {
                completingNow.Remove(module);
            }

            if (!isActiveVessel)
            {
                ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(run.ExperimentId);
                ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(run.SubjectId);
                if (experiment != null && subject != null)
                {
                    float scienceGained = ResearchAndDevelopment.GetScienceValue(run.DataAmount, run.EffectiveScienceValueRatio, subject, 1f);
                    SendCompletionMessage(module.vessel, experiment, run.SubjectId, scienceGained);
                }
            }

            return true;
        }

        /// <summary>
        /// Completes a run whose vessel is unloaded: no PartModule exists to
        /// delegate to, so this builds the ScienceData the module itself would
        /// have created and injects it straight into the protopart's saved
        /// ConfigNode (identical shape to what OnSave/DMagic's/US2's OnSave
        /// would have written), then posts an inbox message with the RESULTS
        /// string (requirement 2). Returns false (defer, don't remove the run
        /// yet) for multi-slot DMagic or US2Advanced experiments
        /// (experimentsLimit > 1): their own "keep" bookkeeping (initialDataList
        /// -> storedScienceReportList, experimentsNumber) needs a live module to
        /// replicate safely - v1 scope limit, see notes/stock-science-flow.md
        /// §5/§5h. Single-slot DMagic (the vast majority of this install's
        /// parts) and all stock/USSimpleScience modules complete normally; US2's
        /// USAdvancedScience wedges default to experimentsLimit=2 and so always
        /// defer for now.
        /// </summary>
        private bool CompleteRunUnloaded(ProtoPartSnapshot protoPart, TimeForScienceRun run, Vessel ownerVessel)
        {
            if ((run.IsDMagic || run.IsUS2Advanced) && run.ExperimentsLimit > 1)
            {
                return false;
            }

            if (protoPart.modules == null || run.ModuleIndex >= protoPart.modules.Count)
            {
                // Can't locate the saved module state; drop rather than retry forever.
                return true;
            }

            ProtoPartModuleSnapshot moduleSnapshot = protoPart.modules[run.ModuleIndex];

            CelestialBody body = FlightGlobals.GetBodyByName(run.BodyName);
            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(run.ExperimentId);
            if (body == null || experiment == null)
            {
                return true;
            }

            var situation = (ExperimentSituations)run.Situation;
            string biome = run.Biome ?? "";
            string displayBiome = string.IsNullOrEmpty(biome) ? "" : ScienceUtil.GetBiomedisplayName(body, biome);

            // Registers the subject in the R&D archive, exactly like the live
            // completion path does inside stock's own OnScienceCompleteDelay.
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, situation, body, biome, displayBiome);

            ScienceData data = (run.IsDMagic || run.IsUS2Advanced)
                ? new ScienceData(run.DataAmount, run.XmitDataScalar, 0f, subject.id, subject.title, triggered: false, protoPart.flightID)
                : new ScienceData(run.DataAmount, run.ScienceValueRatio, run.XmitDataScalar, 0f, subject.id, subject.title, triggered: false, protoPart.flightID);

            moduleSnapshot.moduleValues.RemoveNodes("ScienceData");
            data.Save(moduleSnapshot.moduleValues.AddNode("ScienceData"));
            moduleSnapshot.moduleValues.SetValue("Deployed", true, true);
            if (run.IsDMagic || run.IsUS2Advanced)
            {
                moduleSnapshot.moduleValues.SetValue("IsDeployed", true, true);
            }

            float scienceGained = ResearchAndDevelopment.GetScienceValue(run.DataAmount, run.EffectiveScienceValueRatio, subject, 1f);
            SendCompletionMessage(ownerVessel, experiment, run.SubjectId, scienceGained);

            return true;
        }

        private static void SendCompletionMessage(Vessel vessel, ScienceExperiment experiment, string subjectId, float scienceGained)
        {
            if (MessageSystem.Instance == null)
            {
                return;
            }

            string resultsText = ResearchAndDevelopment.GetResults(subjectId);
            string title = Localizer.Format("#LOC_T4S_InboxTitle", experiment.experimentTitle);
            string body = Localizer.Format("#LOC_T4S_InboxMessage", vessel.vesselName, resultsText, scienceGained.ToString("F1"));

            MessageSystem.Instance.AddMessage(new MessageSystem.Message(
                title, body,
                MessageSystemButton.MessageButtonColor.GREEN,
                MessageSystemButton.ButtonIcons.COMPLETE));
        }

        /// <summary>
        /// Appends a time estimate to the Deploy button label while idle (user
        /// request 2026-07-15), e.g. "Observe Mystery Goo (3s)". Separate from
        /// TryRegisterRun's math on purpose: this only touches modules with no
        /// active run, so it can't affect the already-validated deploy patches.
        /// Only touches buttons the game itself has already decided to show
        /// (deploy.active); DMagic never refreshes its own guiName after OnStart,
        /// so without this poll a moved vessel would keep a stale estimate.
        /// </summary>
        private void RefreshIdleLabels()
        {
            Vessel activeVessel = FlightGlobals.ActiveVessel;
            if (activeVessel == null)
            {
                return;
            }

            foreach (Part part in activeVessel.Parts)
            {
                foreach (PartModule partModule in part.Modules)
                {
                    var module = partModule as ModuleScienceExperiment;
                    if (module == null || HasRun(module))
                    {
                        continue;
                    }

                    BaseEvent deploy = module.Events["DeployExperiment"];
                    if (deploy == null || !deploy.active)
                    {
                        continue;
                    }

                    if (!TryEstimateIdle(module, out double seconds, out float ecCost, out float ecRate))
                    {
                        deploy.guiName = module.experimentActionName;
                    }
                    else if (ecCost > 0f && TimeForScienceSettings.ShowECAsRate)
                    {
                        // User-configurable display (Difficulty Settings,
                        // independent of any specific power mod, user request
                        // 2026-07-21): EC/s instead of a lump total.
                        deploy.guiName = Localizer.Format("#LOC_T4S_ActionWithEstimateECRate", module.experimentActionName, ScienceTiming.FormatRemaining(seconds), ecRate.ToString("0.00"));
                    }
                    else if (ecCost > 0f)
                    {
                        deploy.guiName = Localizer.Format("#LOC_T4S_ActionWithEstimateEC", module.experimentActionName, ScienceTiming.FormatRemaining(seconds), ecCost.ToString("0.0"));
                    }
                    else
                    {
                        deploy.guiName = Localizer.Format("#LOC_T4S_ActionWithEstimate", module.experimentActionName, ScienceTiming.FormatRemaining(seconds));
                    }
                }
            }
        }

        /// <summary>Estimated duration (and EC cost/rate, 0 if the feature is
        /// off) if deployed right now; false (no suffix) for built-in/custom
        /// timer exclusions, asteroid science, or a subject already worth ~0
        /// here (decisions H, G3, A2 in edge-cases.md). Mirrors
        /// ComputeCurrentSubjectId's stock/DMagic dispatch plus the value calc
        /// already used by the two deploy patches.</summary>
        private static bool TryEstimateIdle(ModuleScienceExperiment module, out double seconds, out float ecCost, out float ecRate)
        {
            seconds = 0;
            ecCost = 0;
            ecRate = 0;

            if (module.vessel == null)
            {
                return false;
            }

            if (ScienceExclusions.IsExcludedFromTimer(module.experimentID))
            {
                return false;
            }

            CelestialBody body = module.vessel.mainBody;
            ScienceExperiment experiment;
            float scienceValue;

            if (DMagicBridge.IsDMagic(module))
            {
                if (DMagicBridge.AsteroidInPlay(module))
                {
                    return false;
                }

                experiment = DMagicBridge.GetScienceExp(module);
                if (experiment == null)
                {
                    return false;
                }

                ExperimentSituations dmSituation = DMagicBridge.GetSituation(module);
                string dmBiome = DMagicBridge.GetBiome(module, dmSituation);
                string dmSubjectId = ScienceTiming.ComputeSubjectId(experiment, body, dmSituation, dmBiome);
                float dataAmount = experiment.baseValue * experiment.dataScale * DMagicBridge.GetTotalScienceLevel(module);
                scienceValue = ScienceTiming.ComputeScienceValueForData(dataAmount, experiment, body, dmSituation, dmBiome, dmSubjectId, 1f);
            }
            else
            {
                experiment = module.experiment;
                if (experiment == null)
                {
                    return false;
                }

                ExperimentSituations situation = ScienceTiming.ComputeCurrentSituation(module.vessel);
                string biome = ScienceTiming.ComputeBiome(module.vessel, experiment, situation);
                string subjectId = ScienceTiming.ComputeSubjectId(experiment, body, situation, biome);
                scienceValue = ScienceTiming.ComputeScienceValue(experiment, body, situation, biome, subjectId, module.scienceValueRatio);
            }

            if (scienceValue < ScienceTiming.ScienceEpsilon)
            {
                return false;
            }

            seconds = ScienceTiming.ComputeRunSeconds(scienceValue, module.vessel);
            ecRate = TimeForScienceSettings.ComputeECRate(experiment);
            ecCost = ecRate * (float)seconds;
            return true;
        }

        private static void SetCancelActive(ModuleScienceExperiment module, bool active)
        {
            BaseEvent cancel = module.Events[Patch_InjectCancelEvent.CancelEventName];
            if (cancel != null)
            {
                cancel.active = active;
            }
        }

        private static void SetProgressText(ModuleScienceExperiment module, TimeForScienceRun run, bool paused)
        {
            string text;
            if (paused)
            {
                text = Localizer.Format("#LOC_T4S_Paused");
            }
            else if (run.ECRate > 0f && run.Throttle < 0.99f)
            {
                // Low/no power: still "Running" (situation is fine), just
                // ticking slower - see ApplyPowerThrottle. Not a third "paused"
                // state on purpose (edge-cases.md/ec-consumption.md).
                text = Localizer.Format("#LOC_T4S_RunningLowPower",
                    ScienceTiming.FormatRemaining(run.NeededSeconds - run.AccruedSeconds),
                    (run.Throttle * 100f).ToString("F0"));
            }
            else
            {
                text = Localizer.Format("#LOC_T4S_Running", ScienceTiming.FormatRemaining(run.NeededSeconds - run.AccruedSeconds));
            }

            // The Deploy button stays VISIBLE and doubles as the live countdown
            // (user request 2026-07-14); clicking it is harmless thanks to the
            // HasRun guard in the patches. Stock's updateModuleUI and DMagic's
            // eventsCheck occasionally rewrite these, but we win every frame.
            BaseEvent deploy = module.Events["DeployExperiment"];
            deploy.guiName = text;
            deploy.active = true;

            // The EVA (unfocused) variant stays disabled during the run.
            module.Events["DeployExperimentExternal"].active = false;

            SetCancelActive(module, true);
        }
    }
}
