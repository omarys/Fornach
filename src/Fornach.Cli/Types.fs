namespace Fornach.Cli

open Fornach.Domain

type Tier =
  | Novice
  | Adept
  | Master

type ArchetypeInfo =
  { Name: string
    Tier: Tier
    Discipline: CombatMode
    Description: string
    Factory: unit -> Combatant }

type WinCondition =
  | HealthDepleted
  | MoraleDepleted
  | ExecutionFinisher of Plane
  | Stalemate

type SingleCombatResult =
  { WinnerName: string option
    WinnerId: CombatantId option
    WinnerIsA: bool option
    LoserName: string option
    Rounds: int
    Condition: WinCondition
    TotalWhiffs: int
    TotalCrits: int }

type SimulationSummary =
  { ArchetypeNameA: string
    ArchetypeNameB: string
    TotalIterations: int
    WinsA: int
    WinsB: int
    Stalemates: int
    AvgRounds: float
    MinRounds: int
    MaxRounds: int
    HealthDepletions: int
    MoraleDepletions: int
    Executions: int
    TotalWhiffs: int
    TotalCrits: int }

type CliOptions =
  | InteractiveMenu
  | RunSimulation of archetypeA: string * archetypeB: string * iterations: int
