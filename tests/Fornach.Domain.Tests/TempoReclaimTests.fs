namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// A foe that ambushes from out of sight takes the Vor, pinning the player in the Nach. These pin the
/// ways back out of that pin, and what happens when a combatant finally breaks.
module TempoReclaimTests =

  let private fixedRoller (result: int) : DiceRoller = fun _ _ -> result

  let private player (c: Combatant) = { c with PlayerStyle = true }

  let private bandit () =
    TierFactory.createClassTier CharacterClass.Warrior CombatTier.Veteran
    |> Combatant.seizeVor

  [<Fact>]
  let ``Guarding reclaims the Vor from the foe that holds it`` () =
    // The reported case: pinned in the Nach by an ambusher, guard stacks banked, still no tempo.
    // The old condition was `IsInNach && not target.IsInVor`, so a foe holding the Vor could never lose
    // it to a defended round -- and a braced strike lands-but-is-soaked, so the whiff path never fired.
    let fighter =
      (TierFactory.createClassLevel CharacterClass.Fighter 25 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }
      |> Combatant.addGuardStacks 25

    let foe = bandit ()
    Assert.True(fighter.IsInNach, "precondition: player is in the Nach")
    Assert.True(foe.IsInVor, "precondition: foe holds the Vor")

    let outcome = ActionResolver.resolveActiveDefense (FortitudeGuard true) fighter foe

    Assert.True(outcome.Actor.IsInVor, "a defended round must reclaim the Vor")
    Assert.True(outcome.Target.IsInNach, "the defended foe must be put in the Nach")

  [<Fact>]
  let ``Guarding takes the Vor only from the foe it answered`` () =
    // Seizure is per-target: the rest of a pack keeps whatever tempo it had.
    let fighter =
      (TierFactory.createClassLevel CharacterClass.Fighter 25 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let answered = bandit ()
    let bystander = bandit ()

    let outcome = ActionResolver.resolveActiveDefense (FortitudeGuard true) fighter answered

    Assert.True(outcome.Actor.IsInVor, "the defended foe's Vor must be taken")
    Assert.True(bystander.IsInVor, "an unengaged pack member keeps its own tempo")

  [<Fact>]
  let ``Collapse surrenders the Vor`` () =
    // Reaching 100% on a status meter used to do nothing but mark the combatant execute-eligible, so a
    // collapsed foe kept the Vor and kept swinging.
    let breaking =
      TierFactory.createClassTier CharacterClass.Warrior CombatTier.Veteran
      |> Combatant.seizeVor
      |> Combatant.updateMeters (fun m -> { m with Exhaustion = m.Exhaustion + 100 })

    Assert.True(breaking.IsInVor, "precondition: the foe holds the Vor")

    let collapsed = Combatant.evaluateCollapse breaking

    Assert.True(CollapseState.isCollapsed collapsed.Collapse, "100% Exhaustion must collapse the foe")
    Assert.True(collapsed.IsInNach, "a collapsed combatant surrenders the Vor")
    Assert.True(collapsed.IsExecuteEligible, "a collapsed combatant stays execute-eligible")

  [<Fact>]
  let ``A fully absorbed strike taxes the attacker and hands over the Vor`` () =
    // Braced Fortitude Guard soaks a strike to zero rather than making it miss, so before this the
    // attacker paid nothing: it "landed" for 0 and neither the whiff nor the posture path fired.
    let mutable call = 0
    let alternating min max =
      call <- call + 1
      if call % 2 = 1 then max else min

    let attacker =
      (TierFactory.createClassLevel CharacterClass.Warrior 20 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Vor }

    let defender =
      (TierFactory.createClassLevel CharacterClass.Warrior 20 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }
      |> Combatant.addWard 100000

    let outcome =
      ActionResolver.resolveExWithDefense
        alternating
        (StandardAttack (ForceStrike false))
        None
        attacker
        defender
        0

    let damageDealt =
      outcome.Events
      |> List.sumBy (function CombatEvent.DamageApplied e -> e.Amount | _ -> 0)

    Assert.Equal(0, damageDealt)
    Assert.True(
      outcome.Actor.Meters.Exhaustion.Value >= 20,
      sprintf "an absorbed strike must exhaust the attacker (got %d)" outcome.Actor.Meters.Exhaustion.Value
    )
    Assert.True(outcome.Target.IsInVor, "the defender who absorbed the strike must hold the Vor")

  [<Fact>]
  let ``Status pressure bleeds off out of combat`` () =
    // The 1% a defended round costs is meant to be temporary, so it must not compound across a floor.
    let tired =
      TierFactory.createClassLevel CharacterClass.Fighter 20 |> player
      |> Combatant.updateMeters (fun m ->
        { m with
            Exhaustion = m.Exhaustion + 40
            Frustration = m.Frustration + 40
            Recklessness = m.Recklessness + 40 })

    let rested = TowerSession.recoverOutOfCombat tired 5 0 0 0

    Assert.Equal(30, rested.Meters.Exhaustion.Value)
    Assert.Equal(30, rested.Meters.Frustration.Value)
    Assert.Equal(30, rested.Meters.Recklessness.Value)

  [<Fact>]
  let ``Status decay floors at zero`` () =
    let clean = TierFactory.createClassLevel CharacterClass.Fighter 20 |> player

    let rested = TowerSession.recoverOutOfCombat clean 5 0 0 0

    Assert.Equal(0, rested.Meters.Exhaustion.Value)
    Assert.Equal(0, rested.Meters.Frustration.Value)

  [<Fact>]
  let ``Tempo is per engagement: landing while holding the Vor leaves the defender alone`` () =
    // Reported case: a pack ambush where every sprite held the Vor and the player held one too. The rules
    // treated the Vor as a single pool, so a chip hit from a non-focus foe stripped the tempo the player
    // had won against a different one. That defeder's Vor is only stake in *their* engagement.
    let defender =
      (TierFactory.createClassLevel CharacterClass.Fighter 25 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Vor }
      |> Combatant.addGuardStacks 25

    let insisting = bandit () |> Combatant.seizeVor

    let _, defenderAfter, moved = ActionResolver.resolveLandedTempo insisting defender 3
    Assert.False(moved, "landing while already holding the Vor moves no tempo")
    Assert.True(defenderAfter.IsInVor, "the defender's own engagement is untouched")

  [<Fact>]
  let ``A decisive Nach strike moves the tempo in its own pairing`` () =
    let holder =
      (TierFactory.createClassLevel CharacterClass.Warrior 30 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Vor }

    let striker = bandit () |> Combatant.yieldVor

    let strikerAfter, holderAfter, moved = ActionResolver.resolveLandedTempo striker holder 2
    Assert.True(moved, "two net hits from the Nach still takes the Vor")
    Assert.True(strikerAfter.IsInVor)
    Assert.True(holderAfter.IsInNach)
  [<Fact>]
  let ``The cutoff is arithmetic: ceding needs the lesser best to miss the greater worst`` () =
    // Floor hits are guaranteed, so the greater's worst roll is its floor; a Power pool scores double per
    // die on a six, so the lesser's best is its floor plus twice its pool. Overlap of a single point is
    // enough for the lesser combatant to win, and a possible win is a real win.
    let greaterWorst, _ = DicePool.hitBounds Vector.Power 300 0
    let _, lesserBest = DicePool.hitBounds Vector.Power 0 300

    Assert.True(lesserBest < greaterWorst, "a pool of nothing cannot reach a 300-stat floor")
    Assert.True(DicePool.cannotBeat Vector.Power 300 0)

    // 15 stat points is one guaranteed hit; the lesser side there still overflows it with rolled dice.
    let greaterWorstNear, _ = DicePool.hitBounds Vector.Power 15 0
    let _, lesserBestNear = DicePool.hitBounds Vector.Power 0 15

    Assert.True(lesserBestNear >= greaterWorstNear, "one guaranteed hit is still reachable")
    Assert.False(DicePool.cannotBeat Vector.Power 15 0)

  [<Fact>]
  let ``A novice that wins the bind keeps the win`` () =
    // The rule is about who can win at all, not about rank: once the dice allow the win, taking the Vor
    // is not clawed back from the underdog.
    let master =
      (TierFactory.createClassLevel CharacterClass.Warrior 100 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let novice = TierFactory.createClassLevel CharacterClass.Fighter 1 |> Combatant.yieldVor
    let noviceAfter, masterAfter, moved = ActionResolver.resolveLandedTempo novice master 2

    Assert.True(moved)
    Assert.True(noviceAfter.IsInVor, "a Novice that wins the bind takes the Vor")
    Assert.True(masterAfter.IsInNach)
  [<Fact>]
  let ``Tier floors are derived from the dice, not authored`` () =
    let veteran = ProgressionScale.tierToLevel Veteran
    let master = ProgressionScale.tierToLevel Master
    let grandMaster = ProgressionScale.tierToLevel GrandMaster

    Assert.True(veteran < master && master < grandMaster, "tiers must stay ordered")

    // The floor is the *first* level the tier below cannot touch -- one below it is still contestable.
    Assert.False(ProgressionScale.cannotTouch 1 (veteran - 1), "a starting character can still touch a level just under Veteran")
    Assert.True(ProgressionScale.cannotTouch 1 veteran, "a starting character cannot touch a Veteran")
    Assert.False(ProgressionScale.cannotTouch veteran (master - 1))
    Assert.True(ProgressionScale.cannotTouch veteran master)
    Assert.False(ProgressionScale.cannotTouch master (grandMaster - 1))
    Assert.True(ProgressionScale.cannotTouch master grandMaster)

    Assert.Equal(Novice, ProgressionScale.levelToTier (veteran - 1))
    Assert.Equal(Veteran, ProgressionScale.levelToTier veteran)
    Assert.Equal(Master, ProgressionScale.levelToTier master)
    Assert.Equal(GrandMaster, ProgressionScale.levelToTier grandMaster)

  [<Fact>]
  let ``The level rule and the combatant rule agree on who can be ambushed at all`` () =
    // `cannotTouch` defines the tiers off the bare curve; `cedesInitiative` is what an ambush asks, and it
    // reads the combatant's *equipped* stats. Kit therefore moves the practical boundary to at or below the
    // canonical floor, never above it -- a Veteran is never touchable by a starting character.
    let veteran = ProgressionScale.tierToLevel Veteran

    let duelist level = TierFactory.createClassLevel CharacterClass.Fighter level

    Assert.True(ActionResolver.cedesInitiative (duelist veteran) (duelist 1))
    Assert.True(ActionResolver.cedesInitiative (duelist (veteran + 40)) (duelist 1))
    Assert.False(ActionResolver.cedesInitiative (duelist 1) (duelist 1), "equals always contest the Vor")
    Assert.False(ActionResolver.cedesInitiative (duelist 5) (duelist 1), "neighbours are still a live threat")

  [<Fact>]
  let ``Recovery action in Nach yields the Vor to a defender in the Nach`` () =
    // If an enemy collapsed or was knocked into the Nach and then takes a RecoveryAction,
    // a player waiting in the Nach must seize the Vor rather than being trapped in the Nach forever.
    let player =
      (TierFactory.createClassLevel CharacterClass.Fighter 1 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let foeInNach =
      (TierFactory.createClassLevel CharacterClass.Warrior 1)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let roller : DiceRoller = fun min _ -> min
    let res = ActionResolver.resolveExWithDefense roller (RecoveryAction SteadyForm) None foeInNach player 0

    Assert.True(res.Target.IsInVor, "the defender in the Nach must seize the Vor when the actor recovers")
    Assert.True(res.Actor.IsInNach, "the recovering actor must stay in the Nach")

  [<Fact>]
  let ``Floor 1 tutorial non-lethal resolution preserves player active defense and counters`` () =
    // On floor 1, resolveWithoutLethalWithDefense must respect player's active defense choice
    // and correctly evaluate counter affinities and tempo rather than dropping defOpt to None.
    let player =
      (TierFactory.createClassLevel CharacterClass.Fighter 1 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let foe =
      (TierFactory.createClassLevel CharacterClass.Warrior 1)
      |> fun c -> { c with Tempo = TacticalTiming.Vor }

    // Foe attacks with Power strike (ForceStrike), player defends with FortitudeGuard (Affinity counter against Power)
    let roller : DiceRoller = fun min _ -> min // Minimum rolls so defense easily holds
    let defChoice = Some(Guard(FortitudeGuard true))
    let attackIntent = StandardAttack(ForceStrike false)

    let res = ActionResolver.resolveWithoutLethalWithDefense roller attackIntent defChoice foe player 0

    Assert.True(res.Target.IsInVor, "player affinity counter on Floor 1 must seize the Vor")
    Assert.True(res.Actor.IsInNach, "countered foe on Floor 1 must yield the Vor")
    let hasCounterEvent =
      res.Events
      |> List.exists (function
        | CombatEvent.DefenseAffinityResolved(_, _, isCounter, _) -> isCounter
        | _ -> false)
    Assert.True(hasCounterEvent, "defense affinity counter event must be emitted on Floor 1")

  [<Fact>]
  let ``Whiffing inflicts status damage and surrenders the Vor to the defender`` () =
    let player =
      (TierFactory.createClassLevel CharacterClass.Fighter 1 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let foeInVor =
      (TierFactory.createClassLevel CharacterClass.Warrior 1)
      |> fun c -> { c with Tempo = TacticalTiming.Vor }

    // Attacker rolls lowest, defender rolls highest -> Whiff
    let roller : DiceRoller = fun min max -> if max = 6 then min else max
    let res = ActionResolver.resolveEx roller (StandardAttack(ForceStrike false)) foeInVor player 0

    // Attacker suffered status damage on physical whiff (Exhaustion and Frustration)
    Assert.True(res.Actor.Meters.Exhaustion.Value > 0, "whiffing physical strike must inflict Exhaustion")
    Assert.True(res.Actor.Meters.Frustration.Value > 0, "whiffing physical strike must inflict Frustration")

    // Vor is surrendered to defender
    Assert.True(res.Target.IsInVor, "defender in Nach must seize the Vor on attacker whiff")
    Assert.True(res.Actor.IsInNach, "attacker must yield the Vor on whiff")

    let tempoEvent =
      res.Events
      |> List.tryPick (function
        | CombatEvent.TempoShifted(newVor, oldVor, reason) -> Some(newVor, oldVor, reason)
        | _ -> None)
    Assert.True(tempoEvent.IsSome, "TempoShifted event must be emitted on whiff")
    let newVor, oldVor, _ = tempoEvent.Value
    Assert.Equal(player.Id, newVor)
    Assert.Equal(foeInVor.Id, oldVor)

  [<Fact>]
  let ``Strike dealing no damage due to full mitigation inflicts status damage and surrenders Vor`` () =
    let player =
      (TierFactory.createClassLevel CharacterClass.Fighter 1 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach; ArcaneWard = 5000 }

    let foeInVor =
      (TierFactory.createClassLevel CharacterClass.Warrior 5)
      |> fun c -> { c with Tempo = TacticalTiming.Vor }

    // Attacker lands strike cleanly, but ArcaneWard completely absorbs the damage
    let roller = fixedRoller 6
    let res = ActionResolver.resolveEx roller (StandardAttack(ForceStrike false)) foeInVor player 0

    // Check damage dealt to Health was 0
    let dmgEvent =
      res.Events
      |> List.tryPick (function CombatEvent.DamageApplied d -> Some d | _ -> None)
    Assert.True(dmgEvent.IsSome, sprintf "Events: %A" (res.Events |> List.map (fun e -> sprintf "%A" e)))
    Assert.Equal(0, dmgEvent.Value.Amount)

    // Attacker suffered status damage on 0-damage strike
    Assert.True(res.Actor.Meters.Exhaustion.Value >= 20, "absorbing all damage must inflict Exhaustion on attacker")
    Assert.True(res.Actor.Meters.Frustration.Value >= 15, "absorbing all damage must inflict Frustration on attacker")

    // Vor surrendered to defender
    Assert.True(res.Target.IsInVor, "defender must seize the Vor when incoming strike does no damage")
    Assert.True(res.Actor.IsInNach, "attacker must yield the Vor when strike does no damage")

    let tempoEvent =
      res.Events
      |> List.tryPick (function
        | CombatEvent.TempoShifted(newVor, oldVor, reason) -> Some(newVor, oldVor, reason)
        | _ -> None)
    Assert.True(tempoEvent.IsSome, "TempoShifted event must be emitted when damage is fully absorbed")
    let newVor, oldVor, _ = tempoEvent.Value
    Assert.Equal(player.Id, newVor)
    Assert.Equal(foeInVor.Id, oldVor)

  [<Fact>]
  let ``Mental strike dealing no damage inflicts mental status damage and surrenders Vor`` () =
    let player =
      (TierFactory.createClassLevel CharacterClass.Fighter 1 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach; ArcaneWard = 5000 }

    let foeInVor =
      (TierFactory.createClassLevel CharacterClass.Mage 5)
      |> fun c -> { c with Tempo = TacticalTiming.Vor }

    // Attacker rolls 6 (hits), defender rolls 1 (misses) so strike lands cleanly, but ward absorbs all damage
    let mutable rollCount = 0
    let roller : DiceRoller = fun _ _ ->
      rollCount <- rollCount + 1
      if rollCount <= 20 then 6 else 1
    let res = ActionResolver.resolveEx roller (StandardAttack(ArcaneCataclysm false)) foeInVor player 0

    let dmgEvent =
      res.Events
      |> List.tryPick (function CombatEvent.DamageApplied d -> Some d | _ -> None)
    Assert.True(dmgEvent.IsSome, "DamageApplied event must be emitted")
    Assert.Equal(0, dmgEvent.Value.Amount)

    // Attacker suffered mental status damage (CognitiveFatigue, Confusion, Frustration)
    Assert.True(res.Actor.Meters.CognitiveFatigue.Value >= 20, "fully absorbed mental strike must inflict CognitiveFatigue")
    Assert.True(res.Actor.Meters.Confusion.Value >= 15, "fully absorbed mental strike must inflict Confusion")

    // Vor surrendered to defender
    Assert.True(res.Target.IsInVor, "defender must seize Vor on fully absorbed mental strike")
    Assert.True(res.Actor.IsInNach, "attacker must yield Vor on fully absorbed mental strike")
