namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

module ActiveDefenseTests =

  let private fixedRoller (result: int) : DiceRoller =
    fun _min _max -> result

  [<Fact>]
  let ``FortitudeGuard counters ForceStrike with 25 percent mitigation and seizes Vor`` () =
    let roller = fixedRoller 5
    let attacker =
      TierFactory.createClassLevel CharacterClass.Warrior 1
      |> fun c -> { c with Tempo = TacticalTiming.Vor }
    let defender =
      TierFactory.createClassLevel CharacterClass.Fighter 1
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let atkIntent = StandardAttack (ForceStrike false)
    let defChoice = FortitudeGuard false

    let outcome = ActionResolver.resolveExWithDefense roller atkIntent (Some(Guard defChoice)) attacker defender 0

    // Affinity counter event must be emitted
    let affinityEventOpt =
      outcome.Events
      |> List.tryPick (function CombatEvent.DefenseAffinityResolved (_, _, isCounter, bonus) -> Some (isCounter, bonus) | _ -> None)

    Assert.True(affinityEventOpt.IsSome, "DefenseAffinityResolved event must be present")
    let isCounter, bonus = affinityEventOpt.Value
    Assert.True(isCounter, "FortitudeGuard must counter ForceStrike")
    Assert.Equal(25, bonus)

    // When countered from the Nach, the defender must seize the Vor
    Assert.True(outcome.Target.IsInVor, "Defender must seize Vor after successful affinity counter")

  [<Fact>]
  let ``ReflexSlip counters FinesseCadence with 25 percent mitigation and seizes Vor`` () =
    let roller = fixedRoller 5
    let attacker =
      TierFactory.createClassLevel CharacterClass.Rogue 1
      |> fun c -> { c with Tempo = TacticalTiming.Vor }
    let defender =
      TierFactory.createClassLevel CharacterClass.Fighter 10
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let atkIntent = StandardAttack (FinesseCadence false)
    let defChoice = ReflexSlip false

    let outcome = ActionResolver.resolveExWithDefense roller atkIntent (Some(Guard defChoice)) attacker defender 0

    let affinityEventOpt =
      outcome.Events
      |> List.tryPick (function CombatEvent.DefenseAffinityResolved (_, _, isCounter, bonus) -> Some (isCounter, bonus) | _ -> None)

    Assert.True(affinityEventOpt.IsSome, "DefenseAffinityResolved event must be present")
    let isCounter, bonus = affinityEventOpt.Value
    Assert.True(isCounter, "ReflexSlip must counter FinesseCadence")
    Assert.Equal(25, bonus)

    Assert.True(outcome.Target.IsInVor, "Defender must seize Vor after successful affinity counter")

  [<Fact>]
  let ``Active defenses are vulnerable to mismatched attack vectors`` () =
    let roller = fixedRoller 5
    let attacker = TierFactory.createClassLevel CharacterClass.Rogue 1
    let defender = TierFactory.createClassLevel CharacterClass.Fighter 1

    // Finesse cadence vs Fortitude guard (wrong affinity)
    let outcome =
      ActionResolver.resolveExWithDefense
        roller
        (StandardAttack (FinesseCadence false))
        (Some(Guard(FortitudeGuard false)))
        attacker
        defender
        0

    let affinityEventOpt =
      outcome.Events
      |> List.tryPick (function CombatEvent.DefenseAffinityResolved (_, _, isCounter, bonus) -> Some (isCounter, bonus) | _ -> None)

    Assert.True(affinityEventOpt.IsSome)
    let isCounter, bonus = affinityEventOpt.Value
    Assert.False(isCounter, "FortitudeGuard must NOT counter FinesseCadence")
    Assert.Equal(-15, bonus)

  [<Fact>]
  let ``Unattacked FortitudeGuard repairs armor and bleeds recklessness`` () =
    let fighter =
      TierFactory.createClassLevel CharacterClass.Fighter 1
      |> fun c ->
        { c with
            Armor = ArmorIntegrity.Create 50 |> fun a -> a.Shred 30
            Tempo = TacticalTiming.Nach }
      |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + 40 })

    let opponent =
      TierFactory.createClassLevel CharacterClass.Warrior 1
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let outcome = ActionResolver.resolveActiveDefense (FortitudeGuard true) fighter opponent

    // Armor repaired
    Assert.True(outcome.Actor.Armor.Current > 20, "Braced FortitudeGuard must repair armor")
    // Recklessness bled
    Assert.True(outcome.Actor.Meters.Recklessness.Value < 40, "FortitudeGuard must bleed Recklessness")
    // Momentum gained
    Assert.Equal(1, outcome.Actor.Momentum)
    // Stance set to PowerStance
    Assert.Equal(CombatStance.PowerStance, outcome.Actor.Stance)
    // Seizes Vor
    Assert.True(outcome.Actor.IsInVor, "Unattacked active defense must seize the Vor")

    let hasBenefitEvent =
      outcome.Events
      |> List.exists (function CombatEvent.UnattackedStanceBenefit _ -> true | _ -> false)
    Assert.True(hasBenefitEvent, "UnattackedStanceBenefit event must be emitted")

  [<Fact>]
  let ``Unattacked ReflexSlip vents overwhelm and exhaustion and sets flow`` () =
    let rogue =
      TierFactory.createClassLevel CharacterClass.Rogue 1
      |> fun c -> { c with Tempo = TacticalTiming.Nach }
      |> Combatant.updateMeters (fun m ->
        { m with
            Frustration = m.Frustration + 50
            Exhaustion = m.Exhaustion + 50 })

    let opponent =
      TierFactory.createClassLevel CharacterClass.Warrior 1
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let outcome = ActionResolver.resolveActiveDefense (ReflexSlip true) rogue opponent

    Assert.True(outcome.Actor.Meters.Frustration.Value < 50, "ReflexSlip must vent Frustration")
    Assert.True(outcome.Actor.Meters.Exhaustion.Value < 50, "ReflexSlip must vent Exhaustion")
    Assert.Equal(1, outcome.Actor.Flow)
    Assert.Equal(CombatStance.AgilityStance, outcome.Actor.Stance)
    Assert.True(outcome.Actor.IsInVor, "Unattacked active defense must seize the Vor")

  [<Fact>]
  let ``Mental plane active defenses ResolveAnchor and IntuitionPerception counter mental attacks`` () =
    let roller min max = if max = 100 then 100 else 5
    let attacker =
      TierFactory.createClassLevel CharacterClass.Mage 1
      |> fun c -> { c with Tempo = TacticalTiming.Vor }
    let defender =
      TierFactory.createClassLevel CharacterClass.Mage 1
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    // ResolveAnchor counters ArcaneCataclysm (Power)
    let outcomePower =
      ActionResolver.resolveExWithDefense
        roller
        (StandardAttack (ArcaneCataclysm false))
        (Some(Guard(ResolveAnchor false)))
        attacker
        defender
        0

    let powerAffinity =
      outcomePower.Events
      |> List.tryPick (function CombatEvent.DefenseAffinityResolved (_, _, isCounter, _) -> Some isCounter | _ -> None)
    Assert.Equal(Some true, powerAffinity)

    // IntuitionPerception counters SynapticGlamour (Agility)
    let outcomeAgility =
      ActionResolver.resolveExWithDefense
        roller
        (StandardAttack (SynapticGlamour false))
        (Some(Guard(IntuitionPerception false)))
        attacker
        defender
        0

    let agilAffinity =
      outcomeAgility.Events
      |> List.tryPick (function CombatEvent.DefenseAffinityResolved (_, _, isCounter, _) -> Some isCounter | _ -> None)
    Assert.Equal(Some true, agilAffinity)

  [<Fact>]
  let ``Decoy interception absorbs blow and yields Vor to defender`` () =
    let roller min max = if max = 100 then 1 else 3
    let attacker =
      TierFactory.createClassLevel CharacterClass.Warrior 1
      |> fun c -> { c with Tempo = TacticalTiming.Vor }
    let defender =
      TierFactory.createClassLevel CharacterClass.Mage 1
      |> fun c -> { c with Tempo = TacticalTiming.Nach; MirrorClones = 1 }

    let outcome =
      ActionResolver.resolveExWithDefense
        roller
        (StandardAttack (ForceStrike false))
        None
        attacker
        defender
        0

    // Defender was protected by decoy
    Assert.Equal(0, outcome.Target.MirrorClones)
    // Attacker must have yielded the Vor and defender seized it
    Assert.True(outcome.Target.IsInVor, "Defender must seize Vor when decoy intercepts strike")
    Assert.True(outcome.Actor.IsInNach, "Attacker must yield Vor when decoy intercepts strike")
    let tempoEvent =
      outcome.Events
      |> List.tryPick (function CombatEvent.TempoShifted (taker, yielder, _) -> Some (taker, yielder) | _ -> None)
    Assert.Equal(Some (defender.Id, attacker.Id), tempoEvent)

  [<Fact>]
  let ``Defender reactive disarm defuses attack and yields Vor to defender`` () =
    // Roller returns 1 so 1d100 <= counterChance succeeds
    let roller _min _max = 1
    let attacker =
      TierFactory.createClassLevel CharacterClass.Warrior 1
      |> fun c -> { c with Tempo = TacticalTiming.Vor }
    let defender =
      TierFactory.createClassLevel CharacterClass.Fighter 10
      |> fun c ->
        { c with
            Tempo = TacticalTiming.Nach
            Momentum = 5
            Stats = c.Stats.Modify(StatId.Prowess, 50) }

    let outcome =
      ActionResolver.resolveExWithDefense
        roller
        (StandardAttack (ForceStrike false))
        None
        attacker
        defender
        0

    Assert.True(outcome.Target.IsInVor, "Defender must seize Vor on successful reactive disarm")
    Assert.True(outcome.Actor.IsInNach, "Attacker must yield Vor on successful reactive disarm")
    let disarmEvent =
      outcome.Events
      |> List.tryPick (function CombatEvent.DisarmExecuted _ -> Some true | _ -> None)
    Assert.Equal(Some true, disarmEvent)

  [<Fact>]
  let ``BestiaryKnowledge adds targeted discipline bonus against matching enemy species`` () =
    let roller _min _max = 50
    let wolfTarget =
      TierFactory.createClassLevel CharacterClass.Warrior 1
      |> fun c -> { c with Name = "Lv.1 Forest Wolf" }
    let banditTarget =
      TierFactory.createClassLevel CharacterClass.Warrior 1
      |> fun c -> { c with Name = "Lv.1 Bandit" }

    // Attacker with 0 Prowess but +20 BestiaryKnowledge against "Forest Wolf"
    let attacker =
      TierFactory.createClassLevel CharacterClass.Fighter 1
      |> fun c ->
        let prog = { c.Progression with BestiaryKnowledge = Map [ "Forest Wolf", 20 ] }
        { c with Progression = prog }

    // Scrutiny against Forest Wolf includes the +20 bonus
    let scrutinyWolf = TacticalAssessment.calculateEffectiveScrutinyAgainst wolfTarget attacker
    let scrutinyBandit = TacticalAssessment.calculateEffectiveScrutinyAgainst banditTarget attacker
    Assert.Equal(scrutinyBandit + 20, scrutinyWolf)

    // Assessment insight is heightened against known species
    let assessWolf = attacker.AssessTarget wolfTarget
    let assessBandit = attacker.AssessTarget banditTarget
    Assert.True(assessWolf.EffectiveAcumen > assessBandit.EffectiveAcumen)

  [<Fact>]
  let ``Seize Indes that wins the exchange stops the strike and takes the Vor`` () =
    let roller = fixedRoller 5
    // The defender outclasses the attacker, so its counter out-rolls the incoming strike outright.
    let attacker =
      TierFactory.createClassLevel CharacterClass.Warrior 1
      |> fun c -> { c with Tempo = TacticalTiming.Vor }
    let defender =
      TierFactory.createClassLevel CharacterClass.Fighter 40
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let atkIntent = StandardAttack (ForceStrike false)
    let counter = SeizeIndes(ForceStrike false)

    let outcome =
      ActionResolver.resolveExWithDefense roller atkIntent (Some counter) attacker defender 0

    // The counter that wins reports itself as a seizure, so the log shows a strike answered by a strike.
    let seizedNote =
      outcome.Events
      |> List.tryPick (function
        | CombatEvent.TempoShifted (_, _, reason) when reason.Contains("seized the Indes") -> Some reason
        | _ -> None)

    Assert.True(seizedNote.IsSome, "a winning counter must report an Indes seizure")

    // A counter that wins stops the blow as surely as a read does, and the Vor changes hands.
    Assert.Equal(defender.Health.Current, outcome.Target.Health.Current)
    Assert.Equal(defender.Morale.Current, outcome.Target.Morale.Current)
    Assert.True(outcome.Target.IsInVor, "the countering defender must take the Vor")
    Assert.True(outcome.Actor.IsInNach, "the stopped attacker must fall into the Nach")

  [<Fact>]
  let ``Seize Indes that loses the exchange lands half again as hard and spends every stack`` () =
    let roller = fixedRoller 5
    let attacker =
      TierFactory.createClassLevel CharacterClass.Warrior 40
      |> fun c -> { c with Tempo = TacticalTiming.Vor }
    let defender =
      TierFactory.createClassLevel CharacterClass.Fighter 1
      |> fun c -> { c with Tempo = TacticalTiming.Nach; Health = Pool.Create 1_000_000 }
      |> Combatant.addStacks 3

    let atkIntent = StandardAttack (ForceStrike false)

    // Baseline: the same strike against a defender who did not answer it at all.
    let undefended = ActionResolver.resolveEx roller atkIntent attacker defender 0

    let countered =
      ActionResolver.resolveExWithDefense roller atkIntent (Some(SeizeIndes(ForceStrike false))) attacker defender 0

    // A counter that loses answered nothing: the strike it invited lands half again as hard as an unopposed one.
    let undefendedDamage = defender.Health.Current - undefended.Target.Health.Current
    let counteredDamage = defender.Health.Current - countered.Target.Health.Current

    Assert.True(undefendedDamage > 0, "the strike must land for this test to mean anything")
    Assert.Equal(1.5, float counteredDamage / float undefendedDamage, 1)

    // The gamble was made of the banked stacks, and they do not survive a counter that failed.
    Assert.Equal(0, countered.Target.Momentum)
    Assert.Equal(0, countered.Target.Flow)
