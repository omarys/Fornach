namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

/// The six Grief Aspects were all unwinnable before the Exhaustion retune: every fight ended with the
/// player's Morale at zero and Health untouched, because per-action Exhaustion reached 100 and converted
/// the next defense into an execution. These pin the outcome rather than the tuning values, so a future
/// stat edit that makes an Aspect unbeatable fails here instead of in play.
module GriefAspectLadderTests =

  let private makeRoller (seed: int) : DiceRoller =
    let rng = System.Random(seed)
    fun min max -> rng.Next(min, max + 1)

  let private kit = (StoryBosses.createProloguePlayer "fighter").EquippedItems

  let private makePlayer (level: int) =
    kit
    |> List.fold
      (fun (c: Combatant) item -> Combatant.applyItemBonus item c)
      (TierFactory.createClassLevel CharacterClass.Warrior level)
    |> fun c -> { c with PlayerStyle = true }

  let private wins (floor: int) (create: unit -> Combatant) (seeds: int) =
    let player = makePlayer (ProgressionScale.storyGuardianLevel floor)
    let boss = create () |> StoryBosses.scaleToStage floor

    [ 1 .. seeds ]
    |> List.filter (fun seed ->
      let result, _ =
        Simulation.runPreparedGroupMatchWith Simulation.aiPolicy (makeRoller seed) player [ boss ] 200

      result.SoloWon)
    |> List.length

  [<Fact>]
  let ``every Grief Aspect is beatable by a level-appropriate champion`` () =
    let aspects =
      [ "Guilt", StoryBosses.createGuiltAspect, 2
        "Denial", StoryBosses.createDenialAspect, 3
        "Anger", StoryBosses.createAngerAspect, 4
        "Bargaining", StoryBosses.createBargainingAspect, 5
        "Depression", StoryBosses.createDepressionAspect, 6
        "Acceptance", StoryBosses.createAcceptanceAspect, 7 ]

    for name, create, floor in aspects do
      let won = wins floor create 8
      Assert.True(won >= 5, sprintf "%s was won in only %d of 8 seeds" name won)
