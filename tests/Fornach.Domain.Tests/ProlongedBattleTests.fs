namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine

module ProlongedBattleTests =

  let fixedRoller (rollVal: int) : DiceRoller =
    fun _ _ -> rollVal

  let private fighterStats force fort finesse reflex prowess poise =
    StatBlock.Create [
      Force, force; Fortitude, fort
      Finesse, finesse; Reflex, reflex
      Prowess, prowess; Poise, poise
      Intellect, force; Resolve, fort
      Acuity, finesse; Intuition, reflex
      Acumen, prowess; Composure, poise
    ]

  let createFighter force fort finesse reflex prowess poise =
    Combatant.create (CombatantId.New()) "TestFighter" 2000 2000 (fighterStats force fort finesse reflex prowess poise)

  /// Same stat spread at an explicit progression level — the Attack of Opportunity budget scales
  /// with level, so AoO tests need a leveled defender.
  let createFighterAtLevel level force fort finesse reflex prowess poise =
    Combatant.createWithClass (CombatantId.New()) "TestFighter" 2000 2000
      (fighterStats force fort finesse reflex prowess poise) CharacterClass.Warrior level

  [<Fact>]
  let ``a disarm holding exactly the required stacks is not reported as failed`` () =
    let roller = fixedRoller 6
    let actor = createFighter 50 50 50 50 400 50
    let target = createFighter 50 50 50 50 50 300
    let probe = MasterfulDisarm 3
    let off = actor.GetStat Prowess
    let def = target.GetGuard(probe.Plane, false)
    let required = Math.Max(3, int (Math.Ceiling((float def / Math.Max(1.0, float off)) * 3.5)))

    // The drain happens in step A; the requirement must be checked against the pre-drain count.
    let armed = actor |> Combatant.addStudyStacks required
    let result = ActionResolver.resolve roller (StandardAttack (MasterfulDisarm required)) armed target

    let reportedFailed =
      result.Events
      |> List.exists (function
        | CombatEvent.DisarmExecuted (_, _, msg) -> msg.Contains "failed"
        | _ -> false)

    Assert.False(reportedFailed, sprintf "disarm with exactly %d stacks must not drain and then fail" required)

  [<Fact>]
  let ``mastery raises damage dealt and lowers damage taken against that species`` () =
    let roller = fixedRoller 6
    let species = "Blight Sprite"

    let asPlayer kills =
      let c = createFighter 120 50 50 50 50 50
      { c with
          Name = "TestFighter"
          PlayerStyle = true
          Progression = { c.Progression with Mastery = Map.ofList [ species, kills ] } }

    let damageBetween (attacker: Combatant) (target: Combatant) =
      let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) attacker target
      target.Health.Current - res.Target.Health.Current

    let sprite = { createFighter 50 50 50 50 50 50 with Name = species }

    // Offense: five wins against the species hit harder than none.
    let plainHit = damageBetween (asPlayer 0) sprite
    let masteredHit = damageBetween (asPlayer 5) sprite
    Assert.True(masteredHit > plainHit, sprintf "mastered hit %d should exceed %d" masteredHit plainHit)

    // The bonus is capped at ten kills.
    Assert.Equal(damageBetween (asPlayer 10) sprite, damageBetween (asPlayer 50) sprite)

    // Defense: the same species hurts a player who has beaten it less than one who has not.
    let attacker = { createFighter 120 50 50 50 50 50 with Name = species }
    let takenByPlain = damageBetween attacker (asPlayer 0)
    let takenByMastered = damageBetween attacker (asPlayer 10)
    Assert.True(takenByMastered < takenByPlain, sprintf "mastered defence %d should undercut %d" takenByMastered takenByPlain)

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
  let ``Finesse strikes with high disparity inflict faster Confusion`` () =
    let roller = fixedRoller 5
    let swiftAtk = createFighter 50 50 130 50 50 50
    let slowAtk = createFighter 50 50 50 50 50 50
    let defender = createFighter 50 50 50 50 50 50

    let resSwift = ActionResolver.resolve roller (StandardAttack (FinesseCadence false)) swiftAtk defender
    let resSlow = ActionResolver.resolve roller (StandardAttack (FinesseCadence false)) slowAtk defender

    Assert.True(resSwift.Target.Meters.Confusion.Value > resSlow.Target.Meters.Confusion.Value,
      sprintf "Swift confusion (%d) should exceed slow confusion (%d)"
        resSwift.Target.Meters.Confusion.Value resSlow.Target.Meters.Confusion.Value)

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
    let res = ActionResolver.resolve roller (StandardAttack CalculatedFlawStrike) duelist target

    // Stacks: the whole pile of 6 is spent, +2 generated on hit
    Assert.Equal(2, res.Actor.StudyStacks)    // Recklessness should NOT have spiked from the gambit
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
    let fencerDefender = createFighterAtLevel 100 50 50 140 130 90 85

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
    let brute = createFighterAtLevel 100 200 200 40 40 40 40

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
    let fencer = createFighterAtLevel 100 50 50 140 130 90 85

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
  let ``Attack of opportunity reads its vector from stat disparity rather than the shifting stance`` () =
    // Mid-high roll of 60 on d100
    let roller = fixedRoller 60
    let flanker = createFighter 45 40 30 30 30 30
    let aooVector (res: ActionResult) =
      res.Events
      |> List.tryPick (function CombatEvent.AttackOfOpportunityTriggered (_, _, vec, _, _) -> Some vec | _ -> None)

    // Disparity is 140 vs 30 either way; the stance is deliberately mismatched with it, because
    // stances now shift automatically with whatever was last attacked with.
    let disciplineDominant = { createFighterAtLevel 100 50 50 90 85 140 130 with Stance = CombatStance.AgilityStance }
    let agilityDominant = { createFighterAtLevel 100 50 50 140 130 90 85 with Stance = CombatStance.DisciplineStance }

    // A Prowess-dominant defender in an Agility stance still ripostes with Discipline, and is not
    // taxed for the mismatch (the old Agility-stance tunnel-vision penalty is gone).
    let resDiscipline = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) flanker disciplineDominant 1
    Assert.Equal(Some "Discipline", aooVector resDiscipline)

    // The mirror case: Finesse-dominant in a Discipline stance ripostes with Agility.
    let resAgility = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) flanker agilityDominant 1
    Assert.Equal(Some "Agility", aooVector resAgility)

  [<Fact>]
  let ``Attack of opportunity capacity is one per ten levels after 20 and caps at eight`` () =
    let capacity level = (createFighterAtLevel level 50 50 50 50 50 50).AttackOfOpportunityCapacity

    Assert.Equal(0, capacity 1)
    Assert.Equal(0, capacity 20)
    Assert.Equal(1, capacity 30)
    Assert.Equal(4, capacity 60)
    Assert.Equal(7, capacity 90)
    Assert.Equal(8, capacity 100)
    Assert.Equal(8, capacity 200)

  [<Fact>]
  let ``Attack of opportunity budget is spent one flanker at a time and stops at capacity`` () =
    let roller = fixedRoller 1 // always triggers while the budget allows
    let flanker = createFighter 40 40 30 30 30 30
    // Level 40 -> capacity 2
    let defender = createFighterAtLevel 40 50 320 140 130 90 85

    let aoo (priorDefenses: int) =
      ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) flanker defender priorDefenses
      |> fun res ->
        res.Events
        |> List.filter (function CombatEvent.AttackOfOpportunityTriggered _ -> true | _ -> false)
        |> List.length

    Assert.Equal(1, aoo 1)
    Assert.Equal(1, aoo 2)
    Assert.Equal(0, aoo 3) // capacity reached, remaining flankers strike unanswered

  [<Fact>]
  let ``Fully surrounded level 100 defender answers eight flankers and no more`` () =
    let roller = fixedRoller 1
    let flanker = createFighter 40 40 30 30 30 30
    let grandmaster = createFighterAtLevel 100 50 320 140 130 90 85

    let aoo (priorDefenses: int) =
      ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) flanker grandmaster priorDefenses
      |> fun res ->
        res.Events
        |> List.filter (function CombatEvent.AttackOfOpportunityTriggered _ -> true | _ -> false)
        |> List.length

    Assert.Equal(1, aoo 1)
    Assert.Equal(1, aoo 8) // the eighth adjoining tile is still answered
    Assert.Equal(0, aoo 9) // a ninth attacker is beyond the eight tiles

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
    let grandmaster = { createFighter 350 320 180 200 300 280 with Stance = CombatStance.PowerStance; PlayerStyle = true }
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

  // =========================================================================
  // Arcane Magic Tests
  // =========================================================================

  let createMage (intellect: int) (resolve: int) (acuity: int) (intuition: int) (acumen: int) (composure: int) =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 20; Fortitude, 20
        Finesse, 20; Reflex, 20
        Prowess, 20; Poise, 20
        Intellect, intellect; Resolve, resolve
        Acuity, acuity; Intuition, intuition
        Acumen, acumen; Composure, composure
      ]
    Combatant.create id "TestMage" 2000 2000 stats

  [<Fact>]
  let ``Power / Intellect magic user deals high Cataclysm damage and splashes to swarm in group turn`` () =
    let roller = fixedRoller 6 // always hits
    let intellectMage = createMage 150 100 30 30 30 30
    let primaryTarget = createMage 30 30 30 30 30 30
    let secA = createMage 30 30 30 30 30 30
    let secB = createMage 30 30 30 30 30 30

    let groupRes =
      ActionResolver.resolveGroupTurn roller (StandardAttack (ArcaneCataclysm false)) intellectMage primaryTarget [ secA; secB ]

    // Primary target took damage
    Assert.True(groupRes.PrimaryTarget.Morale.Current < primaryTarget.Morale.Current)

    // Cataclysm splashes to adjacent targets
    let splashEvents =
      groupRes.Events
      |> List.filter (function CombatEvent.CataclysmSplashed _ -> true | _ -> false)
    Assert.Equal(2, splashEvents.Length)
    Assert.True(groupRes.SecondaryTargets.[0].Morale.Current < secA.Morale.Current)
    Assert.True(groupRes.SecondaryTargets.[1].Morale.Current < secB.Morale.Current)

  [<Fact>]
  let ``Off-specialization arcane spellcasting incurs Cognitive Fatigue strain and scales down potency`` () =
    let roller = fixedRoller 6
    // Intellect specialist (Intellect 150, Acuity 30 -> 20% proficiency in Guile/Illusion)
    let intellectMage = createMage 150 100 30 30 30 30
    let target = createMage 30 30 30 30 30 30

    // Casting off-school spell: MirrorIllusion (Agility vector)
    let resOff = ActionResolver.resolve roller (StandardAttack (MirrorIllusion false)) intellectMage target

    // Should incur ArcaneStrainIncurred event
    let strainEvt =
      resOff.Events
      |> List.tryFind (function CombatEvent.ArcaneStrainIncurred _ -> true | _ -> false)
    Assert.True(strainEvt.IsSome, "Off-school spell cast should incur ArcaneStrainIncurred event")

    // Caster incurs Cognitive Fatigue from mental strain
    Assert.True(resOff.Actor.Meters.CognitiveFatigue.Value > 0, "Actor should suffer Cognitive Fatigue from off-school strain")

  [<Fact>]
  let ``Agility / Acuity magic user weaves Mirror Clones that intercept and defuse incoming attacks`` () =
    let roller = fixedRoller 6
    let acuityMage = createMage 40 40 150 100 40 40
    let target = createMage 30 30 30 30 30 30

    // 1. Weave Mirror Illusion
    let resWeave = ActionResolver.resolve roller (StandardAttack (MirrorIllusion false)) acuityMage target
    Assert.True(resWeave.Actor.MirrorClones > 0, "Casting Mirror Illusion should conjure mirror clones")

    let conjuredEvt =
      resWeave.Events
      |> List.tryFind (function CombatEvent.MirrorClonesConjured _ -> true | _ -> false)
    Assert.True(conjuredEvt.IsSome, "MirrorClonesConjured event should be emitted")

    // 2. Incoming enemy attack is intercepted by mirror decoy
    let cloneProtectedMage = resWeave.Actor
    let attacker = { createFighter 100 50 50 50 50 50 with ComboTracker = ConsecutiveComboTracker.Zero.RegisterHit().RegisterHit() }
    let deceiveRoller = fixedRoller 30 // low roll <= deceiveChance -> clone deceives attacker

    let resDefend = ActionResolver.resolve deceiveRoller (StandardAttack (ForceStrike false)) attacker cloneProtectedMage

    // Attack was defused: clone destroyed, 0 damage to defender, attacker combo reset
    let decoyEvt =
      resDefend.Events
      |> List.tryFind (function CombatEvent.MirrorCloneDecoyed _ -> true | _ -> false)
    Assert.True(decoyEvt.IsSome, "Incoming attack should be intercepted by MirrorCloneDecoyed")
    Assert.Equal(cloneProtectedMage.MirrorClones - 1, resDefend.Target.MirrorClones)
    Assert.Equal(cloneProtectedMage.Health.Current, resDefend.Target.Health.Current)
    Assert.Equal(0, resDefend.Actor.ComboTracker.ConsecutiveHits)

  [<Fact>]
  let ``Discipline / Acumen magic user erects Arcane Ward that absorbs incoming damage and restores via CenterMind`` () =
    let roller = fixedRoller 6
    let acumenMage = createMage 40 40 40 40 150 100
    let target = createMage 30 30 30 30 30 30

    // 1. Erect Arcane Ward via RunicWardTrap
    let resWard = ActionResolver.resolve roller (StandardAttack (RunicWardTrap false)) acumenMage target
    Assert.True(resWard.Actor.ArcaneWard > 0, "Runic Ward Trap should erect an Arcane Ward barrier")

    // Reinforce ward to test absorption of full strike
    let strongWardedMage = Combatant.addWard 2000 resWard.Actor
    let initialWard = strongWardedMage.ArcaneWard
    let initialHealth = strongWardedMage.Health.Current

    // 2. Incoming physical attack hits ward first
    let attacker = createFighter 40 30 30 30 30 30
    let resHit = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) attacker strongWardedMage

    let absorbEvt =
      resHit.Events
      |> List.tryFind (function CombatEvent.ArcaneWardAbsorbed _ -> true | _ -> false)
    Assert.True(absorbEvt.IsSome, "ArcaneWardAbsorbed event should be emitted when damage hits ward")
    Assert.True(resHit.Target.ArcaneWard < initialWard, "Arcane Ward should be reduced by absorbed damage")
    Assert.Equal(initialHealth, resHit.Target.Health.Current)

    // 3. CenterMind restores Arcane Ward scaled by Acumen
    let depletedMage = { resHit.Target with ArcaneWard = 0 }
    let resRecovery = ActionResolver.resolve roller (RecoveryAction CenterMind) depletedMage target
    Assert.True(resRecovery.Actor.ArcaneWard > 0, "CenterMind should restore Arcane Ward for Discipline mage")

  [<Fact>]
  let ``Discipline / Acumen Disorienting Shockwave resets opponent combo tempo and pulses in group turn`` () =
    let roller = fixedRoller 6
    let acumenMage = createMage 40 40 40 40 150 100
    let primaryEnemy =
      { createFighter 40 30 30 30 30 30 with
          ComboTracker = ConsecutiveComboTracker.Zero.RegisterHit().RegisterHit().RegisterHit() }
    let secA =
      { createFighter 40 30 30 30 30 30 with
          ComboTracker = ConsecutiveComboTracker.Zero.RegisterHit().RegisterHit() }
    let secB = createFighter 40 30 30 30 30 30

    // Group turn with Disorienting Shockwave
    let groupRes =
      ActionResolver.resolveGroupTurn roller (StandardAttack (DisorientingShockwave false)) acumenMage primaryEnemy [ secA; secB ]

    // Primary enemy combo reset and OpponentDisoriented emitted
    Assert.Equal(0, groupRes.PrimaryTarget.ComboTracker.ConsecutiveHits)
    let disorientEvents =
      groupRes.Events
      |> List.filter (function CombatEvent.OpponentDisoriented _ -> true | _ -> false)
    Assert.True(disorientEvents.Length >= 2, "Disorienting shockwave should disorient primary and secondary swarm enemies")

    // Secondary enemy combo also reset by pulse
    Assert.Equal(0, groupRes.SecondaryTargets.[0].ComboTracker.ConsecutiveHits)

  [<Fact>]
  let ``Universal spell access allows all magic users to cast all spells with scaled proficiency`` () =
    let roller = fixedRoller 6
    let intellectSpecialist = createMage 150 100 40 40 40 40
    let acumenSpecialist = createMage 40 40 40 40 150 100
    let target = createMage 30 30 30 30 30 30

    // Both can cast RunicWardTrap!
    let resIntellect = ActionResolver.resolve roller (StandardAttack (RunicWardTrap false)) intellectSpecialist target
    let resAcumen = ActionResolver.resolve roller (StandardAttack (RunicWardTrap false)) acumenSpecialist target

    // Acumen specialist erects a significantly stronger ward due to higher specialization ratio
    Assert.True(resAcumen.Actor.ArcaneWard > resIntellect.Actor.ArcaneWard,
      sprintf "Acumen specialist ward (%d) should be stronger than off-specialist ward (%d)"
        resAcumen.Actor.ArcaneWard resIntellect.Actor.ArcaneWard)

  [<Fact>]
  let ``SteadyForm and CenterMind build bounded Study Stacks and never explode into dozens`` () =
    let roller = fixedRoller 1
    // Veteran fighter with Prowess 170 (like Stone Gargoyle)
    let gargoyleFighter = createFighter 180 220 230 240 170 200
    let opponent = createFighter 100 100 100 100 100 100

    let resSteady = ActionResolver.resolve roller (RecoveryAction SteadyForm) gargoyleFighter opponent
    // Must gain 6 stacks (clamped from 42!), NOT 42 stacks!
    Assert.Equal(6, resSteady.Actor.StudyStacks)

    let stabilizedEvt =
      resSteady.Events
      |> List.tryPick (function
        | CombatEvent.FormStabilized(_, _, gained) -> Some gained
        | _ -> None)
    Assert.True(stabilizedEvt.IsSome)
    Assert.Equal(6, stabilizedEvt.Value)

    // CenterMind with Acumen 170
    let mentalFighter = createMage 100 100 100 100 170 140
    let resCenter = ActionResolver.resolve roller (RecoveryAction CenterMind) mentalFighter opponent
    Assert.Equal(6, resCenter.Actor.StudyStacks)

    // Combatant.addStudyStacks is clamped at 15
    let flooded = resSteady.Actor |> Combatant.addStudyStacks 100
    Assert.Equal(15, flooded.StudyStacks)

  [<Fact>]
  let ``SteadyForm vents all dynamic status meters and restores Morale`` () =
    let roller = fixedRoller 1
    let strainedFighter =
      createFighter 100 100 100 100 100 100
      |> Combatant.updateMeters (fun _ ->
        { Recklessness = Meter.Create 50
          Exhaustion = Meter.Create 60
          Overwhelm = Meter.Create 55
          Frustration = Meter.Create 65
          CognitiveFatigue = Meter.Create 50
          Confusion = Meter.Create 70
          Provoke = Meter.Create 45 })
      |> fun c -> { c with Morale = c.Morale.ApplyDelta -50 }
    let opponent = createFighter 100 100 100 100 100 100

    let res = ActionResolver.resolve roller (RecoveryAction SteadyForm) strainedFighter opponent
    let m = res.Actor.Meters

    // All 7 meters must be substantially reduced
    Assert.True(m.Recklessness.Value < 50, "Recklessness should be reduced")
    Assert.True(m.Exhaustion.Value < 60, "Exhaustion should be reduced")
    Assert.True(m.Overwhelm.Value < 55, "Overwhelm should be reduced")
    Assert.True(m.Frustration.Value < 65, "Frustration should be reduced")
    Assert.True(m.CognitiveFatigue.Value < 50, "CognitiveFatigue should be reduced")
    Assert.True(m.Confusion.Value < 70, "Confusion should be reduced")
    Assert.True(m.Provoke.Value < 45, "Provoke should be reduced")

    // Morale restored
    Assert.True(res.Actor.Morale.Current > strainedFighter.Morale.Current, "Morale should be restored")

    // BreathStabilized event emitted
    let breathEvt =
      res.Events
      |> List.tryPick (function
        | CombatEvent.BreathStabilized(_, summary, mor) -> Some (summary, mor)
        | _ -> None)
    Assert.True(breathEvt.IsSome, "BreathStabilized event must be emitted")

  [<Fact>]
  let ``SteadyForm and recovery resets stabilize a Collapsed combatant when strain drops below 100`` () =
    let roller = fixedRoller 1
    // Fighter collapsed from 100 Frustration
    let collapsedFighter =
      createFighter 100 100 100 100 100 100
      |> Combatant.updateMeters (fun _ ->
        { StatusMeters.Zero with Frustration = Meter.Max })
      |> Combatant.evaluateCollapse
    let opponent = createFighter 100 100 100 100 100 100

    Assert.True(CollapseState.isCollapsed collapsedFighter.Collapse)
    Assert.True(collapsedFighter.IsExecuteEligible)

    let res = ActionResolver.resolve roller (RecoveryAction SteadyForm) collapsedFighter opponent

    // Frustration dropped below 100, so collapse state must be cleared back to Stable!
    Assert.True(res.Actor.Meters.Frustration.Value < 100)
    Assert.False(CollapseState.isCollapsed res.Actor.Collapse)
    Assert.False(res.Actor.IsExecuteEligible)

    let hasRecoveredEvt =
      res.Events
      |> List.exists (function CombatEvent.CollapseRecovered _ -> true | _ -> false)
    Assert.True(hasRecoveredEvt, "CollapseRecovered event must be emitted")

  [<Fact>]
  let ``SteadyForm drains Frustration and Overwhelm in addition to Exhaustion`` () =
    let roller = fixedRoller 1
    let strainedFighter =
      createFighter 100 100 100 100 100 100
      |> Combatant.updateMeters (fun _ ->
        { StatusMeters.Zero with
            Exhaustion = Meter.Create 50
            Overwhelm = Meter.Create 50
            Frustration = Meter.Create 50 })
    let opponent = createFighter 100 100 100 100 100 100

    let res = ActionResolver.resolve roller (RecoveryAction SteadyForm) strainedFighter opponent
    let m = res.Actor.Meters

    Assert.True(m.Exhaustion.Value < 50, "SteadyForm should drain Exhaustion")
    Assert.True(m.Overwhelm.Value < 50, "SteadyForm should drain Overwhelm")
    Assert.True(m.Frustration.Value < 50, "SteadyForm should drain Frustration")

  [<Fact>]
  let ``CenterMind drains Provoke and Frustration in addition to Confusion and Fatigue`` () =
    let roller = fixedRoller 1
    let strainedMage =
      createMage 100 100 100 100 100 100
      |> Combatant.updateMeters (fun _ ->
        { StatusMeters.Zero with
            Confusion = Meter.Create 50
            CognitiveFatigue = Meter.Create 50
            Provoke = Meter.Create 50
            Frustration = Meter.Create 50 })
    let opponent = createFighter 100 100 100 100 100 100

    let res = ActionResolver.resolve roller (RecoveryAction CenterMind) strainedMage opponent
    let m = res.Actor.Meters

    Assert.True(m.Confusion.Value < 50, "CenterMind should drain Confusion")
    Assert.True(m.CognitiveFatigue.Value < 50, "CenterMind should drain CognitiveFatigue")
    Assert.True(m.Provoke.Value < 50, "CenterMind should drain Provoke")
    Assert.True(m.Frustration.Value < 50, "CenterMind should drain Frustration")

  [<Fact>]
  let ``Fluid Stances automatically transition based on the last physical attack issued`` () =
    let roller = fixedRoller 6
    let initialFighter = { createFighter 100 100 100 100 100 100 with Stance = CombatStance.PowerStance; PlayerStyle = true }
    let target = createFighter 100 100 100 100 100 100

    // 1. Issuing FinesseCadence shifts actor from PowerStance to AgilityStance
    let resAgility = ActionResolver.resolve roller (StandardAttack (FinesseCadence false)) initialFighter target
    Assert.Equal(CombatStance.AgilityStance, resAgility.Actor.Stance)
    let shiftedToAgility =
      resAgility.Events
      |> List.exists (function CombatEvent.StanceShifted(_, CombatStance.PowerStance, CombatStance.AgilityStance) -> true | _ -> false)
    Assert.True(shiftedToAgility, "Should emit StanceShifted to AgilityStance")

    // 2. Issuing ProwessStrike shifts actor from AgilityStance to DisciplineStance
    let resDiscipline = ActionResolver.resolve roller (StandardAttack (ProwessStrike false)) resAgility.Actor target
    Assert.Equal(CombatStance.DisciplineStance, resDiscipline.Actor.Stance)
    let shiftedToDiscipline =
      resDiscipline.Events
      |> List.exists (function CombatEvent.StanceShifted(_, CombatStance.AgilityStance, CombatStance.DisciplineStance) -> true | _ -> false)
    Assert.True(shiftedToDiscipline, "Should emit StanceShifted to DisciplineStance")

    // 3. Issuing ForceStrike shifts actor from DisciplineStance to PowerStance
    let resPower = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) resDiscipline.Actor target
    Assert.Equal(CombatStance.PowerStance, resPower.Actor.Stance)
    let shiftedToPower =
      resPower.Events
      |> List.exists (function CombatEvent.StanceShifted(_, CombatStance.DisciplineStance, CombatStance.PowerStance) -> true | _ -> false)
    Assert.True(shiftedToPower, "Should emit StanceShifted to PowerStance")

  [<Fact>]
  let ``Fluid Complex Forms automatically thread based on the last arcane spell issued`` () =
    let roller = fixedRoller 6
    let initialMage = { createMage 100 100 100 100 100 100 with ComplexForm = None; PlayerStyle = true }
    let target = createMage 100 100 100 100 100 100

    // 1. Issuing ArcaneCataclysm threads ResonanceSpike
    let resResonance = ActionResolver.resolve roller (StandardAttack (ArcaneCataclysm false)) initialMage target
    Assert.Equal(Some ComplexForm.ResonanceSpike, resResonance.Actor.ComplexForm)
    let threadedResonance =
      resResonance.Events
      |> List.exists (function CombatEvent.ComplexFormThreaded(_, None, ComplexForm.ResonanceSpike) -> true | _ -> false)
    Assert.True(threadedResonance, "Should emit ComplexFormThreaded to ResonanceSpike")

    // 2. Issuing SynapticGlamour threads PhantasmalDiffusion
    let resDiffusion = ActionResolver.resolve roller (StandardAttack (SynapticGlamour false)) resResonance.Actor target
    Assert.Equal(Some ComplexForm.PhantasmalDiffusion, resDiffusion.Actor.ComplexForm)

    // 3. Issuing RunicWardTrap threads AegisLattice
    let resAegis = ActionResolver.resolve roller (StandardAttack (RunicWardTrap false)) resDiffusion.Actor target
    Assert.Equal(Some ComplexForm.AegisLattice, resAegis.Actor.ComplexForm)

  let private damageTaken attacker atk defender =
    let res = ActionResolver.resolve (fixedRoller 6) (StandardAttack atk) attacker defender
    defender.Health.Current - res.Target.Health.Current, res

  /// A defender that will not die to the top of the stack curve, so damage stays measurable instead
  /// of saturating at its pool.
  let private punchingBag () =
    { createFighter 50 50 50 50 50 50 with Health = Pool.Create 400000 }

  let private studyStacksOf (res: ActionResult) =
    res.Events
    |> List.pick (function
      | CombatEvent.DisciplineGambitExecuted(_, _, spent) -> Some spent
      | _ -> None)

  [<Fact>]
  let ``Force insight cleave spends Study Stacks with zero Recklessness cost`` () =
    let attacker = { createFighter 120 50 50 50 50 50 with StudyStacks = 6 }
    let target = createFighter 50 50 50 50 50 50

    let _, res = damageTaken attacker ForceInsightStrike target

    // The whole pile is spent, whatever the payload asked for.
    Assert.Equal(6, studyStacksOf res)
    Assert.Equal(attacker.Meters.Recklessness.Value, res.Actor.Meters.Recklessness.Value)

  [<Fact>]
  let ``Force insight cleave scales with the stacks actually spent`` () =
    let target = punchingBag ()
    let mk stacks = { createFighter 120 50 50 50 50 50 with StudyStacks = stacks }

    // The payoff follows the pile, so a bigger investment must land harder.
    let dmgFew, _ = damageTaken (mk 4) ForceInsightStrike target
    let dmgMany, _ = damageTaken (mk 8) ForceInsightStrike target
    Assert.True(dmgMany > dmgFew, sprintf "Holding 8 (%d) must outdamage holding 4 (%d)" dmgMany dmgFew)

  [<Fact>]
  let ``Finesse threaded opening spends Study Stacks with zero Recklessness cost`` () =
    let attacker = { createFighter 50 50 120 50 50 50 with StudyStacks = 6 }
    let target = createFighter 50 50 50 50 50 50

    let _, res = damageTaken attacker FinesseInsightStrike target

    Assert.Equal(6, studyStacksOf res)
    Assert.Equal(attacker.Meters.Recklessness.Value, res.Actor.Meters.Recklessness.Value)
    Assert.True(
      res.Target.Meters.Confusion.Value > target.Meters.Confusion.Value,
      "The threaded opening must press the Agility line's Confusion pressure")

  [<Fact>]
  let ``Finesse threaded opening scales with the stacks actually spent`` () =
    let target = punchingBag ()
    let mk stacks = { createFighter 50 50 120 50 50 50 with StudyStacks = stacks }

    let dmgFew, _ = damageTaken (mk 4) FinesseInsightStrike target
    let dmgMany, _ = damageTaken (mk 8) FinesseInsightStrike target
    Assert.True(dmgMany > dmgFew, sprintf "Holding 8 (%d) must outdamage holding 4 (%d)" dmgMany dmgFew)

  [<Fact>]
  let ``Free Prowess read is worth 0.10 damage multiplier per Study Stack`` () =
    let target = createFighter 50 50 50 50 50 50
    let mk stacks = { createFighter 50 50 50 50 120 50 with StudyStacks = stacks }

    let dmgZero, _ = damageTaken (mk 0) (ProwessStrike false) target
    let dmgTen, _ = damageTaken (mk 10) (ProwessStrike false) target

    // 1.0 -> 2.0 multiplier: the study school's own vector reads stacks twice as hard as the flat rate.
    let ratio = float dmgTen / float dmgZero
    Assert.True(ratio > 1.90 && ratio < 2.10, sprintf "10 free stacks should scale damage ~2.0x, got %.3f" ratio)

  [<Fact>]
  let ``Calculated flaw strike pays 0.7 multiplier per stack and follows the spent amount`` () =
    let target = punchingBag ()
    let mk stacks = { createFighter 50 50 50 50 120 50 with StudyStacks = stacks }

    let dmgWith stacks = fst (damageTaken (mk stacks) CalculatedFlawStrike target)
    let d2, d4, d6 = dmgWith 2, dmgWith 4, dmgWith 6

    // Damage is multiplicative in the class multiplier, so the slope/intercept of the spend curve
    // recovers the per-stack factor relative to the 2.2 base multiplier.
    let slope = float (d6 - d2) / 4.0
    let intercept = float d2 - 2.0 * slope
    let perStack = 2.2 * slope / intercept
    Assert.True(perStack > 0.68 && perStack < 0.72, sprintf "Expected 0.7 per stack, fitted %.3f" perStack)

    // Nothing is left behind: the pile is spent, not trimmed to the payload.
    let _, res = damageTaken (mk 6) CalculatedFlawStrike target
    Assert.Equal(6, studyStacksOf res)

  [<Fact>]
  let ``Discipline stance guard anchors on Poise alone`` () =
    let stats =
      StatBlock.Create [
        Force, 50; Fortitude, 50; Finesse, 50; Reflex, 50; Prowess, 50; Poise, 100
        Intellect, 50; Resolve, 50; Acuity, 50; Intuition, 50; Acumen, 50; Composure, 300
      ]
    let defender = Combatant.create (CombatantId.New()) "Guard" 2000 2000 stats

    Assert.Equal(200, defender.GetGuard(Plane.Physical, true))
    Assert.Equal(200, defender.GetGuard(Plane.Mental, true))

    let disciplined = { defender with Stance = CombatStance.DisciplineStance }
    Assert.Equal(150, disciplined.GetGuard(Plane.Physical, true))
    Assert.Equal(100, disciplined.GetGuard(Plane.Mental, true))

  [<Fact>]
  let ``Discipline stance guard ignores Composure at every defence contest`` () =
    let attacker = createFighter 50 50 50 50 400 50

    let guardStats composure =
      StatBlock.Create [
        Force, 50; Fortitude, 50; Finesse, 50; Reflex, 50; Prowess, 50; Poise, 100
        Intellect, 50; Resolve, 50; Acuity, 50; Intuition, 50; Acumen, 50; Composure, composure
      ]

    let guardFighter composure stance =
      { Combatant.create (CombatantId.New()) "Guard" 2000 2000 (guardStats composure) with Stance = stance }

    // Anchored on Poise alone: Composure no longer pads a DisciplineStance guard
    let dmgLow, _ = damageTaken attacker (ProwessStrike false) (guardFighter 100 CombatStance.DisciplineStance)
    let dmgHigh, _ = damageTaken attacker (ProwessStrike false) (guardFighter 400 CombatStance.DisciplineStance)
    Assert.Equal(dmgLow, dmgHigh)

    // Outside the stance the Poise/Composure blend still guards
    let dmgBlendLow, _ = damageTaken attacker (ProwessStrike false) (guardFighter 100 CombatStance.PowerStance)
    let dmgBlendHigh, _ = damageTaken attacker (ProwessStrike false) (guardFighter 400 CombatStance.PowerStance)
    Assert.True(
      dmgBlendHigh < dmgBlendLow,
      sprintf "Composure must keep padding the out-of-stance guard: %d vs %d" dmgBlendHigh dmgBlendLow)
