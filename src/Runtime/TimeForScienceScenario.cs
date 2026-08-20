using System.Collections.Generic;
using System.Linq;
using KSP.Localization;
using KSP.UI.Screens;
using RealBattery;
using UnityEngine;

namespace TimeForScience
{
    /// <summary>
    /// Registry and per-frame tick for hidden experiment runs. Tracks the
    /// active vessel, other loaded-but-inactive vessels, and unloaded vessels
    /// alike. Completion on a live module re-invokes the untouched
    /// stock/DMagic/US2 method; completion on an unloaded vessel injects a
    /// ScienceData node into the protopart's saved ConfigNode and posts an
    /// inbox message.
    /// </summary>
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER)]
    public class TimeForScienceScenario : ScenarioModule
    {
        internal static TimeForScienceScenario Instance { get; private set; }

        private readonly List<TimeForScienceRun> runs = new List<TimeForScienceRun>();
        private readonly List<ScienceBankEntry> banks = new List<ScienceBankEntry>();
        private readonly HashSet<ModuleScienceExperiment> completingNow = new HashSet<ModuleScienceExperiment>();

        // Throttled, not per-frame: only an estimate, and DMagic doesn't
        // refresh its own button on situation changes the way stock does.
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

            banks.Clear();
            foreach (ConfigNode bankNode in node.GetNodes("BANK"))
            {
                ScienceBankEntry entry = ScienceBankEntry.Load(bankNode);
                if (entry.BankedSeconds > 0 && !string.IsNullOrEmpty(entry.SubjectId))
                {
                    banks.Add(entry);
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            foreach (TimeForScienceRun run in runs)
            {
                run.Save(node.AddNode("RUN"));
            }

            foreach (ScienceBankEntry bank in banks)
            {
                bank.Save(node.AddNode("BANK"));
            }
        }

        internal bool IsCompleting(ModuleScienceExperiment module)
        {
            return completingNow.Contains(module);
        }

        /// <summary>True while a run is registered for this exact module - the
        /// anti-double-start guard.</summary>
        internal bool HasRun(ModuleScienceExperiment module)
        {
            return FindRun(module) != null;
        }

        /// <summary>
        /// Registers a new hidden run for this module; the PAW Deploy button
        /// doubles as the live countdown until completion, and re-triggers are
        /// swallowed by the HasRun guard in the patches. No-op if a run is
        /// already active for this exact module.
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

            uint partFlightId = module.part.flightID;
            int moduleIndex = module.part.Modules.IndexOf(module);

            // Any time already banked for this exact subject counts toward
            // this run from the start - it may even complete on the very
            // next tick if the bank already covers it.
            double banked = ConsumeBank(partFlightId, moduleIndex, subjectId);

            var run = new TimeForScienceRun
            {
                PartFlightId = partFlightId,
                ModuleIndex = moduleIndex,
                ExperimentId = module.experimentID,
                SubjectId = subjectId,
                BodyName = bodyName,
                Biome = biome,
                Situation = (int)situation,
                NeededSeconds = neededSeconds,
                AccruedSeconds = banked,
                LastTickUT = Planetarium.GetUniversalTime(),
                ShowDialog = showDialog,
                DataAmount = dataAmount,
                XmitDataScalar = xmitDataScalar,
                ScienceValueRatio = scienceValueRatio,
                IsDMagic = isDMagic,
                ExperimentsLimit = experimentsLimit,
                IsUS2Advanced = isUS2Advanced,
                Overwrite = overwrite,
                Rerunnable = module.rerunnable,
                ECRate = ecRate,
                HasRealBattery = RealBatteryPowerLedgerWrapper.Installed
                    && module.vessel.Parts.Any(p => p.Modules.Contains("RealBattery")),
            };
            runs.Add(run);

            SetProgressText(module, run, paused: false);

            double remaining = System.Math.Max(0, neededSeconds - banked);
            ScreenMessages.PostScreenMessage(
                Localizer.Format("#LOC_T4S_Started", ExperimentTitle(module), ScienceTiming.FormatRemaining(remaining)),
                5f, ScreenMessageStyle.UPPER_LEFT);
        }

        /// <summary>Aborts a run for a module we still have a live reference to.
        /// No-op if this module has no active run. Always triggered by the
        /// player acting on the module directly, so the owning vessel is
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
        /// otherwise.
        /// </summary>
        private void AbortRunInternal(TimeForScienceRun run, ModuleScienceExperiment liveModule, Vessel ownerVessel, string locKey)
        {
            runs.Remove(run);

            // Time already observed toward this subject isn't wasted - it
            // goes back into the bank so a later deploy starts ahead.
            if (TimeForScienceSettings.BankedProgressEnabled && run.AccruedSeconds > 0)
            {
                CreditBank(run, run.SubjectId, run.Biome, run.AccruedSeconds);
            }

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
            // Must keep ticking in TrackingStation/SpaceCenter too, since
            // that's where players warp through a background run. The
            // idle-label poll stays Flight-only: there is no PAW anywhere else.
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
                    // Part gone entirely (destroyed, or its vessel recovered).
                    runs.RemoveAt(i);
                    continue;
                }

                double dt = now - run.LastTickUT;
                run.LastTickUT = now;

                if (liveModule != null && run.PendingECDebt > 0f)
                {
                    SettleECDebt(run, ownerVessel, now);
                }

                bool sameBody = ownerVessel.mainBody.name == run.BodyName;
                bool situationMatches = false;
                bool banking = false;

                if (sameBody)
                {
                    ExperimentSituations currentSituation;
                    string currentBiome;
                    string currentSubjectId = liveModule != null
                        ? ComputeCurrentSubjectId(liveModule, out currentSituation, out currentBiome)
                        : ComputeCurrentSubjectIdUnloaded(ownerVessel, run, out currentSituation, out currentBiome);

                    situationMatches = currentSubjectId == run.SubjectId;

                    if (situationMatches)
                    {
                        run.AccruedSeconds += ComputeEffectiveDt(run, liveModule, ownerVessel, dt, now);
                    }
                    else if (TimeForScienceSettings.BankedProgressEnabled
                        && run.Rerunnable
                        && !string.IsNullOrEmpty(currentSubjectId)
                        && (int)currentSituation == run.Situation
                        && VesselHasScienceContainer(ownerVessel))
                    {
                        // Same instrument, different biome, still worth
                        // observing, on a vessel that can actually hold the
                        // extra data: bank the time instead of wasting it.
                        banking = TryBankProgress(run, ownerVessel, liveModule, currentSubjectId, currentBiome, dt, now);
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
                    // else: deferred, left in the registry, retried next tick.
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
                    SetProgressText(liveModule, run, paused: !situationMatches, banking);
                }
            }
        }

        /// <summary>Advances EC bookkeeping for this tick's dt and returns
        /// the seconds to credit - shared by progress toward the frozen
        /// subject and by banked progress toward a different one, since it's
        /// the same instrument drawing from the same power budget either
        /// way.</summary>
        private static double ComputeEffectiveDt(TimeForScienceRun run, ModuleScienceExperiment liveModule, Vessel vessel, double dt, double now)
        {
            if (run.ECRate <= 0f)
            {
                return dt;
            }

            if (liveModule == null && run.HasRealBattery)
            {
                if (run.PendingECDebt <= 0f)
                {
                    run.DebtSinceUT = now;
                }
                run.PendingECDebt += run.ECRate * (float)dt;
                return dt;
            }

            return ApplyPowerThrottle(run, liveModule, vessel, dt);
        }

        /// <summary>Credits time toward a subject other than the one this
        /// run is frozen on. Uses the plain stock value formula as an
        /// approximation for this gate only (never DMagic's totalScienceLevel
        /// scaling) - actual payout is always recomputed by the real deploy
        /// patches once a run starts on that subject. Returns whether
        /// anything was credited (false if pruned as exhausted).</summary>
        private bool TryBankProgress(TimeForScienceRun run, Vessel vessel, ModuleScienceExperiment liveModule, string subjectId, string biome, double dt, double now)
        {
            ScienceExperiment experiment = liveModule != null
                ? (DMagicBridge.IsDMagic(liveModule) ? DMagicBridge.GetScienceExp(liveModule) : liveModule.experiment)
                : ResearchAndDevelopment.GetExperiment(run.ExperimentId);
            if (experiment == null)
            {
                return false;
            }

            var situation = (ExperimentSituations)run.Situation;
            float scienceValue = ScienceTiming.ComputeScienceValue(experiment, vessel.mainBody, situation, biome, subjectId, run.EffectiveScienceValueRatio);

            if (scienceValue < ScienceTiming.ScienceEpsilon)
            {
                // Exhausted since it was last banked (e.g. observed by
                // another vessel) - prune rather than leave a dead entry.
                RemoveBank(run.PartFlightId, run.ModuleIndex, subjectId);
                return false;
            }

            double effectiveDt = ComputeEffectiveDt(run, liveModule, vessel, dt, now);
            CreditBank(run, subjectId, biome, effectiveDt);
            return true;
        }

        /// <summary>A probe's onboard storage is finite - banking data from
        /// biomes it isn't actively transmitting requires somewhere to put
        /// it. Presence of the module is enough; no capacity math.</summary>
        private static bool VesselHasScienceContainer(Vessel vessel)
        {
            if (vessel.loaded)
            {
                return vessel.Parts.Any(p => p.Modules.Contains("ModuleScienceContainer"));
            }

            return vessel.protoVessel?.protoPartSnapshots != null
                && vessel.protoVessel.protoPartSnapshots.Any(p => p.modules != null && p.modules.Any(m => m.moduleName == "ModuleScienceContainer"));
        }

        private ScienceBankEntry FindBank(uint partFlightId, int moduleIndex, string subjectId)
        {
            return banks.FirstOrDefault(b => b.PartFlightId == partFlightId && b.ModuleIndex == moduleIndex && b.SubjectId == subjectId);
        }

        private double CreditBank(TimeForScienceRun run, string subjectId, string biome, double seconds)
        {
            ScienceBankEntry entry = FindBank(run.PartFlightId, run.ModuleIndex, subjectId);
            if (entry == null)
            {
                entry = new ScienceBankEntry
                {
                    PartFlightId = run.PartFlightId,
                    ModuleIndex = run.ModuleIndex,
                    SubjectId = subjectId,
                    ExperimentId = run.ExperimentId,
                    BodyName = run.BodyName,
                    Situation = run.Situation,
                    Biome = biome,
                    ScienceValueRatio = run.EffectiveScienceValueRatio,
                };
                banks.Add(entry);
            }
            entry.BankedSeconds += seconds;
            return entry.BankedSeconds;
        }

        private void RemoveBank(uint partFlightId, int moduleIndex, string subjectId)
        {
            banks.RemoveAll(b => b.PartFlightId == partFlightId && b.ModuleIndex == moduleIndex && b.SubjectId == subjectId);
        }

        /// <summary>Removes and returns whatever time is banked for this
        /// exact module+subject, or 0 if none. Honors existing banked time
        /// even if banking is currently disabled - the toggle only gates
        /// accruing new banked time, not spending what's already there.</summary>
        private double ConsumeBank(uint partFlightId, int moduleIndex, string subjectId)
        {
            ScienceBankEntry entry = FindBank(partFlightId, moduleIndex, subjectId);
            if (entry == null)
            {
                return 0;
            }
            banks.Remove(entry);
            return entry.BankedSeconds;
        }

        /// <summary>
        /// Draws run.ECRate * dt from the vessel and eases run.Throttle toward
        /// however much was actually available, then returns dt scaled by the
        /// (already-updated) throttle. Ramping rather than snapping means a
        /// one-off short draw failure only nudges progress, not halts it, and
        /// a huge background/warp tick collapses to a plain instantaneous
        /// ratio anyway, so one code path serves both cases.
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

        /// <summary>Reports EC debt accrued while unloaded via RealBattery's
        /// power-reporting contract now that the vessel is loaded. Any
        /// remainder it couldn't cover stays queued, with a fresh reporting
        /// window, for the next tick's report.</summary>
        private static void SettleECDebt(TimeForScienceRun run, Vessel vessel, double now)
        {
            double elapsed = now - run.DebtSinceUT;
            double covered = RealBatteryPowerLedgerWrapper.ReportConsumedEc(vessel, run.PendingECDebt, elapsed);
            run.PendingECDebt = Mathf.Max(0f, run.PendingECDebt - (float)covered);
            run.DebtSinceUT = now;
        }

        /// <summary>Loaded vessels draw plain ElectricCharge via
        /// Part.RequestResource; RealBattery's own live simulation reconciles
        /// a loaded vessel's deficit/surplus on its own, no special-casing
        /// needed here.</summary>
        private static float DrawPowerLive(Part part, float needed)
        {
            if (needed <= 0f)
            {
                return 0f;
            }

            return (float)part.RequestResource("ElectricCharge", (double)needed);
        }

        /// <summary>Unloaded vessels draw plain ElectricCharge too, via direct
        /// protovessel depletion since no live Part exists.</summary>
        private static float DrawPowerUnloaded(Vessel vessel, float needed)
        {
            if (needed <= 0f || vessel?.protoVessel?.protoPartSnapshots == null)
            {
                return 0f;
            }

            return DepleteProtoVesselResource(vessel, "ElectricCharge", needed);
        }

        /// <summary>Treats the whole vessel as one shared pool - no live
        /// crossfeed/priority simulation to defer to while unloaded.</summary>
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
        /// the active vessel (checked first), a live PartModule on some other
        /// loaded-but-inactive vessel, or the ProtoPartSnapshot on an unloaded
        /// vessel. Vessel situation/altitude/lat/lon stay valid for unloaded
        /// (on-rails) vessels too, so ownerVessel alone is enough to compute
        /// the current subject without a live module.
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
        /// way the module itself will at completion: DMagic modules use their
        /// own situation/biome logic, stock uses ScienceUtil - the timer only
        /// advances while this matches the frozen subject.</summary>
        private static string ComputeCurrentSubjectId(ModuleScienceExperiment module, out ExperimentSituations situation, out string biome)
        {
            if (DMagicBridge.IsDMagic(module))
            {
                ScienceExperiment experiment = DMagicBridge.GetScienceExp(module);
                if (experiment == null)
                {
                    situation = default;
                    biome = "";
                    return "";
                }
                situation = DMagicBridge.GetSituation(module);
                biome = DMagicBridge.GetBiome(module, situation);
                return ScienceTiming.ComputeSubjectId(experiment, module.vessel.mainBody, situation, biome);
            }

            situation = ScienceTiming.ComputeCurrentSituation(module.vessel);
            biome = ScienceTiming.ComputeBiome(module.vessel, module.experiment, situation);
            return ScienceTiming.ComputeSubjectId(module.experiment, module.vessel.mainBody, situation, biome);
        }

        /// <summary>Same as ComputeCurrentSubjectId, but for an unloaded
        /// vessel: no live module exists, so this always uses the plain stock
        /// formula (situation/biome from the Vessel object, which KSP keeps
        /// updated even on-rails).</summary>
        private static string ComputeCurrentSubjectIdUnloaded(Vessel vessel, TimeForScienceRun run, out ExperimentSituations situation, out string biome)
        {
            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(run.ExperimentId);
            if (experiment == null)
            {
                situation = default;
                biome = "";
                return "";
            }

            situation = ScienceTiming.ComputeCurrentSituation(vessel);
            biome = ScienceTiming.ComputeBiome(vessel, experiment, situation);
            return ScienceTiming.ComputeSubjectId(experiment, vessel.mainBody, situation, biome);
        }

        /// <summary>
        /// Completes a run on a live module. Only shows the real results
        /// dialog when this run's vessel is the active one; otherwise
        /// completes silently and sends the same inbox notification an
        /// unloaded completion would.
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
        /// Completes a run whose vessel is unloaded: builds the ScienceData
        /// the module itself would have created and injects it into the
        /// protopart's saved ConfigNode, then posts an inbox message with the
        /// results text. Returns false (defer) for multi-slot DMagic or
        /// US2Advanced experiments (experimentsLimit > 1): their own "keep"
        /// bookkeeping needs a live module to replicate safely.
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
        /// Appends a time estimate to the Deploy button label while idle.
        /// Only touches buttons the game itself has already decided to show;
        /// DMagic never refreshes its own guiName after OnStart, so without
        /// this poll a moved vessel would keep a stale estimate.
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
                    if (module == null)
                    {
                        continue;
                    }

                    // Independent of HasRun: the bank group can be non-empty
                    // (or show the active run as its top row) either way.
                    RefreshBankGroup(module);

                    if (HasRun(module))
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
        /// off) if deployed right now; false for timer-excluded experiments,
        /// asteroid science, or a subject already worth ~0. Mirrors
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
            string subjectId;

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
                subjectId = ScienceTiming.ComputeSubjectId(experiment, body, dmSituation, dmBiome);
                float dataAmount = experiment.baseValue * experiment.dataScale * DMagicBridge.GetTotalScienceLevel(module);
                scienceValue = ScienceTiming.ComputeScienceValueForData(dataAmount, experiment, body, dmSituation, dmBiome, subjectId, 1f);
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
                subjectId = ScienceTiming.ComputeSubjectId(experiment, body, situation, biome);
                scienceValue = ScienceTiming.ComputeScienceValue(experiment, body, situation, biome, subjectId, module.scienceValueRatio);
            }

            if (scienceValue < ScienceTiming.ScienceEpsilon)
            {
                return false;
            }

            seconds = ScienceTiming.ComputeRunSeconds(scienceValue, module.vessel);

            double banked = Instance?.FindBank(module.part.flightID, module.part.Modules.IndexOf(module), subjectId)?.BankedSeconds ?? 0;
            if (banked > 0)
            {
                seconds = System.Math.Max(0, seconds - banked);
            }

            ecRate = TimeForScienceSettings.ComputeECRate(experiment);
            ecCost = ecRate * (float)seconds;
            return true;
        }

        /// <summary>Display name for a body/biome pair, or null for
        /// situations with no biome (e.g. high orbit).</summary>
        private static string ResolveDisplayBiome(string bodyName, string biome)
        {
            if (string.IsNullOrEmpty(biome))
            {
                return null;
            }

            CelestialBody body = FlightGlobals.GetBodyByName(bodyName);
            return body != null ? ScienceUtil.GetBiomedisplayName(body, biome) : biome;
        }

        private static void SetCancelActive(ModuleScienceExperiment module, bool active)
        {
            BaseEvent cancel = module.Events[Patch_InjectCancelEvent.CancelEventName];
            if (cancel != null)
            {
                cancel.active = active;
            }
        }

        private static void SetProgressText(ModuleScienceExperiment module, TimeForScienceRun run, bool paused, bool banking = false)
        {
            string text;
            if (banking)
            {
                text = Localizer.Format("#LOC_T4S_Banking");
            }
            else if (paused)
            {
                text = Localizer.Format("#LOC_T4S_Paused");
            }
            else if (run.ECRate > 0f && run.Throttle < 0.99f)
            {
                text = Localizer.Format("#LOC_T4S_RunningLowPower",
                    ScienceTiming.FormatRemaining(run.NeededSeconds - run.AccruedSeconds),
                    (run.Throttle * 100f).ToString("F0"));
            }
            else
            {
                text = Localizer.Format("#LOC_T4S_Running", ScienceTiming.FormatRemaining(run.NeededSeconds - run.AccruedSeconds));
            }

            // The Deploy button stays visible and doubles as the live
            // countdown; clicking it is harmless thanks to the HasRun guard
            // in the patches.
            BaseEvent deploy = module.Events["DeployExperiment"];
            deploy.guiName = text;
            deploy.active = true;

            module.Events["DeployExperimentExternal"].active = false;

            SetCancelActive(module, true);
        }

        /// <summary>
        /// Populates the "Banked biomes" PAW group for this module: the run
        /// it's currently deployed on (if any, marked with a leading ">" and
        /// always first) plus every bank entry for this exact module, sorted
        /// by remaining time descending. Fixed pool of
        /// Patch_InjectCancelEvent.BankRowCount rows - once exhausted, the
        /// last one becomes a "+N more" summary instead of truncating
        /// silently. Hidden entirely unless banking is enabled and this
        /// module/vessel actually qualifies to bank (see TickRuns).
        /// </summary>
        private void RefreshBankGroup(ModuleScienceExperiment module)
        {
            int rowCount = Patch_InjectCancelEvent.BankRowCount;
            bool eligible = TimeForScienceSettings.BankedProgressEnabled
                && module.rerunnable
                && VesselHasScienceContainer(module.vessel);

            var rows = new List<string>();

            if (eligible)
            {
                TimeForScienceRun run = FindRun(module);
                if (run != null)
                {
                    string biome = ResolveDisplayBiome(run.BodyName, run.Biome) ?? "-";
                    double remaining = System.Math.Max(0, run.NeededSeconds - run.AccruedSeconds);
                    rows.Add(Localizer.Format("#LOC_T4S_BankRowPrimary", biome, ScienceTiming.FormatRemaining(remaining)));
                }

                uint partFlightId = module.part.flightID;
                int moduleIndex = module.part.Modules.IndexOf(module);
                var secondary = new List<(string label, double remaining)>();

                foreach (ScienceBankEntry bank in banks)
                {
                    if (bank.PartFlightId != partFlightId || bank.ModuleIndex != moduleIndex)
                    {
                        continue;
                    }

                    double remaining = ComputeBankRemaining(bank, module.vessel);
                    if (remaining < 0)
                    {
                        continue;
                    }

                    string biome = ResolveDisplayBiome(bank.BodyName, bank.Biome) ?? "-";
                    secondary.Add((Localizer.Format("#LOC_T4S_BankRow", biome, ScienceTiming.FormatRemaining(remaining)), remaining));
                }

                secondary.Sort((a, b) => b.remaining.CompareTo(a.remaining));

                int freeSlots = rowCount - rows.Count;
                if (secondary.Count <= freeSlots)
                {
                    rows.AddRange(secondary.Select(s => s.label));
                }
                else
                {
                    int shown = System.Math.Max(0, freeSlots - 1);
                    rows.AddRange(secondary.Take(shown).Select(s => s.label));
                    rows.Add(Localizer.Format("#LOC_T4S_BankingMore", secondary.Count - shown));
                }
            }

            for (int i = 0; i < rowCount; i++)
            {
                BaseEvent rowEvent = module.Events[Patch_InjectCancelEvent.BankRowEventName(i)];
                if (rowEvent == null)
                {
                    continue;
                }

                if (i < rows.Count)
                {
                    rowEvent.guiName = rows[i];
                    rowEvent.active = true;
                }
                else
                {
                    rowEvent.active = false;
                }
            }
        }

        /// <summary>Time still needed on a banked subject, recomputed fresh
        /// from its frozen components (same approximation as TryBankProgress:
        /// plain stock value formula) rather than cached - a bank entry can
        /// sit unconsumed for a long time and its value can change (e.g.
        /// another vessel submitting the same subject). Negative if the
        /// subject is gone or exhausted.</summary>
        private static double ComputeBankRemaining(ScienceBankEntry bank, Vessel vessel)
        {
            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(bank.ExperimentId);
            CelestialBody body = FlightGlobals.GetBodyByName(bank.BodyName);
            if (experiment == null || body == null)
            {
                return -1;
            }

            var situation = (ExperimentSituations)bank.Situation;
            float scienceValue = ScienceTiming.ComputeScienceValue(experiment, body, situation, bank.Biome, bank.SubjectId, bank.ScienceValueRatio);
            if (scienceValue < ScienceTiming.ScienceEpsilon)
            {
                return -1;
            }

            double neededSeconds = ScienceTiming.ComputeRunSeconds(scienceValue, vessel);
            return System.Math.Max(0, neededSeconds - bank.BankedSeconds);
        }
    }
}
