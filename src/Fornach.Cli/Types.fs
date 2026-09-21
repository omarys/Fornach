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

type GroupSingleResult =
  { SoloWon: bool
    Rounds: int
    OpponentsInitial: int
    OpponentsEliminated: int
    SoloRemainingHPPct: float
    SoloRemainingMoralePct: float
    SoloExhaustion: int
    SoloOverwhelm: int
    TotalCritsDealtByMob: int
    PeakEncirclementPenalty: int
    TotalAoOsTriggered: int
    TotalCleaves: int
    TotalChains: int
    Condition: WinCondition }

type GroupSimulationSummary =
  { SoloArchetypeName: string
    MobArchetypeName: string
    MobCount: int
    TotalIterations: int
    SoloWins: int
    MobWins: int
    Stalemates: int
    SoloWinRate: float
    AvgRounds: float
    MinRounds: int
    MaxRounds: int
    AvgEliminations: float
    EliminationDistribution: (int * int * float) list // (kills, occurrences, percentage)
    AvgPeakPenalty: float
    AvgSoloRemainingHP: float
    AvgAoOsTriggered: float
    AvgCleaves: float
    AvgChains: float
    HealthDepletions: int
    MoraleDepletions: int
    Executions: int }


type CliOptions =
  | InteractiveMenu
  | RunSimulation of archetypeA: string * archetypeB: string * iterations: int
  | RunGroupSimulation of soloName: string * mobName: string * mobCount: int * iterations: int
