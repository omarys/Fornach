namespace Fornach.Story

open System
open Fornach.Domain

/// A fragment of repressed trauma or memory unlocked through the narrative
type StoryMemory =
  { Id: string
    Name: string
    Description: string
    UnlockedAt: DateTime }

/// The state of the narrative progression and player memory bank
type NarrativeState =
  { Player: Combatant
    Memories: Map<string, StoryMemory>
    CurrentKnot: string
    IsGameOver: bool }

  static member Create player =
    { Player = player
      Memories = Map.empty
      CurrentKnot = "waking"
      IsGameOver = false }

  member this.HasMemory id = Map.containsKey id this.Memories

  member this.AwardMemory id =
    let desc =
      match id with
      | "rain_and_headlights" ->
        "A distant blur of headlights in the driving rain, and a hand slipping away."
      | "prepared_mind" ->
        "The instinct to check pouches before rushing into danger."
      | "shattered_windshield" ->
        "Shattered glass reflected in a puddle: the memory of the crosswalk impact."
      | "broken_umbrella" ->
        "Running into the downpour after an argument, leaving an umbrella behind on the porch."
      | "shouting_in_the_hallway" ->
        "The harsh, regretful words shouted before the front door slammed shut."
      | "hospital_monitors" ->
        "The sterile rhythm of intensive care monitors and desperate whispered bargains."
      | "empty_bedroom" ->
        "The suffocating quiet of standing before an untouched bedroom door."
      | "twin_smile" ->
        "Lyra's serene, gentle smile in the sunlight—forgiving and at peace."
      | other -> sprintf "Memory fragment: %s" other

    let mem =
      { Id = id
        Name = id.Replace("_", " ")
        Description = desc
        UnlockedAt = DateTime.UtcNow }

    { this with Memories = Map.add id mem this.Memories }

/// Story event emitted during progression
[<RequireQualifiedAccess>]
type StoryEvent =
  | TextProduced of string
  | MemoryUnlocked of StoryMemory
  | CombatInitiated of enemyId: string * enemy: Combatant
  | GameOverTriggered
  | RespawnTriggered

/// Resulting status from a combat encounter
type CombatOutcome =
  | PlayerVictorious
  | PlayerDefeated
