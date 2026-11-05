namespace Fornach.Cli

open Fornach.Domain

type Tier = CombatTier

type ArchetypeInfo =
  { Name: string
    Tier: Tier
    Discipline: CombatMode
    Description: string
    Factory: unit -> Combatant }

type CliOptions =
  | InteractiveMenu
  | RunSimulation of archetypeA: string * archetypeB: string * iterations: int
  | RunGroupSimulation of soloName: string * mobName: string * mobCount: int * iterations: int
  | RunPeerBalanceMatrix
  | RunBalanceMatrix
  | RunBestiarySwarmMatrix of selectedTier: CombatTier option
  | RunProgressionSimulation of runs: int * maxFloors: int

/// Debug/test-play launch options parsed alongside the CliOptions mode.
/// Enabled = --debug/-d (HUD overlay + seed printout);
/// ForcedSeed = --seed N (overrides the randomly chosen run seed).
type DebugOptions =
  { Enabled: bool
    ForcedSeed: int option }
