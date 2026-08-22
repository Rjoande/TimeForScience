# Changelog

## [0.2.1] — Exclusion config additions and cleanup

### Added

- `evaScience` added to the default exclusions (`Config/Exclusions.cfg`), alongside `evaReport` and `surfaceSample`: always instantaneous, never draws EC.
- New `excludeFromBanking` field in `Config/Exclusions.cfg`: excludes an experiment from banking progress on other biomes specifically, even with the Difficulty Setting on. Redundant (but harmless) on anything already `excludeFromTimer = true`, since an instantaneous experiment never has an active run to bank against.

### Changed

- Internal cleanup: removed leftover development diagnostic logging (per-run/per-patch debug messages) and trimmed overly verbose code comments. No behavior change.

## [0.2.0] — Banked progress and RealBattery compatibility

### Added

- **Bank Progress on Other Biomes** (Difficulty Settings, off by default): while an observation is running, time spent flying over a different biome in the same situation (one that's still worth observing) is banked per-biome instead of wasted, shown in a collapsible "Banked biomes" PAW group. A later Deploy on a banked biome starts with that time already credited. Requires a rerunnable experiment and a science container on board. Idea from forum user DeadJohn, who pointed out that opportunistic multi-biome passes (e.g. polar orbits over many small biomes) were otherwise always wasted time.
- **RealBattery compatibility**: if [RealBattery](https://github.com/Rjoande/RealBattery) is installed, EC debt accrued by a timed observation while the vessel is unloaded is settled through RealBattery's own reporting API (`RealBatteryPowerLedger`) instead of a blind stock draw, so it plays correctly with RealBattery's charge/discharge simulation.
- **Difficulty presets**: EC consumption and its rate scale with the stock preset (off on Easy/Normal, on from Moderate up, higher rate on Hard); banked progress defaults on for Easy only, off elsewhere, matching the mod's own off-by-default setting. Custom preset leaves your own choices untouched.

### Changed

- Difficulty Settings: the EC Consumption Rate slider no longer has an accompanying text-entry box (it was rendering at full row width, unfixable through the stock UI's own attributes); the slider alone remains just as precise.
- Difficulty Settings: EC Consumption Rate and Show EC Cost As Rate are now greyed out (not hidden) whenever Consume Electric Charge is off, making the dependency visible.

### Known limitations

- The "Banked biomes" PAW group shows at most 6 rows per module (the rest collapse into a "+N more" line); all banked biomes still count fully toward a later Deploy regardless of whether they're shown.

## [0.1.0] — First public release

### What it is

TimeForScience makes science experiments take real time to complete — proportional to the science they'll actually yield, computed on the real subject rather than estimated — instead of finishing the instant you click Deploy. Runs in the background too: warp away or switch vessels and the observation keeps ticking, pausing on a situation/biome change and resuming if it returns before completion.

### Added

- **Time-proportional experiments**: duration derived from the actual science value the subject will produce right now (seconds in atmosphere/suborbital, longer in orbit and beyond); repeated observations of an already-mostly-exhausted subject run correspondingly faster, down to effectively instant.
- **Background execution**: observations keep progressing on unloaded and loaded-but-inactive vessels alike, aborting cleanly (no data) on an actual change of celestial body.
- **Live PAW countdown** on the Deploy button while foregrounded, plus an idle time estimate before you even start.
- **Explicit cancel control**, reachable from EVA, visible only while an observation is running.
- **Background completion via inbox message** (with the real experiment results text) instead of a results dialog popping up for a vessel you're not watching; the normal stock dialog still shows when you're actually looking at the vessel.
- **Optional Electric Charge consumption** (Difficulty Settings, off by default): EC/s proportional to the experiment's base science value, with a ramped throttle model so a brief power shortfall slows progress instead of hard-pausing it. Idle-button estimate can show a total cost or an EC/s rate, your choice.
- **Configurable exclusions** (`Config/Exclusions.cfg`): crew report, EVA report and surface sample stay quick/free by default; add custom or modded experiment IDs to exclude them from the timer and/or EC consumption too.
- **Compatibility**: stock science modules, DMagicScienceAnimate, Universal Storage 2's science modules, `[x]_Science!` (including "run all"), ContractConfigurator, ResearchBodies, SCANsat, FinalFrontier, Strategia, and AGExt/KRAB/KRILL action-group deploys. All verified, no dedicated bridge needed beyond DMagic/Universal Storage 2's own independent deploy paths.
- **Localization**: English and Italian.

### Known limitations

- Multi-slot DMagic/Universal Storage 2 experiments don't complete while their vessel is unloaded (deferred until the vessel reloads).
- DMagic asteroid science runs instantly rather than timed (synthetic per-encounter subjects don't fit the duration model).
- No "pop a dialog on return to the vessel" alternative to the background inbox message.
- By design, Electric Charge rate is a single global multiplier, not per-situation.

### Requirements

KSP 1.12.5, [Harmony 2](https://github.com/KSPModdingLibs/HarmonyKSP) (hard dependency). Not compatible with ExperimentsTakeTime — remove/disable it first.
