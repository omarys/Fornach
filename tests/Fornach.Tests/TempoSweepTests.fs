namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

/// The 1-vs-N balance harness used to resolve every round solo-first, so a pack that held the Vor still ate
/// the champion's opening strike — the blind spot that let four combat bugs ship through a green suite.
/// These pin the Vor-ordered phases the sweeps now run, and the telemetry that makes the tempo movement the
/// harness used to discard visible.
module TempoSweepTests =

  let private makeRoller (seed: int) : DiceRoller =
    let rng = System.Random(seed)
    fun min max -> rng.Next(min, max + 1)

  /// A Fighter the pack cannot finish in one round, so both phases resolve and the act order is visible.
  let private makeSolo () =
    { TierFactory.createClassLevel CharacterClass.Fighter 30 with PlayerStyle = true }

  let private makePack (solo: Combatant) (count: int) =
    [ 1 .. count ]
    |> List.map (fun i ->
      { TierFactory.clampEngagedToPlayerLevel (Bestiary.createMonster Bestiary.blightSprite) solo with
          Name = sprintf "Blight Sprite %d" i })

  /// `GroupSingleResult` reports what happened, not who acted first, so the order is taken from the two policy
  /// hooks the harness calls once per side: `ChooseIntent` on the solo's turn and `ChooseDefense` on the
  /// pack's. Both delegate to `Simulation.aiPolicy`, so the fight itself is exactly the sweeps' fight.
  let private runRecordingActOrder (solo: Combatant) (mobs: Combatant list) (seed: int) =
    let calls = ResizeArray<string>()

    let probe =
      { Simulation.aiPolicy with
          ChooseIntent =
            fun s t n ->
              calls.Add "solo"
              Simulation.aiPolicy.ChooseIntent s t n
          ChooseDefense =
            fun s a ->
              calls.Add "pack"
              Simulation.aiPolicy.ChooseDefense s a }

    let result, _ = Simulation.runPreparedGroupMatchWith probe (makeRoller seed) solo mobs 1
    List.ofSeq calls, result

  [<Fact>]
  let ``a pack holding the Vor resolves before a solo that opens in the Nach`` () =
    // The blind spot: a champion ambushed into the Nach still struck first, because the harness never asked
    // who held the initiative. The pack's swing must land before the champion's.
    let solo = makeSolo () |> Combatant.yieldVor
    let mobs = makePack solo 2 |> List.map Combatant.seizeVor

    let calls, _ = runRecordingActOrder solo mobs 5

    Assert.Equal("pack", List.head calls)
    Assert.Contains("solo", calls)

  [<Fact>]
  let ``a solo holding the Vor keeps striking first`` () =
    // The behaviour every existing sweep depends on: with the initiative, the champion opens the round.
    let solo = makeSolo () |> Combatant.seizeVor
    let mobs = makePack solo 2 |> List.map Combatant.yieldVor

    let calls, _ = runRecordingActOrder solo mobs 7

    Assert.Equal("solo", List.head calls)
    Assert.Contains("pack", calls)

  [<Fact>]
  let ``the harness counts the tempo shifts it used to discard`` () =
    // Initiative changes hands inside ActionResolver, which emitted TempoShifted all along — the sweep threw
    // those events away. A fight long enough for exchanges to resolve must report them.
    let roller = makeRoller 23
    let solo = makeSolo () |> Combatant.yieldVor
    let mobs = makePack solo 2 |> List.map Combatant.seizeVor

    let result, _ = Simulation.runPreparedGroupMatchWith Simulation.aiPolicy roller solo mobs 20

    Assert.True(result.Rounds > 1, sprintf "expected a multi-round fight, got %d round(s)" result.Rounds)
    Assert.True(result.TotalTempoShifts > 0, "the harness must observe the tempo shifts the resolver emits")
