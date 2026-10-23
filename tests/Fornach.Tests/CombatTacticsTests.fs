namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

module CombatTacticsTests =

  let private fixedRoller (n: int) : DiceRoller = fun _ _ -> n

  [<Fact>]
  let ``Shield Bash deals bludgeoning damage without shifting the attacker's stance`` () =
    let player = TierFactory.createClassLevel CharacterClass.Fighter 30
    let target = TierFactory.createClassLevel CharacterClass.Warrior 30
    let roller = fixedRoller 5

    let intent = ActionIntent.StandardAttack (AttackClassification.ShieldBash false)
    let result = ActionResolver.resolve roller intent player target

    // A bash is a reactive interrupt, not a school technique, so it leaves the stance alone —
    // that is what lets the same action floor, parry or disarm depending on the stance held.
    Assert.Equal(player.Stance, result.Actor.Stance)
    Assert.True(result.Target.Health.Current < target.Health.Current, "Target should have taken physical damage from shield bash")
    Assert.True(result.Target.Meters.Frustration.Value > target.Meters.Frustration.Value, "Target Frustration should increase")

  [<Fact>]
  let ``Shield Bash interrupts enemy windup when target has high Recklessness`` () =
    let player = TierFactory.createClassLevel CharacterClass.Fighter 30
    let target =
      TierFactory.createClassLevel CharacterClass.Warrior 30
      |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + 35 })
    let roller = fixedRoller 5

    let intent = ActionIntent.StandardAttack (AttackClassification.ShieldBash false)
    let result = ActionResolver.resolve roller intent player target

    let interruptedEvt =
      result.Events
      |> List.tryPick (function CombatEvent.ActionInterrupted(_, tId, tech, _) when tId = target.Id -> Some tech | _ -> None)

    Assert.True(interruptedEvt.IsSome, "Shield bash should emit ActionInterrupted against reckless windup")
    Assert.Equal("Shield Bash", interruptedEvt.Value)

  /// A bash in a fixed stance, thrown by a real player-class actor so the stance-keyed branch is
  /// proven reachable from the menu path rather than only for generic opposition.
  let private heavyBash (stance: CombatStance) =
    let actor = { TierFactory.createClassLevel CharacterClass.Fighter 100 with Stance = stance }
    let target = TierFactory.createClassLevel CharacterClass.Fighter 30
    let intent = ActionIntent.StandardAttack (AttackClassification.ShieldBash true)
    target, ActionResolver.resolve (fixedRoller 5) intent actor target

  [<Fact>]
  let ``Heavy Shield Slam in Power Stance knocks the target prone and interrupts its windup`` () =
    let target, power = heavyBash CombatStance.PowerStance
    let _, agility = heavyBash CombatStance.AgilityStance

    // Six points of footing is the knockdown; the rest of the limb penalty rides the hit itself.
    Assert.Equal(agility.Target.LimbDebuff + 6, power.Target.LimbDebuff)
    let knockedProne =
      power.Events
      |> List.exists (function CombatEvent.LimbDisabled(tId, penalty) -> tId = target.Id && penalty = 6 | _ -> false)
    Assert.True(knockedProne, "a Power Stance slam must emit the six-point LimbDisabled")

    let interrupted =
      power.Events
      |> List.exists (function CombatEvent.ActionInterrupted(_, tId, tech, _) -> tId = target.Id && tech = "Shield Bash" | _ -> false)
    Assert.True(interrupted, "a heavy slam must always interrupt the target's windup")

  [<Fact>]
  let ``Heavy Shield Slam in Agility Stance parries and opens the counter window instead`` () =
    let _, power = heavyBash CombatStance.PowerStance
    let _, agility = heavyBash CombatStance.AgilityStance

    // The parry turns the slam aside: no knockdown on the target, and the counter window opens on
    // the parrier (one extra combo hit, worth the tracker's flat 15).
    Assert.Equal(power.Target.LimbDebuff - 6, agility.Target.LimbDebuff)
    Assert.Equal(power.Actor.ComboTracker.VitalOpeningBonus + 15, agility.Actor.ComboTracker.VitalOpeningBonus)

  [<Fact>]
  let ``Heavy Shield Slam in Discipline Stance degrades the target's weapon`` () =
    let target, discipline = heavyBash CombatStance.DisciplineStance
    let _, power = heavyBash CombatStance.PowerStance

    // Whatever incidental wear the blow carries, the Discipline bite adds one more notch.
    Assert.Equal(WeaponCondition.degradation power.Target.WeaponCondition, discipline.Target.WeaponCondition)
    let degraded =
      discipline.Events
      |> List.exists (function CombatEvent.WeaponDegraded(tId, _) -> tId = target.Id | _ -> false)
    Assert.True(degraded, "a Discipline Stance slam should emit WeaponDegraded")

  [<Fact>]
  let ``Raise Shield fortifies armor soak and feeds the ramp`` () =
    let damagedPlayer =
      let p = TierFactory.createClassLevel CharacterClass.Fighter 30
      { p with Armor = { p.Armor with Current = 10 } }
      |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + 40 })

    let dummyTarget = TierFactory.createClassLevel CharacterClass.Warrior 30
    let roller = fixedRoller 4

    let intent = ActionIntent.RecoveryAction DefensiveReset.RaiseShield
    let result = ActionResolver.resolve roller intent damagedPlayer dummyTarget

    Assert.True(result.Actor.Armor.Current > damagedPlayer.Armor.Current, "Raise shield should restore armor integrity")
    // Every swing raises all three ramps, so a raised shield leaves them level with each other.
    Assert.True(result.Actor.Flow >= 2, "Raise shield should feed the Flow ramp")
    Assert.Equal(result.Actor.Flow, result.Actor.Momentum)
    Assert.Equal(result.Actor.Flow, result.Actor.Focus)
    Assert.True(result.Actor.Meters.Recklessness.Value < damagedPlayer.Meters.Recklessness.Value, "Raise shield should bleed recklessness")

    let raisedEvt =
      result.Events
      |> List.tryPick (function CombatEvent.ShieldRaised(_, armor, stacks) -> Some(armor, stacks) | _ -> None)

    Assert.True(raisedEvt.IsSome, "ShieldRaised combat event should be emitted")

  [<Fact>]
  let ``Staff Siphon strikes Morale and siphons Cognitive Fatigue and Recklessness back to caster`` () =
    let strainedMage =
      TierFactory.createClassLevel CharacterClass.Mage 30
      |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + 50; Recklessness = m.Recklessness + 40 })
    let target = TierFactory.createClassLevel CharacterClass.Warrior 30
    let roller = fixedRoller 5

    let intent = ActionIntent.StandardAttack AttackClassification.StaffSiphon
    let result = ActionResolver.resolve roller intent strainedMage target

    Assert.True(result.Target.Morale.Current < target.Morale.Current, "Target morale should be damaged by staff siphon")
    Assert.True(result.Actor.Meters.CognitiveFatigue.Value < strainedMage.Meters.CognitiveFatigue.Value, "Caster fatigue should be siphoned")
    Assert.True(result.Actor.Meters.Recklessness.Value < strainedMage.Meters.Recklessness.Value, "Caster recklessness should be calmed")
    Assert.True(result.Actor.ArcaneWard > strainedMage.ArcaneWard, "Arcane ward should be reinforced")
    Assert.Equal(Some ComplexForm.AegisLattice, result.Actor.ComplexForm)

    let siphonedEvt =
      result.Events
      |> List.tryPick (function CombatEvent.StaffSiphoned(_, _, f, r, w) -> Some(f, r, w) | _ -> None)

    Assert.True(siphonedEvt.IsSome, "StaffSiphoned event should be emitted")

  [<Fact>]
  let ``Telegraphed enemy intents generate descriptive warnings and tactical counterplay advice`` () =
    let boss = TierFactory.createClassLevel CharacterClass.Warrior 40
    let wildBlowIntent = ActionIntent.StandardAttack (AttackClassification.ForceStrike true)

    let title, desc, tip, vector = Display.formatTelegraphedIntent wildBlowIntent boss

    Assert.Contains("HEAVY POWER WINDUP", title)
    Assert.Contains("Shield Bash", tip)
    Assert.Equal("Power", vector)

  [<Fact>]
  let ``buildActionChoicesWithTelegraph dynamically highlights tactical counters against telegraphed vectors`` () =
    let player = TierFactory.createClassLevel CharacterClass.Fighter 30
    let enemy = TierFactory.createClassLevel CharacterClass.Warrior 30

    // 1. Enemy telegraphs Power strike -> Agility gets Counter-Evasion, Shield Bash gets INTERRUPT
    let powerWindup = ActionIntent.StandardAttack (AttackClassification.ForceStrike true)
    let powerChoices = Program.buildActionChoicesWithTelegraph player enemy (Some powerWindup)

    let hasEvasiveTag = powerChoices |> List.exists (fun c -> c.Contains("Counter-Evasion"))
    let hasInterruptTag = powerChoices |> List.exists (fun c -> c.Contains("SHIELD INTERRUPT") || c.Contains("INTERRUPT OPPORTUNITY"))
    Assert.True(hasEvasiveTag, "Agility options should highlight evasive advantage vs Power")
    Assert.True(hasInterruptTag, "Shield bash should highlight interrupt opportunity vs heavy power windup")

    // 2. Enemy telegraphs Agility flurry -> Discipline gets Bastion Counter, Raise Shield gets Parry Opening
    let agilityWindup = ActionIntent.StandardAttack (AttackClassification.FinesseCadence true)
    let agilityChoices = Program.buildActionChoicesWithTelegraph player enemy (Some agilityWindup)

    let hasBastionTag = agilityChoices |> List.exists (fun c -> c.Contains("Bastion Counter"))
    let hasParryTag = agilityChoices |> List.exists (fun c -> c.Contains("Parry Opening"))
    Assert.True(hasBastionTag, "Discipline options should highlight bastion counter vs Agility")
    Assert.True(hasParryTag, "Raise shield should highlight parry opening vs Agility")
