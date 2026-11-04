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

  [<Fact>]
  let ``a guard taken from the Nach in a group turn seizes the Vor and cleaves nobody`` () =
    // Reported: a Bramble Boar ambushed the player into the Nach, and Fortitude Guard did nothing --
    // no tempo, no collapse, no surrender, indefinitely. Meanwhile a Bandit that joined took damage
    // from the guard, and stopped taking it once it died. A defensive posture must seize the Vor it is
    // defending against and must not damage anything.
    let boar = { makeSprite (makePlayer 3) 1 with Name = "Bramble Boar"; Tempo = TacticalTiming.Vor }
    let bandit = { makeSprite (makePlayer 3) 2 with Name = "Bandit"; Tempo = TacticalTiming.Nach }

    let player =
      { makePlayer 3 with
          Tempo = TacticalTiming.Nach
          EnemiesFaced = 2 }

    let res = ActionResolver.resolveGroupTurn (makeRoller 7) (ActiveDefense (FortitudeGuard false)) player boar [ bandit ]

    let banditAfter =
      res.SecondaryTargets |> List.tryFind (fun (c: Combatant) -> c.Id = bandit.Id)

    Assert.True(res.Actor.IsInVor, sprintf "the guard must seize the Vor from the ambusher (got %s)" (describe res.Actor [ res.PrimaryTarget ]))
    Assert.True(res.PrimaryTarget.IsInNach, "the ambusher yields the Vor")
    Assert.Equal(bandit.Health.Current, banditAfter |> Option.map (fun c -> c.Health.Current) |> Option.defaultValue bandit.Health.Current)

  [<Fact>]
  let ``the pack's answer to a guard does not take back the Vor the guard just won`` () =
    // Reported: Fortitude Guard was taken several times and the exchange never moved -- the player stayed
    // in the Nach with no collapse and no surrender while the pack kept the initiative. The guard wins the
    // Vor from the ambusher, and then the pack's answer to that same guard strike hands it straight back,
    // so a player who defends never keeps the initiative past the end of the round.
    let mutable boar = { makeSprite (makePlayer 3) 1 with Name = "Bramble Boar"; Tempo = TacticalTiming.Vor }
    let mutable player = { makePlayer 3 with Tempo = TacticalTiming.Nach; EnemiesFaced = 1 }

    // The guard the player picked, which is also the defense it answers the pack's strike with.
    let guarded = ActionResolver.resolveGroupTurn (makeRoller 11) (ActiveDefense (FortitudeGuard false)) player boar []
    player <- guarded.Actor
    boar <- guarded.PrimaryTarget

    Assert.True(player.IsInVor, sprintf "precondition: the guard seized the Vor (%s)" (describe player [ boar ]))

    let mutable round = 1
    for _ in 1..3 do
      // The pack's answer: the foe it was answered against strikes back, with the guard still up.
      let answer =
        ActionResolver.resolveExWithDefense
          (makeRoller 13)
          (StandardAttack(ForceStrike false))
          (Some(Guard(FortitudeGuard false)))
          boar
          player
          0

      boar <- answer.Actor
      player <- answer.Target
      round <- round + 1

    Assert.True(player.IsInVor, sprintf "a defended round must not hand the Vor back (round %d: %s)" round (describe player [ boar ]))

  [<Fact>]
  let ``a guard is not swallowed when the foe does something other than strike`` () =
    // Reported: defending several times against a Bramble Boar that held the Vor did nothing at all. The
    // guard was only ever handed to `resolveAttack`, so any other opponent action -- bracing, shifting,
    // recovering, deploying -- discarded it: no mitigation, no stacks, no tempo, indefinitely.
    let player = { makePlayer 8 with Tempo = TacticalTiming.Nach; EnemiesFaced = 1 }
    let foe = { makeSprite player 1 with Tempo = TacticalTiming.Vor }

    let res =
      ActionResolver.resolveExWithDefense
        (makeRoller 17)
        (ShiftStance CombatStance.PowerStance)
        (Some(Guard(FortitudeGuard true)))
        foe
        player
        0

    Assert.True(
      res.Target.IsInVor,
      sprintf "a guard must still seize the Vor from a foe that does not strike (%s)" (describe res.Target [ res.Actor ]))

    Assert.True(res.Actor.IsInNach, "the foe it was held against yields the Vor")

  [<Fact>]
  let ``the hold margin scales the status damage a failed strike costs the attacker`` () =
    // The penalty for swinging into a wall was flat however decisively the guard won. A wider margin is a
    // worse loss of the bind and should cost the attacker more, capped so one lopsided answer cannot
    // empty its meters outright.
    let player = { makePlayer 30 with Tempo = TacticalTiming.Nach; EnemiesFaced = 1 }
    let foe =
      { TierFactory.createClassLevel CharacterClass.Fighter 1 with
          Name = "Bramble Boar"
          Tempo = TacticalTiming.Vor }
    let before = foe.Meters.Exhaustion.Value

    let res =
      ActionResolver.resolveExWithDefense
        (makeRoller 19)
        (StandardAttack(ForceStrike false))
        (Some(Guard(FortitudeGuard true)))
        foe
        player
        0

    let whiffed =
      res.Events
      |> List.exists (function
        | CombatEvent.ComboReset(_, msg) -> msg.Contains "failed to penetrate"
        | _ -> false)

    Assert.True(whiffed, sprintf "precondition: the guard turned the strike aside (%A)" res.Events)

    let margin =
      match res.Contest with
      | Some c -> max 1 (min 5 -c.NetHits)
      | None -> 1

    // Physical/Power whiffs pay 10 Exhaustion per margin of the hold, on top of the round's 1 upkeep.
    let spent = res.Actor.Meters.Exhaustion.Value - before
    Assert.Equal(10 * margin + 1, spent)

  [<Fact>]
  let ``a pack that collapsed from exhaustion surrenders the Vor`` () =
    // Reported: the sprites exhausted themselves and collapsed, but the HUD still showed them in the
    // Nach and the player kept a defensive-only menu. A collapsed foe cannot hold the initiative, and
    // the player cannot be pinned in the Nach by nothing.
    let player = { makePlayer 8 with Tempo = TacticalTiming.Nach; EnemiesFaced = 3 }

    let collapsed =
      { makeSprite player 1 with
          Tempo = TacticalTiming.Vor
          Collapse = CollapseState.Collapsed CollapseReason.SomaticAnoxia }

    let after = ActionResolver.settleTempoVacuum player [ collapsed ]

    Assert.True(after.IsInVor, "the uncontested initiative goes to the player")

  [<Fact>]
  let ``an uncontested Vor does not move while a standing foe still holds it`` () =
    let player = { makePlayer 8 with Tempo = TacticalTiming.Nach; EnemiesFaced = 1 }
    let standing = { makeSprite player 1 with Tempo = TacticalTiming.Vor }

    let after = ActionResolver.settleTempoVacuum player [ standing ]

    Assert.True(after.IsInNach, "a foe still on its feet keeps the Vor")
