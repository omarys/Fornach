namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine

module ProlongedBattleTests =

  let fixedRoller (rollVal: int) : DiceRoller =
    fun _ _ -> rollVal

  let createFighter force fort finesse reflex prowess poise =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, force; Fortitude, fort
        Finesse, finesse; Reflex, reflex
        Prowess, prowess; Poise, poise
        Intellect, 10; Resolve, 10
        Acuity, 10; Intuition, 10
        Acumen, 10; Composure, 10
      ]
    Combatant.create id "TestFighter" 2000 2000 stats

  [<Fact>]
  let ``Force strike with high disparity inflicts greater Exhaustion than even match`` () =
    let roller = fixedRoller 6 // always hits
    let highDisparityAtk = createFighter 120 50 50 50 50 50
    let evenAtk = createFighter 50 50 50 50 50 50
    let defender = createFighter 50 50 50 50 50 50

    let resHigh = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) highDisparityAtk defender
    let resEven = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) evenAtk defender

    Assert.True(resHigh.Target.Meters.Exhaustion.Value > resEven.Target.Meters.Exhaustion.Value,
      sprintf "High disparity (%d) should inflict more exhaustion than even (%d)"
        resHigh.Target.Meters.Exhaustion.Value resEven.Target.Meters.Exhaustion.Value)

  [<Fact>]
  let ``Heavy Power damage can degrade target weapon condition`` () =
    // With a roll of 1 on d100, passives always proc
    let roller = fixedRoller 1
    let attacker = createFighter 200 50 50 50 50 50
    let defender = createFighter 50 50 50 50 50 50

    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike true)) attacker defender
    Assert.Equal(WeaponCondition.Notched, res.Target.WeaponCondition)

  [<Fact>]
  let ``Degraded weapon condition reduces outgoing physical strike damage`` () =
    let pristineFighter = createFighter 140 50 50 50 50 50
    let brokenFighter = { pristineFighter with WeaponCondition = WeaponCondition.Broken }
    let defender = createFighter 50 50 50 50 50 50
    let roller = fixedRoller 5

    let resPristine = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) pristineFighter defender
    let resBroken = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) brokenFighter defender

    let dmgPristine = defender.Health.Current - resPristine.Target.Health.Current
    let dmgBroken = defender.Health.Current - resBroken.Target.Health.Current

    Assert.True(dmgBroken < dmgPristine,
      sprintf "Broken weapon damage (%d) should be less than pristine (%d)" dmgBroken dmgPristine)

  [<Fact>]
  let ``Finesse strikes with high disparity inflict faster Overwhelm`` () =
    let roller = fixedRoller 5
    let swiftAtk = createFighter 50 50 130 50 50 50
    let slowAtk = createFighter 50 50 50 50 50 50
    let defender = createFighter 50 50 50 50 50 50

    let resSwift = ActionResolver.resolve roller (StandardAttack (FinesseCadence false)) swiftAtk defender
    let resSlow = ActionResolver.resolve roller (StandardAttack (FinesseCadence false)) slowAtk defender

    Assert.True(resSwift.Target.Meters.Overwhelm.Value > resSlow.Target.Meters.Overwhelm.Value,
      sprintf "Swift overwhelm (%d) should exceed slow overwhelm (%d)"
        resSwift.Target.Meters.Overwhelm.Value resSlow.Target.Meters.Overwhelm.Value)

  [<Fact>]
  let ``Agility critical strikes apply stacking bleed and upkeep deals damage`` () =
    let roller = fixedRoller 1 // triggers vital opening passives
    let fencer = { (createFighter 50 50 120 50 50 50) with
                    Stance = CombatStance.AgilityStance
                    ComboTracker = { ConsecutiveHits = 3; ConsecutivePowerHits = 0; VitalOpeningBonus = 50 } }
    let target = createFighter 50 50 50 40 50 50

    let res = ActionResolver.resolve roller (StandardAttack (FinesseCadence false)) fencer target
    Assert.True(res.Target.BleedStacks >= 2, "Target should have accumulated bleed stacks")

    // Bleed upkeep
    let postBleed, events = ActionResolver.applyTurnUpkeep res.Target
    Assert.True(postBleed.Health.Current < res.Target.Health.Current, "Bleed upkeep should deal damage")
    Assert.Equal(res.Target.BleedStacks - 1, postBleed.BleedStacks)

  [<Fact>]
  let ``Prowess strikes with high disparity inflict faster Frustration`` () =
    let roller = fixedRoller 5
    let master = createFighter 50 50 50 50 140 50
    let novice = createFighter 50 50 50 50 50 50
    let target = createFighter 50 50 50 50 50 50

    let resMaster = ActionResolver.resolve roller (StandardAttack (ProwessStrike false)) master target
    let resNovice = ActionResolver.resolve roller (StandardAttack (ProwessStrike false)) novice target

    Assert.True(resMaster.Target.Meters.Frustration.Value > resNovice.Target.Meters.Frustration.Value,
      sprintf "Master frustration (%d) should exceed novice frustration (%d)"
        resMaster.Target.Meters.Frustration.Value resNovice.Target.Meters.Frustration.Value)

  [<Fact>]
  let ``Accrued Study Stacks passively trigger Riposte without consuming stacks`` () =
    // Roller always rolls 1: counter roll <= counterChance
    let roller = fixedRoller 1
    let attacker = createFighter 80 80 80 80 80 80
    // With 2 study stacks, defender cannot meet disarm requirement (min 3), so triggers Riposte
    let defender =
      { (createFighter 80 80 80 80 120 100) with
          Stance = CombatStance.DisciplineStance
          StudyStacks = 2 }

    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) attacker defender

    // Check that defender's study stacks were NOT consumed
    Assert.Equal(2, res.Target.StudyStacks)

    // Check that Riposte event was emitted and attacker suffered counter damage
    let hasRiposte = res.Events |> List.exists (function CombatEvent.RiposteExecuted _ -> true | _ -> false)
    Assert.True(hasRiposte, "Discipline defender with study stacks should trigger reactive Riposte")
    Assert.True(res.Actor.Health.Current < attacker.Health.Current, "Attacker should have taken counter damage from Riposte")

  [<Fact>]
  let ``Accrued Study Stacks passively trigger Disarm when conditions and stacks are met`` () =
    let roller = fixedRoller 1
    let attacker = createFighter 80 80 80 80 80 80
    // With 5 study stacks and prowess 120 vs poise 80, defender meets disarm requirement
    let defender =
      { (createFighter 80 80 80 80 120 100) with
          Stance = CombatStance.DisciplineStance
          StudyStacks = 5 }

    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) attacker defender

    // Study stacks not consumed
    Assert.Equal(5, res.Target.StudyStacks)

    // Check that Disarm event was emitted and attacker weapon degraded
    let hasDisarm = res.Events |> List.exists (function CombatEvent.DisarmExecuted _ -> true | _ -> false)
    Assert.True(hasDisarm, "Discipline defender with high study stacks should trigger reactive Disarm")
    Assert.Equal(WeaponCondition.Notched, res.Actor.WeaponCondition)

  [<Fact>]
  let ``Low Prowess combatant cannot disarm an opponent with superior Poise`` () =
    let roller = fixedRoller 5
    let weakAttacker =
      { (createFighter 50 50 50 50 30 50) with
          StudyStacks = 10 }
    let strongDefender = createFighter 50 50 50 50 50 120

    let res = ActionResolver.resolve roller (StandardAttack (MasterfulDisarm 5)) weakAttacker strongDefender

    // Disarm should fail because attacker Prowess (30) < 75% of defender Poise (120)
    Assert.Equal(WeaponCondition.Pristine, res.Target.WeaponCondition)
    let hasDisarmFail =
      res.Events |> List.exists (function
        | CombatEvent.DisarmExecuted(_, _, reason) when reason.Contains("too commanding") -> true
        | _ -> false)
    Assert.True(hasDisarmFail, "Disarm should report failure due to commanding Poise")

  [<Fact>]
  let ``Discipline gambits consume Study Stacks with zero Recklessness cost`` () =
    let roller = fixedRoller 5
    let duelist =
      { (createFighter 50 50 50 50 120 100) with
          StudyStacks = 6 }
    let target = createFighter 50 50 50 50 50 80

    let startReck = duelist.Meters.Recklessness.Value
    let res = ActionResolver.resolve roller (StandardAttack (CalculatedFlawStrike 3)) duelist target

    // Stacks: 6 started - 3 spent + 2 generated on hit = 5
    Assert.Equal(5, res.Actor.StudyStacks)
    // Recklessness should NOT have spiked from the gambit
    Assert.Equal(startReck, res.Actor.Meters.Recklessness.Value)

  [<Fact>]
  let ``Encirclement penalty compounds with successive defenses based on disparity`` () =
    // Equal fighters (50 vs 50) have strict quadratic acceleration
    let p0 = DicePool.computeEncirclementPenalty 50 50 0
    let p1 = DicePool.computeEncirclementPenalty 50 50 1
    let p2 = DicePool.computeEncirclementPenalty 50 50 2
    let p3 = DicePool.computeEncirclementPenalty 50 50 3
    let p4 = DicePool.computeEncirclementPenalty 50 50 4

    Assert.Equal(0, p0)
    Assert.Equal(4, p1)
    Assert.Equal(10, p2)
    Assert.Equal(21, p3)
    Assert.Equal(35, p4)


    // Higher prior defenses must strictly increase penalty with accelerating growth
    Assert.True(p4 - p3 > p3 - p2)
    Assert.True(p3 - p2 > p2 - p1)
    Assert.True(p2 - p1 > p1 - p0)

  [<Fact>]
  let ``High stat disparity heavily mitigates encirclement penalty for Grandmaster`` () =
    let noviceAtk = 40
    let adeptDef = 110
    let grandmasterDef = 240

    // Against 4th prior defense (5th attacker)
    let adeptPen = DicePool.computeEncirclementPenalty noviceAtk adeptDef 4
    let gmPen = DicePool.computeEncirclementPenalty noviceAtk grandmasterDef 4

    Assert.True(gmPen < adeptPen, sprintf "Grandmaster penalty (%d) must be significantly lower than Adept penalty (%d)" gmPen adeptPen)
    Assert.Equal(1, gmPen)
    Assert.Equal(5, adeptPen)

  [<Fact>]
  let ``ActionResolver with priorDefenses emits EncirclementPenalized event and landed strikes inflate Overwhelm`` () =
    let roller = fixedRoller 6 // always hits
    let attacker = createFighter 110 110 110 110 110 110
    let defender = createFighter 110 110 110 110 110 110

    let res = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) attacker defender 3

    let encOpt =
      res.Events
      |> List.tryPick (function
        | CombatEvent.EncirclementPenalized(dId, prior, pen) -> Some(dId, prior, pen)
        | _ -> None)

    Assert.True(encOpt.IsSome, "EncirclementPenalized event must be recorded")
    let _, prior, pen = encOpt.Value
    Assert.Equal(3, prior)
    Assert.True(pen > 0, "Penalty hits must be positive")
    Assert.True(res.Target.Meters.Overwhelm.Value > 0, "Landed strike under surround pressure should increase Overwhelm meter")

  [<Fact>]
  let ``Defended attacks under encirclement do NOT inflate defender Overwhelm on whiffs`` () =
    let roller = fixedRoller 4 // whiffs against high defense
    let attacker = createFighter 40 40 40 40 40 40
    // Pure Fortitude defender with low agility/discipline (no AoO triggered)
    let defender = createFighter 40 110 40 40 40 40

    let res = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) attacker defender 3

    Assert.True(res.Contest.IsSome && res.Contest.Value.IsWhiff, "Attack should be cleanly deflected/whiffed")
    Assert.Equal(0, res.Target.Meters.Overwhelm.Value)

  [<Fact>]
  let ``Agile or Disciplined defender triggers Attack of Opportunity on flank attempt and defuses strike`` () =
    let roller = fixedRoller 1 // triggers AoO and passives
    let noviceFlanker = createFighter 40 40 30 30 30 30
    let fencerDefender = createFighter 50 50 140 130 90 85

    let res = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) noviceFlanker fencerDefender 2

    let aooOpt =
      res.Events
      |> List.tryPick (function
        | CombatEvent.AttackOfOpportunityTriggered(dId, aId, vec, dmg, disrupted) -> Some(dId, aId, vec, dmg, disrupted)
        | _ -> None)

    Assert.True(aooOpt.IsSome, "Attack of Opportunity must be triggered against flanker")
    let _, _, vec, dmg, disrupted = aooOpt.Value
    Assert.Equal("Agility", vec)
    Assert.True(dmg > 50, sprintf "AoO damage (%d) should be substantial" dmg)
    Assert.True(disrupted, "Disrupted strike should defuse incoming attack")
    Assert.True(res.Contest.IsNone, "Attack was defused before contest resolution")

  [<Fact>]
  let ``Power-only combatant does NOT trigger Attack of Opportunity`` () =
    let roller = fixedRoller 1
    let attacker = createFighter 40 40 50 50 50 50
    // High Force & Fortitude, but lower Finesse and Prowess than attacker
    let brute = createFighter 200 200 40 40 40 40

    let res = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) attacker brute 2

    let hasAoO =
      res.Events
      |> List.exists (function CombatEvent.AttackOfOpportunityTriggered _ -> true | _ -> false)

    Assert.False(hasAoO, "Power combatant with low Agility/Discipline should not trigger Attack of Opportunity")


  [<Fact>]
  let ``Grandmaster holds guard against 3-5 novices while Adept defense breaks at 4-5`` () =
    let roller = fixedRoller 4
    let noviceAtk = 45
    let adeptDef = 110
    let grandmasterDef = 240

    // Attack #5 (priorDefenses = 4)
    let adeptContest = DicePool.resolveContestEx roller Power noviceAtk 0 adeptDef 0 4
    let gmContest = DicePool.resolveContestEx roller Power noviceAtk 0 grandmasterDef 0 4

    // Adept defense should be compromised against 5th novice attack (not a whiff)
    Assert.False(adeptContest.IsWhiff, "Adept should struggle / get hit by 5th novice attacker")
    // Grandmaster must still completely whiff the 5th novice attacker
    Assert.True(gmContest.IsWhiff, "Grandmaster must easily deflect 5th novice attacker")

  [<Fact>]
  let ``Novice Force strike on Grandmaster causes zero strike exhaustion`` () =
    let roller = fixedRoller 6 // forced hit
    let novice = createFighter 45 40 40 40 40 40
    let grandmaster = createFighter 200 320 200 200 200 200

    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) novice grandmaster
    // Disparity is negative (-275), so strike itself adds 0 exhaustion
    Assert.Equal(0, res.Target.Meters.Exhaustion.Value)

  [<Fact>]
  let ``Prolonged combat rounds accumulate natural fatigue and SteadyForm recovers stamina`` () =
    let fighter = createFighter 100 100 100 100 100 100
    Assert.Equal(0, fighter.Meters.Exhaustion.Value)

    // 3 rounds of active upkeep
    let f1, _ = ActionResolver.applyTurnUpkeep fighter
    let f2, _ = ActionResolver.applyTurnUpkeep f1
    let f3, _ = ActionResolver.applyTurnUpkeep f2

    Assert.Equal(3, f3.Meters.Exhaustion.Value)

    // SteadyForm clears accumulated fatigue
    let res = ActionResolver.resolve (fixedRoller 1) (RecoveryAction SteadyForm) f3 fighter
    Assert.Equal(0, res.Actor.Meters.Exhaustion.Value)

  [<Fact>]
  let ``Defensive penalties from encirclement and fatigue degrade Attack of Opportunity trigger chance`` () =
    let roller = fixedRoller 60 // mid-high roll (60 on d100)
    let flanker = createFighter 45 40 30 30 30 30
    let fencer = createFighter 50 50 140 130 90 85

    // Case 1: Fresh defender on initial flank (priorDefenses = 1, 0 exhaustion)
    // Base chance is ~77%, penalty is ~5%, effective chance ~72% >= 60 -> Triggers!
    let resFresh = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) flanker fencer 1
    let freshAoO =
      resFresh.Events
      |> List.exists (function CombatEvent.AttackOfOpportunityTriggered _ -> true | _ -> false)
    Assert.True(freshAoO, "Fresh defender should trigger AoO on a roll of 60")

    // Case 2: Heavily burdened defender (priorDefenses = 5, Exhaustion = 60)
    // Encirclement penalty and fatigue degrade effective chance well below 60 -> Fails to trigger!
    let burdenedFencer =
      fencer
      |> Combatant.updateMeters (fun m -> { m with Exhaustion = Meter.Create 60 })

    let resBurdened = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) flanker burdenedFencer 5
    let burdenedAoO =
      resBurdened.Events
      |> List.exists (function CombatEvent.AttackOfOpportunityTriggered _ -> true | _ -> false)
    Assert.False(burdenedAoO, "Burdened defender with encirclement and fatigue penalties should NOT trigger AoO on roll of 60")

  [<Fact>]
  let ``Power Stance Cleave damages adjacent targets and scales Recklessness by stat disparity`` () =
    let roller = fixedRoller 6 // always lands penetrating hit
    let gmAttacker = { createFighter 350 320 180 200 300 280 with Stance = CombatStance.PowerStance }
    let noviceA = createFighter 45 40 25 25 30 30
    let noviceB = createFighter 45 40 25 25 30 30
    let noviceC = createFighter 45 40 25 25 30 30

    let groupResGM =
      ActionResolver.resolveGroupTurn roller (StandardAttack (ForceStrike false)) gmAttacker noviceA [ noviceB; noviceC ]

    // Cleave should trigger on 2 adjacent targets
    let cleaveEventsGM =
      groupResGM.Events
      |> List.filter (function CombatEvent.CleaveExecuted _ -> true | _ -> false)
    Assert.Equal(2, cleaveEventsGM.Length)

    // Both adjacent targets took damage
    Assert.True(groupResGM.SecondaryTargets.[0].Health.Current < noviceB.Health.Current)
    Assert.True(groupResGM.SecondaryTargets.[1].Health.Current < noviceC.Health.Current)

    // Grandmaster Force disparity (40 / 350) yields minimal Recklessness spike (3 per cleave)
    // Primary standard attack gives 0 Recklessness, plus 2 cleaves * 3 = 6
    Assert.Equal(6, groupResGM.Actor.Meters.Recklessness.Value)

    // Compare with low disparity attacker (e.g. Force 50 vs Fortitude 40 -> 40/50 = 0.8 -> ~20 recklessness per cleave)
    let lowDispAttacker = { createFighter 50 50 50 50 50 50 with Stance = CombatStance.PowerStance }
    let groupResLow =
      ActionResolver.resolveGroupTurn roller (StandardAttack (ForceStrike false)) lowDispAttacker noviceA [ noviceB; noviceC ]
    Assert.True(groupResLow.Actor.Meters.Recklessness.Value > groupResGM.Actor.Meters.Recklessness.Value,
      sprintf "Low disparity (%d) should incur much more Recklessness than Grandmaster disparity (%d)"
        groupResLow.Actor.Meters.Recklessness.Value groupResGM.Actor.Meters.Recklessness.Value)

  [<Fact>]
  let ``Discipline Stance Chaining flows across engaged opponents with zero recklessness and accrues Study Stacks`` () =
    let roller = fixedRoller 6 // always lands penetrating hit
    let master =
      { createFighter 180 200 180 200 300 280 with
          Stance = CombatStance.DisciplineStance
          StudyStacks = 2 } // With 2 study stacks, capacity = 1 + (2/2) = 2 chained targets
    let target1 = createFighter 45 40 25 25 30 30
    let target2 = createFighter 45 40 25 25 30 30
    let target3 = createFighter 45 40 25 25 30 30

    let groupRes =
      ActionResolver.resolveGroupTurn roller (StandardAttack (ProwessStrike false)) master target1 [ target2; target3 ]

    let chainEvents =
      groupRes.Events
      |> List.filter (function CombatEvent.StrikeChained _ -> true | _ -> false)
    Assert.Equal(2, chainEvents.Length)

    // Both chained targets took damage
    Assert.True(groupRes.SecondaryTargets.[0].Health.Current < target2.Health.Current)
    Assert.True(groupRes.SecondaryTargets.[1].Health.Current < target3.Health.Current)

    // Chaining incurred 0 extra Recklessness (disciplined martial economy)
    Assert.Equal(0, groupRes.Actor.Meters.Recklessness.Value)

    // Each chain hit accrued +1 Study Stack (primary +2 from discipline stance + 2 chain hits = 6 total)
    Assert.True(groupRes.Actor.StudyStacks >= 5, sprintf "Expected >= 5 study stacks, got %d" groupRes.Actor.StudyStacks)

  [<Fact>]
  let ``Agility / Finesse Stance strictly duels single target with zero cleave or chain`` () =
    let roller = fixedRoller 6
    let fencer = { createFighter 60 70 140 130 95 85 with Stance = CombatStance.AgilityStance }
    let primary = createFighter 45 40 25 25 30 30
    let secondary = createFighter 45 40 25 25 30 30

    let res = ActionResolver.resolveGroupTurn roller (StandardAttack (FinesseCadence false)) fencer primary [ secondary ]

    // No cleaves or chains
    let cleaves = res.Events |> List.filter (function CombatEvent.CleaveExecuted _ -> true | _ -> false)
    let chains = res.Events |> List.filter (function CombatEvent.StrikeChained _ -> true | _ -> false)
    Assert.Empty(cleaves)
    Assert.Empty(chains)

    // Secondary target HP unchanged
    Assert.Equal(secondary.Health.Current, res.SecondaryTargets.[0].Health.Current)

  [<Fact>]
  let ``Whiffed strike prevents both cleaving and chaining`` () =
    let roller = fixedRoller 1 // low roll -> whiff
    let powerFighter = { createFighter 50 50 50 50 50 50 with Stance = CombatStance.PowerStance }
    let primary = createFighter 200 200 200 200 200 200
    let secondary = createFighter 45 40 25 25 30 30

    let res = ActionResolver.resolveGroupTurn roller (StandardAttack (ForceStrike false)) powerFighter primary [ secondary ]
    let cleaves = res.Events |> List.filter (function CombatEvent.CleaveExecuted _ -> true | _ -> false)
    Assert.Empty(cleaves)
    Assert.Equal(secondary.Health.Current, res.SecondaryTargets.[0].Health.Current)

  [<Fact>]
  let ``AI facing multiple opponents shifts to Discipline Stance if Prowess is viable`` () =
    // Grandmaster has Force 350, Prowess 300. In 1v1 duel, he uses PowerStance.
    let grandmaster = { createFighter 350 320 180 200 300 280 with Stance = CombatStance.PowerStance }
    let opponent = createFighter 45 40 25 25 30 30

    // 1-on-1 duel: stays in Power Stance (highest stat is Force)
    let duelIntent = Fornach.Cli.AI.chooseIntent grandmaster opponent
    match duelIntent with
    | StandardAttack (ForceStrike _) -> ()
    | other -> Assert.Fail(sprintf "Expected ForceStrike in 1v1 duel, got %A" other)

    // Outnumbered (5 opponents): AI recognizes swarm and shifts to Discipline Stance!
    let swarmIntent = Fornach.Cli.AI.chooseIntentWithContext grandmaster opponent 5
    match swarmIntent with
    | ShiftStance CombatStance.DisciplineStance -> ()
    | other -> Assert.Fail(sprintf "Expected ShiftStance DisciplineStance when outnumbered, got %A" other)

