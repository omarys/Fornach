namespace Fornach.Story

open System
open System.IO
open System.Reflection
open Ink.Runtime
open Fornach.Domain

type StoryRunner
  (
    inkJson: string,
    player: Combatant,
    ?onDeathAnimation: unit -> unit,
    ?onMemoryAwarded: StoryMemory -> unit,
    ?onClassChosen: string -> unit
  ) =
  let story = Story(inkJson)
  let mutable state = NarrativeState.Create player
  let deathAnim = defaultArg onDeathAnimation ignore
  let memoryCallback = defaultArg onMemoryAwarded ignore
  let classCallback = defaultArg onClassChosen ignore

  let mutable pendingCombatEnemyId: string option = None
  let mutable lastCombatOutcome: CombatOutcome option = None

  do
    // 1. Bind external function: award_memory(memory_id)
    story.BindExternalFunction(
      "award_memory",
      Func<string, obj>(fun (memId: string) ->
        state <- state.AwardMemory memId
        let mem = state.Memories.[memId]
        memoryCallback mem
        box ())
    )

    // 2. Bind external function: start_combat(enemy_id)
    story.BindExternalFunction(
      "start_combat",
      Func<string, obj>(fun (enemyId: string) ->
        pendingCombatEnemyId <- Some enemyId
        box ())
    )

    // 3. Bind external function: check_preparation(prep_name)
    story.BindExternalFunction(
      "check_preparation",
      Func<string, obj>(fun (prepName: string) ->
        let hasPrep =
          state.Player.ActivePreparations
          |> List.exists (fun p ->
            p.Type.ToString().Equals(prepName, StringComparison.OrdinalIgnoreCase))

        box hasPrep)
    )

    // 4. Bind external function: choose_class(class_name)
    story.BindExternalFunction(
      "choose_class",
      Func<string, obj>(fun (className: string) ->
        classCallback className
        box ())
    )

  member this.Story = story
  member this.State = state
  member this.Player = state.Player
  member this.Memories = state.Memories
  member this.PendingCombat = pendingCombatEnemyId
  member this.CanContinue = story.canContinue

  /// Updates the player combatant in the story state (e.g. after class selection or victory rewards)
  member this.UpdatePlayer(updated: Combatant) =
    state <- { state with Player = updated }
  member this.LastCombatOutcome = lastCombatOutcome

  /// Continues the story and yields text events until a choice point, combat trigger, or story end.
  member this.ContinueToNextEvent() : StoryEvent list =
    let events = ResizeArray<StoryEvent>()
    let mutable keepGoing = true

    while keepGoing && story.canContinue && pendingCombatEnemyId.IsNone do
      let text = story.Continue().Trim()

      if not (String.IsNullOrWhiteSpace text) then
        events.Add(StoryEvent.TextProduced text)

    // Check if an external combat function was invoked
    match pendingCombatEnemyId with
    | Some enemyId ->
      pendingCombatEnemyId <- None
      let enemy = StoryBosses.createEnemy enemyId
      events.Add(StoryEvent.CombatInitiated(enemyId, enemy))
    | None -> ()

    Seq.toList events

  /// Resolves an active combat encounter.
  /// If player is victorious, sets Ink knot to nextKnot (defaults to post_combat).
  /// If player is defeated, triggers death animation and routes Ink to game_over_respawn.
  member this.ResolveCombat(outcome: CombatOutcome, ?nextKnot: string) =
    lastCombatOutcome <- Some outcome

    match outcome with
    | PlayerVictorious ->
      let targetKnot = defaultArg nextKnot "post_combat"
      try
        story.ChoosePathString targetKnot
      with _ ->
        ()
    | PlayerDefeated ->
      state <- { state with IsGameOver = true }
      deathAnim ()
      story.ChoosePathString "game_over_respawn"

  /// Selects an active choice by zero-based index.
  member this.ChooseChoice(index: int) =
    if index >= 0 && index < story.currentChoices.Count then
      story.ChooseChoiceIndex index
    else
      invalidArg
        "index"
        (sprintf
          "Choice index %d is out of range (0..%d)"
          index
          (story.currentChoices.Count - 1))

  /// Returns the current list of available choices as (index, text).
  member this.CurrentChoices: (int * string) list =
    story.currentChoices |> Seq.mapi (fun i c -> i, c.text) |> Seq.toList

  /// Helper to load the prologue.ink.json from embedded resource or disk.
  static member LoadPrologueJson() : string =
    let asm = Assembly.GetExecutingAssembly()

    let resourceName =
      asm.GetManifestResourceNames()
      |> Array.tryFind (fun n ->
        n.EndsWith("prologue.ink.json", StringComparison.OrdinalIgnoreCase))

    match resourceName with
    | Some res ->
      use stream = asm.GetManifestResourceStream res
      use reader = new StreamReader(stream)
      reader.ReadToEnd()
    | None ->
      let candidatePaths =
        [ "Scripts/prologue.ink.json"
          "src/Fornach.Story/Scripts/prologue.ink.json"
          "../../src/Fornach.Story/Scripts/prologue.ink.json"
          "../../../src/Fornach.Story/Scripts/prologue.ink.json" ]

      let found = candidatePaths |> List.tryFind File.Exists

      match found with
      | Some p -> File.ReadAllText p
      | None ->
        failwith
          "Could not locate prologue.ink.json as an embedded resource or relative file."
