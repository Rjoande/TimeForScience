# Time For Science

A Kerbal Space Program mod that makes science experiments take real time to complete, proportional to the science they'll actually yield, instead of finishing the instant you click Deploy.

A magnetometer reading over Kerbin takes seconds. A first survey of a distant biome on Eve takes minutes. Run it again once the subject's mostly tapped out and it's over almost instantly, handy for "be at this exact point" contracts. And it all keeps running while you warp away or switch vessels.

## What it does

- **Time proportional to science, calculated on the real subject.** Duration is derived from the actual science value the experiment will produce right now (not an estimate), computed post-hoc against the specific subject: seconds in atmosphere/suborbital flight, longer in orbit and beyond.
- **Repeated observations get faster.** Once a subject is mostly exhausted, re-running the same experiment there is correspondingly quick, down to effectively instant at ~0 marginal science.
- **Runs in the background.** Warp away, switch to another vessel, or leave it running unattended: the observation keeps ticking on unloaded and inactive vessels alike, pausing if the situation or biome changes and resuming if it returns to the original conditions before completion (an actual change of celestial body aborts it, with no data lost. Nothing was collected yet).
- **Live countdown, no results dialog when you're not looking.** The Deploy button doubles as a running countdown while foregrounded; a background completion posts an inbox message with the real experiment results instead of popping a dialog for a vessel you're not watching.
- **Idle time estimate.** The Deploy button shows the expected duration before you even start.
- **Explicit cancel control**, reachable from EVA too, visible only while an observation is actually running.
- **Optional Electric Charge consumption** (Difficulty Settings, off by default): draws EC per second proportional to the experiment's base science value. A brief power shortfall ramps progress down rather than hard-pausing it, so temporary shade or a shadowed orbit doesn't stall everything outright. The idle-button estimate can show either a total EC cost or an EC/s rate, whichever you prefer.
- **Configurable exclusions**: crew report, EVA report and surface sample stay quick/free by default; add your own custom or modded experiment IDs to `Config/Exclusions.cfg` to exclude them from the timer and/or from EC consumption too.

## Compatibility

Works with stock science modules plus:

- **DMagicScienceAnimate** and **Universal Storage 2**'s own science modules (both use an independent deploy path from stock, detected and handled directly, not just tolerated).
- **`[x]_Science!`**.
- **ContractConfigurator**, **ResearchBodies**, **SCANsat**, **FinalFrontier**, **Strategia**: verified, no conflicts.
- **AGExt / KRAB / KRILL** action-group triggered deploys.

**Not compatible with ExperimentsTakeTime**, both change stock experiment behavior in incompatible ways (ExperimentsTakeTime renames the stock module via a `:FINAL` patch). Remove or disable ExperimentsTakeTime before installing this mod.

## Requirements

- Kerbal Space Program 1.12.5
- [Harmony 2](https://github.com/KSPModdingLibs/HarmonyKSP) (hard dependency)

## Installation

Copy the contents of this repository into your `GameData` folder, so you end up with `GameData/TimeForScience/...`. Make sure Harmony 2 is installed alongside it.

## Known limitations

- Multi-slot DMagic/Universal Storage 2 experiments (parts that keep more than one result before you retrieve them) don't complete while their vessel is unloaded; completion is deferred until the vessel reloads, to avoid replicating that bookkeeping by hand without a live module.
- Asteroid science (DMagic) runs instantly rather than timed: its synthetic, per-encounter subjects don't fit the same duration model.
- Background completion always uses an inbox message; there's no "pop a dialog when you switch back to that vessel" alternative.
- The Electric Charge rate is a single global multiplier, no separate rate per situation (space/atmosphere/surface).

## License

[MIT](LICENSE).

## Credits

Author: Rjoande. Built with the help of Claude Code.
