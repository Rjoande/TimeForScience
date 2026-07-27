# Changelog

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
- **Compatibility**: stock science modules, DMagicScienceAnimate, Universal Storage 2's science modules, `[x]_Science!` (including "run all"), ContractConfigurator, ResearchBodies, SCANsat, FinalFrontier, Strategia, and AGExt/KRAB action-group deploys — all verified, no dedicated bridge needed beyond DMagic/Universal Storage 2's own independent deploy paths.
- **Localization**: English and Italian, full parity (every player-facing string in both).

### Known limitations

- Multi-slot DMagic/Universal Storage 2 experiments don't complete while their vessel is unloaded — deferred until the vessel reloads.
- DMagic asteroid science runs instantly rather than timed (synthetic per-encounter subjects don't fit the duration model).
- No "pop a dialog on return to the vessel" alternative to the background inbox message.
- Electric Charge rate is a single global multiplier, not per-situation.

### Requirements

KSP 1.12.5, [Harmony 2](https://github.com/KSPModdingLibs/HarmonyKSP) (hard dependency). Not compatible with ExperimentsTakeTime — remove/disable it first.
