namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

/// A Blight Sprite ambush reported looping in defence forever: the pack's opening strike won the player
/// the Vor through an Indes answer, then the recovery the player had picked from the Nach -- the only
/// menu a Nach player is offered -- spent it again on the same foe. These pin the two halves of that.
module TempoDeadlockTests =

  let private makeRoller (seed: int) : DiceRoller =
    let rng = System.Random(seed)
    fun min max -> rng.Next(min, max + 1)

  let private makePlayer (level: int) =
    { TierFactory.createClassLevel CharacterClass.Fighter level with PlayerStyle = true }

  let private makeSprite (floorPlayer: Combatant) (i: int) =
    { TierFactory.clampEngagedToPlayerLevel (Bestiary.createMonster Bestiary.blightSprite) floorPlayer with
        Name = sprintf "Blight Sprite %d" i }

  let private isUp (c: Combatant) =
    not c.Health.IsDepleted
    && not c.Morale.IsDepleted
    && not (CollapseState.isCollapsed c.Collapse)

  let private tempoOf (c: Combatant) = if c.IsInVor then "Vor" else "Nach"

  let private describe (p: Combatant) (foes: Combatant list) =
    sprintf
      "player %s [%d hp, %A] | %s"
      (tempoOf p)
      p.Health.Current
      p.Collapse
      (foes
       |> List.map (fun f -> sprintf "%s %s [%d hp]" f.Name (tempoOf f) f.Health.Current)
       |> String.concat ", ")

  [<Fact>]
  let ``a recovery against a foe already in the Nach does not surrender the Vor`` () =
    // The foe yielded the initiative; a recovery has nobody to hand it to, so it must not spend it.
    // This is the step that revoked the Vor the player had just won in the pack's opening strike.
    let roller = makeRoller 11
    let player = makePlayer 8
    let foe = makeSprite player 1 |> Combatant.yieldVor

    let res =
      ActionResolver.resolveExWithDefense roller (RecoveryAction DefensiveReset.SteadyForm) None player foe 0

    Assert.True(res.Actor.IsInVor, "the recovering player keeps the Vor when the foe is in the Nach")
    Assert.True(res.Target.IsInNach, "the foe stays in the Nach")

  [<Fact>]
  let ``a recovery from the Nach does not invent a Vor transfer`` () =
    // Neither side holds it, so there is nothing to yield and nothing to seize.
    let roller = makeRoller 13
    let player = makePlayer 8 |> Combatant.yieldVor
    let foe = makeSprite player 1 |> Combatant.yieldVor

    let res =
      ActionResolver.resolveExWithDefense roller (RecoveryAction DefensiveReset.SteadyForm) None player foe 0

    Assert.True(res.Actor.IsInNach, "the player stays in the Nach")
    Assert.True(res.Target.IsInNach, "a recovery must not hand the Vor to a foe that had yielded it")

  [<Fact>]
  let ``a recovery from the Vor still costs the initiative against a foe holding it`` () =
    // The cost the rule exists for: recovering while the foe is still in the Vor gives it up.
    let roller = makeRoller 17
    let player = makePlayer 8
    let foe = makeSprite player 1

    let res =
      ActionResolver.resolveExWithDefense roller (RecoveryAction DefensiveReset.SteadyForm) None player foe 0

    Assert.True(res.Actor.IsInNach, "the recovering player yields the Vor")
    Assert.True(res.Target.IsInVor, "the foe takes the initiative")

  [<Fact>]
  let ``the Vor won by holding a defense survives the next recovery`` () =
    // The reported loop, in the order it happens. The sprite opens; the player answers with an active
    // defense and wins the Vor (ActionResolver.fs, `finalActor` branch); the pack's remaining sprites
    // yield their own tempo; the player then recovers. Before the fix that recovery handed the Vor
    // straight back to the sprite it had just been taken from, so the exchange could never progress.
    let roller = makeRoller 23
    let player = makePlayer 8 |> Combatant.yieldVor
    let sprite = makeSprite player 1

    let defended =
      ActionResolver.resolveExWithDefense
        roller
        (ActiveDefense(FortitudeGuard true))
        (Some(Guard(FortitudeGuard true)))
        player
        sprite
        0

    Assert.True(defended.Actor.IsInVor, "holding a defense seizes the Vor")
    Assert.True(defended.Target.IsInNach, "the sprite that was answered yields it")

    let recovered =
      ActionResolver.resolveExWithDefense
        roller
        (RecoveryAction DefensiveReset.SteadyForm)
        None
        defended.Actor
        defended.Target
        0

    Assert.True(recovered.Actor.IsInVor, "the recovery must not hand the Vor back to the sprite")
    Assert.True(recovered.Target.IsInNach, "the sprite stays in the Nach")
