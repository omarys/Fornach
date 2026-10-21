namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

module CombatTacticsTests =

  let private fixedRoller (n: int) : DiceRoller = fun _ _ -> n

  [<Fact>]
  let ``Shield Bash deals bludgeoning damage and fluidly transitions into Discipline Stance`` () =
    let player = TierFactory.createClassLevel CharacterClass.Fighter 30
    let target = TierFactory.createClassLevel CharacterClass.Warrior 30
    let roller = fixedRoller 5

    let intent = ActionIntent.StandardAttack (AttackClassification.ShieldBash false)
    let result = ActionResolver.resolve roller intent player target

    Assert.Equal(CombatStance.DisciplineStance, result.Actor.Stance)
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

  [<Fact>]
  let ``Raise Shield fortifies armor soak and grants study stacks`` () =
    let damagedPlayer =
      let p = TierFactory.createClassLevel CharacterClass.Fighter 30
      { p with Armor = { p.Armor with Current = 10 } }
      |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + 40 })

    let dummyTarget = TierFactory.createClassLevel CharacterClass.Warrior 30
    let roller = fixedRoller 4

    let intent = ActionIntent.RecoveryAction DefensiveReset.RaiseShield
    let result = ActionResolver.resolve roller intent damagedPlayer dummyTarget

    Assert.True(result.Actor.Armor.Current > damagedPlayer.Armor.Current, "Raise shield should restore armor integrity")
    Assert.True(result.Actor.StudyStacks >= 2, "Raise shield should grant study stacks")
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
