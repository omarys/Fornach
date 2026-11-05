namespace Fornach.Domain.Tests

open System
open Fornach.Domain
open Fornach.Engine

/// Headless balance batches, rebuilt on the engine for the tests that used to call the CLI's
/// Simulation module. That wrapper contributed a Spectre progress bar and a roster of named
/// archetypes; neither belongs in the engine's own verification, so the same battles are rebuilt
/// here from the engine's factories.
module BalanceBatches =

  /// The engine's own AI: these batches measure default behaviour, so no game presses are registered.
  let policies : AI.PolicyTable = Map.empty

  /// Fixed base seed, so a rerun replays the same fights. A balance workbench that drifts between
  /// invocations is not a workbench.
  let baseSeed = CombatSimulation.batchBaseSeed

  let grandmasterFighter () = TierFactory.createClassTier CharacterClass.Fighter CombatTier.GrandMaster
  let grandmasterMage () = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster

  /// A novice is level 1 of its class, under the plain name the roster gave it.
  let novice (cls: CharacterClass) (name: string) () =
    { TierFactory.createClassLevel cls 1 with Name = name }

  /// One 1-vs-N press, run the way the old CLI wrapper ran it: the solo is a player build, the mobs
  /// are plain opposition, and each iteration draws its roller from the fixed base seed.
  let groupResults (solo: unit -> Combatant) (mob: unit -> Combatant) (mobCount: int) (iterations: int) : GroupSingleResult list =
    let maxRounds = Math.Max(60, mobCount + 20)
    CombatSimulation.runParallelBatch
      baseSeed
      iterations
      (fun () -> ())
      (fun roller ->
        CombatSimulation.runSingleGroupMatch
          policies
          roller
          (fun () -> { solo () with PlayerStyle = true })
          (fun () -> { mob () with PlayerStyle = false })
          mobCount
          maxRounds)
    |> Array.toList

  let groupSummary (solo: unit -> Combatant) (mob: unit -> Combatant) (mobCount: int) (iterations: int) : GroupSimulationSummary =
    groupResults solo mob mobCount iterations
    |> CombatSimulation.summarizeGroup "solo" "mob" mobCount iterations

  /// One 1-vs-1 duel result with the combatant ids dropped. A duel mints fresh `CombatantId`s on every
  /// run, so raw `SingleCombatResult`s never compare equal; everything a summary is computed from
  /// survives here, which is what the determinism checks are about.
  type DuelOutcome =
    { WinnerName: string option
      WinnerIsA: bool option
      LoserName: string option
      Rounds: int
      Condition: WinCondition
      TotalWhiffs: int
      TotalCrits: int }

  /// One 1-vs-1 duel per seeded iteration.
  let duelOutcomes (a: unit -> Combatant) (b: unit -> Combatant) (iterations: int) : DuelOutcome list =
    CombatSimulation.runParallelBatch
      baseSeed
      iterations
      (fun () -> ())
      (fun roller -> CombatSimulation.runSingleMatch policies roller a b 60)
    |> Array.map (fun r ->
      { WinnerName = r.WinnerName
        WinnerIsA = r.WinnerIsA
        LoserName = r.LoserName
        Rounds = r.Rounds
        Condition = r.Condition
        TotalWhiffs = r.TotalWhiffs
        TotalCrits = r.TotalCrits })
    |> Array.toList
