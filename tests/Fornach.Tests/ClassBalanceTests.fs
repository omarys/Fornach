module Fornach.Tests.ClassBalanceTests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

[<Fact>]
let ``Physical sub-classes follow exact 1.0 to 0.75 to 0.75 stat ratios`` () =
  let level = 200 // GrandMaster tier

  // 1. Warrior: Power Primary (Power 1.0, Agility 0.75, Discipline 0.75)
  let warrior = TierFactory.createClassLevel CharacterClass.Warrior level
  Assert.Equal(841, warrior.GetStat Force)
  Assert.Equal(631, warrior.GetStat Finesse)
  Assert.Equal(631, warrior.GetStat Prowess)

  // 2. Rogue: Agility Primary (Agility 1.0, Discipline 0.75, Power 0.75)
  let rogue = TierFactory.createClassLevel CharacterClass.Rogue level
  Assert.Equal(841, rogue.GetStat Finesse)
  Assert.Equal(631, rogue.GetStat Prowess)
  Assert.Equal(631, rogue.GetStat Force)

  // 3. Soldier: Discipline Primary (Discipline 1.0, Power 0.75, Agility 0.75)
  let soldier = TierFactory.createClassLevel CharacterClass.Soldier level
  Assert.Equal(841, soldier.GetStat Prowess)
  Assert.Equal(631, soldier.GetStat Force)
  Assert.Equal(631, soldier.GetStat Finesse)

[<Fact>]
let ``Mage mental stats grow at the full 1.0 ratio on all three mental axes`` () =
  let level = 200 // GrandMaster tier

  // The Mage is the only arcane class left and normalizes Intellect, Acuity and Acumen at the primary 1.0 ratio
  let mage = TierFactory.createClassLevel CharacterClass.Mage level
  Assert.Equal(756, mage.GetStat Intellect)
  Assert.Equal(756, mage.GetStat Acuity)
  Assert.Equal(756, mage.GetStat Acumen)

[<Fact>]
let ``Mage active Phantasmal Decoy Swap defuses incoming kinetic strike in the Nach`` () =
  let roller : DiceRoller = fun _ _ -> 1 // Guaranteed favorable roll
  let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let warrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster

  // Warrior attacks with ForceStrike: Wild Blow
  let attack = AttackClassification.ForceStrike true
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent warrior mage

  // Assert decoy intercepted
  let swapEvent =
    result.Events
    |> List.tryPick (function CombatEvent.PhantasmalSwapExecuted (_, _, success, _) -> Some success | _ -> None)
  Assert.True(swapEvent.IsSome)
  Assert.True(swapEvent.Value)

  // Mage Health must be undamaged!
  Assert.Equal(mage.Health.Current, result.Target.Health.Current)
  // Attacker gained confusion and combo reset
  Assert.True(result.Actor.Meters.Confusion.Value > 0)
  Assert.Equal(0, result.Actor.ComboTracker.ConsecutiveHits)

[<Fact>]
let ``A Mage's Destabilizing Ground Ward disrupts incoming attacker mid-swing in the Nach`` () =
  // A d100 of 100 beats the mirror-clone weave (clamped to at most 90), so the ground ward is the defense that answers
  let roller : DiceRoller = fun min max -> if min = 1 && max = 100 then 100 else 6
  let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let warrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster

  let attack = AttackClassification.ForceStrike true
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent warrior mage

  let wardEvent =
    result.Events
    |> List.tryPick (function CombatEvent.DestabilizingWardTriggered (_, _, _, mitig) -> Some mitig | _ -> None)
  Assert.True(wardEvent.IsSome)

[<Fact>]
let ``A Mage's Synaptic Mind-Shock disrupts attacker focus in the Nach`` () =
  // A d100 of 100 beats the mirror-clone weave and a Soldier's Poise (841) beats the ground ward's margin,
  // so mind-shock is the defense left to answer the strike
  let roller : DiceRoller = fun min max -> if min = 1 && max = 100 then 100 else 6
  let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let soldier = TierFactory.createClassTier CharacterClass.Soldier CombatTier.GrandMaster

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent soldier mage

  let shockEvent =
    result.Events
    |> List.tryPick (function CombatEvent.SynapticMindShockDisrupted (_, _, fat, _) -> Some fat | _ -> None)
  Assert.True(shockEvent.IsSome)
  Assert.True(shockEvent.Value > 0)

[<Fact>]
let ``ActionResolver computeTierMultiplier is normalized with diminishing returns and soft-capped at 5.0x`` () =
  Assert.Equal(0.0, ActionResolver.computeTierMultiplier 0)
  Assert.Equal(1.50, ActionResolver.computeTierMultiplier 3)
  Assert.Equal(3.50, ActionResolver.computeTierMultiplier 8)

  // NetHits = 18: smoothly scales with diminishing returns
  let mult18 = ActionResolver.computeTierMultiplier 18
  Assert.True(mult18 <= 5.00)

  // NetHits = 36: strictly soft-capped at 5.00x to prevent high-tier one-shot kills
  let mult36 = ActionResolver.computeTierMultiplier 36
  Assert.Equal(5.00, mult36)

[<Fact>]
let ``Grandmaster Mage fends off Novice Warrior with zero fatigue and zero exhaustion (Spherical Chicken)`` () =
  let roller : DiceRoller = fun _ _ -> 1
  let gmMage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let noviceWarrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent noviceWarrior gmMage

  // Passive clone weaving + Phantasmal Decoy Swap both incurred 0 drain due to massive disparity
  Assert.Equal(0, result.Target.Meters.CognitiveFatigue.Value)
  Assert.Equal(0, result.Target.Meters.Exhaustion.Value)
  Assert.Equal(gmMage.Health.Current, result.Target.Health.Current)

[<Fact>]
let ``Novice Mage facing Novice Warrior incurs disparity-scaled fatigue and exhaustion costs`` () =
  let roller : DiceRoller = fun _ _ -> 1
  let noviceMage = TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice
  let noviceWarrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent noviceWarrior noviceMage

  // Disparity is small (< 30), so every passive incurs its standard cost: ambient weave (+4), decoy swap
  // (+5 fatigue, +3 exhaustion), composure ward reinforcement (+4) and psychic presence (+4)
  Assert.Equal(17, result.Target.Meters.CognitiveFatigue.Value)
  Assert.Equal(3, result.Target.Meters.Exhaustion.Value)
  Assert.Equal(noviceMage.Health.Current, result.Target.Health.Current)

[<Fact>]
let ``Grandmaster Mage fends off Novice Warrior with zero fatigue and zero exhaustion`` () =
  let roller : DiceRoller = fun _ _ -> 6
  let gmMage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let noviceWarrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent noviceWarrior gmMage

  Assert.Equal(0, result.Target.Meters.CognitiveFatigue.Value)
  Assert.Equal(0, result.Target.Meters.Exhaustion.Value)
  Assert.True(result.Target.ArcaneWard > 0)

[<Fact>]
let ``Grandmaster Mage fends off Novice Warrior with zero fatigue drain`` () =
  let roller : DiceRoller = fun _ _ -> 6
  let gmMage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let noviceWarrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent noviceWarrior gmMage

  Assert.Equal(0, result.Target.Meters.CognitiveFatigue.Value)

[<Fact>]
let ``Physical archetypes all carry real mental defence and keep only their offence off-plane`` () =
  let level = 200 // GrandMaster tier
  let gmRogue = TierFactory.createClassLevel CharacterClass.Rogue level
  let gmWarrior = TierFactory.createClassLevel CharacterClass.Warrior level
  let gmSoldier = TierFactory.createClassLevel CharacterClass.Soldier level
  let gmMage = TierFactory.createClassLevel CharacterClass.Mage level

  // Mental defence is no longer a dump stat for physical archetypes: Resolve, Intuition and Composure
  // all sit at the secondary tier (631 at GrandMaster) for every one of them. Only the off-plane
  // *offence* stays deliberately low, which is the trade-off that survived the class reduction.
  for fighter in [ gmRogue; gmWarrior; gmSoldier ] do
    Assert.Equal(631, fighter.GetStat Resolve)
    Assert.Equal(631, fighter.GetStat Intuition)
    Assert.Equal(631, fighter.GetStat Composure)
    Assert.Equal(214, fighter.GetStat Intellect)

  // Agility archetype: Acuity is its off-plane offence (offOff = 317)
  Assert.Equal(317, gmRogue.GetStat Acuity)

  // Power archetype: Acumen is its off-plane offence
  Assert.Equal(317, gmWarrior.GetStat Acumen)

  // Discipline archetype: neither mental offence is developed
  Assert.Equal(214, gmSoldier.GetStat Acuity)
  Assert.Equal(214, gmSoldier.GetStat Acumen)

  // Magic user: physical stat growth stopped developing after level 49; physical defense boosted by spell armor (80% at GM tier)
  Assert.Equal(63, gmMage.GetStat Force) // Physical offense stopped developing after level 49
  Assert.Equal(740, gmMage.GetStat Poise) // 135 base + 80% of Composure (756)
  Assert.Equal(668, gmMage.GetStat Reflex) // 63 base + 80% of Intuition (756)

[<Fact>]
let ``Mage mirror decoy shatters upon being attacked, inflicting retaliatory Morale blast damage`` () =
  let roller : DiceRoller = fun _ _ -> 1
  let gmMage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let gmRogue = TierFactory.createClassTier CharacterClass.Rogue CombatTier.GrandMaster

  let attack = AttackClassification.FinesseCadence false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent gmRogue gmMage

  // Decoy intercepted and shattered
  let shatteredEvent =
    result.Events
    |> List.tryPick (function CombatEvent.MirrorCloneShattered (_, _, blastDmg, _) -> Some blastDmg | _ -> None)
  Assert.True(shatteredEvent.IsSome)
  Assert.True(shatteredEvent.Value > 0)

  // Attacker took retaliatory Morale damage from shatter
  Assert.True(result.Actor.Morale.Current < gmRogue.Morale.Current)
  // Attacker gained confusion and combo reset from shatter shockwave
  Assert.True(result.Actor.Meters.Confusion.Value > 0)
  Assert.Equal(0, result.Actor.ComboTracker.ConsecutiveHits)

[<Fact>]
let ``Physical character cannot execute magic attacks and action is blocked`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let warrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster
  let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster

  let attack = AttackClassification.ArcaneCataclysm false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent warrior mage

  Assert.True(result.Contest.IsNone, "Blocked attack must not produce a contest.")
  let resetEvent =
    result.Events
    |> List.tryPick (function
      | CombatEvent.ComboReset (_, reason) when reason.Contains("cannot execute magic attacks") -> Some reason
      | _ -> None)
  Assert.True(resetEvent.IsSome, "Must emit ComboReset indicating magic attacks are disabled for physical characters.")

[<Fact>]
let ``Magic character cannot execute physical martial attacks and action is blocked`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let warrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent mage warrior

  Assert.True(result.Contest.IsNone, "Blocked attack must not produce a contest.")
  let resetEvent =
    result.Events
    |> List.tryPick (function
      | CombatEvent.ComboReset (_, reason) when reason.Contains("cannot execute physical martial attacks") -> Some reason
      | _ -> None)
  Assert.True(resetEvent.IsSome, "Must emit ComboReset indicating physical attacks are disabled for magic characters.")

[<Fact>]
let ``Physical character successfully executes physical strike and produces contest`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let warrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster
  let soldier = TierFactory.createClassTier CharacterClass.Soldier CombatTier.GrandMaster

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent warrior soldier

  Assert.True(result.Contest.IsSome, "Valid physical attack must produce contest.")

[<Fact>]
let ``Magic character successfully executes magic strike and produces contest`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let warrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster

  let attack = AttackClassification.SynapticGlamour false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent mage warrior

  Assert.True(result.Contest.IsSome, "Valid magic attack must produce contest.")

[<Fact>]
let ``Mental character can thread complex forms and emits ComplexFormThreaded event`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let enemy = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster

  let intent = ActionIntent.ThreadComplexForm ComplexForm.AegisLattice
  let result = ActionResolver.resolve roller intent mage enemy

  Assert.Equal(Some ComplexForm.AegisLattice, result.Actor.ComplexForm)
  let threadedEvt =
    result.Events
    |> List.tryPick (function
      | CombatEvent.ComplexFormThreaded (_, oldForm, newForm) -> Some (oldForm, newForm)
      | _ -> None)
  Assert.True(threadedEvt.IsSome, "Must emit ComplexFormThreaded event.")
  let oldOpt, newF = threadedEvt.Value
  Assert.Equal(Some ComplexForm.ResonanceSpike, oldOpt)
  Assert.Equal(ComplexForm.AegisLattice, newF)

[<Fact>]
let ``Physical character cannot thread complex forms and action is blocked`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let warrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster
  let enemy = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster

  let intent = ActionIntent.ThreadComplexForm ComplexForm.ResonanceSpike
  let result = ActionResolver.resolve roller intent warrior enemy

  Assert.True(result.Contest.IsNone, "Blocked form threading must not produce contest.")
  let resetEvt =
    result.Events
    |> List.tryPick (function
      | CombatEvent.ComboReset (_, reason) when reason.Contains("cannot thread mental complex forms") -> Some reason
      | _ -> None)
  Assert.True(resetEvt.IsSome, "Must emit ComboReset indicating complex forms are mental only.")

[<Fact>]
let ``Mental character cannot shift physical martial stances and action is blocked`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let enemy = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster

  let intent = ActionIntent.ShiftStance CombatStance.PowerStance
  let result = ActionResolver.resolve roller intent mage enemy

  Assert.True(result.Contest.IsNone, "Blocked stance shift must not produce contest.")
  let resetEvt =
    result.Events
    |> List.tryPick (function
      | CombatEvent.ComboReset (_, reason) when reason.Contains("cannot shift physical martial stances") -> Some reason
      | _ -> None)
  Assert.True(resetEvt.IsSome, "Must emit ComboReset indicating martial stances are physical only.")

[<Fact>]
let ``Resonance Spike boosts arcane spell damage and inflicts Fading drain and extra cognitive fatigue`` () =
  let roller : DiceRoller = fun _ _ -> 5
  let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
  let enemy = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster

  // Mage defaults to ResonanceSpike
  Assert.Equal(Some ComplexForm.ResonanceSpike, mage.ComplexForm)

  let intent = ActionIntent.StandardAttack (AttackClassification.ArcaneCataclysm false)
  let result = ActionResolver.resolve roller intent mage enemy

  // Verify Fading drain suffered by caster
  let fadingEvt =
    result.Events
    |> List.tryPick (function
      | CombatEvent.FadingDrainSuffered (_, name, fatDrain, reckSpike) -> Some (name, fatDrain, reckSpike)
      | _ -> None)
  Assert.True(fadingEvt.IsSome, "Resonance Spike must emit FadingDrainSuffered event.")
  let name, fatDrain, reckSpike = fadingEvt.Value
  Assert.Equal("Resonance Spike", name)
  Assert.Equal(10, fatDrain)
  Assert.Equal(15, reckSpike)
  Assert.True(result.Actor.Meters.CognitiveFatigue.Value >= 10, "Caster must suffer fatigue from Fading drain.")
  Assert.True(result.Actor.Meters.Recklessness.Value >= 15, "Caster must suffer recklessness from Fading drain.")

  // Verify extra cognitive fatigue inflicted on target
  Assert.True(result.Target.Meters.CognitiveFatigue.Value >= 20, "Target must suffer extra cognitive fatigue from Resonance Spike.")

[<Fact>]
let ``Aegis Lattice regenerates +15 Arcane Ward on turn upkeep`` () =
  let mage =
    TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
    |> Combatant.setComplexForm (Some ComplexForm.AegisLattice)
    |> fun c -> { c with ArcaneWard = 20 }

  let updated, events = ActionResolver.applyTurnUpkeep mage

  Assert.Equal(35, updated.ArcaneWard)
  let wardEvt =
    events
    |> List.tryPick (function
      | CombatEvent.ArcaneWardErected (_, added, total) -> Some (added, total)
      | _ -> None)
  Assert.True(wardEvt.IsSome, "Must emit ArcaneWardErected event during turn upkeep.")
  Assert.Equal((15, 35), wardEvt.Value)

[<Fact>]
let ``Phantasmal Diffusion passively replenishes mirror clones during turn upkeep`` () =
  let mage =
    TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
    |> fun c -> { c with MirrorClones = 0; ComplexForm = Some ComplexForm.PhantasmalDiffusion }

  let updated, events = ActionResolver.applyTurnUpkeep mage

  Assert.Equal(1, updated.MirrorClones)
  let cloneEvt =
    events
    |> List.tryPick (function
      | CombatEvent.MirrorClonesConjured (_, added, total) -> Some (added, total)
      | _ -> None)
  Assert.True(cloneEvt.IsSome, "Must emit MirrorClonesConjured during turn upkeep.")
  Assert.Equal((1, 1), cloneEvt.Value)

[<Fact>]
let ``Aegis Lattice grounds Overchannel back to standard cast`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let mage =
    TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
    |> Combatant.setComplexForm (Some ComplexForm.AegisLattice)
  let enemy = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster

  let intent = ActionIntent.StandardAttack (AttackClassification.ArcaneCataclysm true)
  let result = ActionResolver.resolve roller intent mage enemy

  let gambitDeclared =
    result.Events
    |> List.exists (function
      | CombatEvent.GambitDeclared (_, name, _) when name.Contains("Overchanneled") -> true
      | _ -> false)
  Assert.False(gambitDeclared, "Aegis Lattice must prevent Overchannel gambit declaration.")
  Assert.True(result.Actor.Meters.Recklessness.Value < 35, "Must not suffer the +35 Recklessness gambit penalty.")

[<Fact>]
let ``Aegis Lattice reflects 50% damage and 15 Frustration when Arcane Ward absorbs incoming damage`` () =
  // A d100 of 100 beats the mirror-clone weave and a Fighter's Poise (841) beats the ground ward's margin,
  // so the strike reaches the ward and is absorbed
  let roller : DiceRoller = fun min max -> if min = 1 && max = 100 then 100 else 4
  let fighter = TierFactory.createClassTier CharacterClass.Fighter CombatTier.GrandMaster
  let mage =
    TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster
    |> Combatant.setComplexForm (Some ComplexForm.AegisLattice)
    |> fun c -> { c with ArcaneWard = 100 }

  let intent = ActionIntent.StandardAttack (AttackClassification.ForceStrike false)
  let result = ActionResolver.resolve roller intent fighter mage

  let reflectEvt =
    result.Events
    |> List.tryPick (function
      | CombatEvent.RetributionReflected (_, _, reflectDmg, frust) -> Some (reflectDmg, frust)
      | _ -> None)
  Assert.True(reflectEvt.IsSome, "Aegis Lattice ward absorption must reflect damage.")
  let reflectDmg, frust = reflectEvt.Value
  Assert.True(reflectDmg > 0, "Reflected damage must be positive.")
  Assert.Equal(15, frust)
  Assert.True(result.Actor.Meters.Frustration.Value >= 15, "Attacker must suffer +15 Frustration from Aegis Lattice reflection.")

[<Fact>]
let ``Combat action choices are ordered by class archetype vector`` () =
  let dummyEnemy = TierFactory.createClassLevel CharacterClass.Warrior 1

  // 1. Warrior (Power Vector): Force / Fortitude must be top choices
  let warrior = TierFactory.createClassLevel CharacterClass.Warrior 1
  let berserkerChoices = Program.buildActionChoices warrior dummyEnemy
  Assert.Contains("Force Strike: Standard Cleave", berserkerChoices.[0])
  Assert.Contains("Force Strike: Wild Blow", berserkerChoices.[1])

  // 2. Rogue (Agility Vector): Finesse / Reflex must be top choices
  let rogue = TierFactory.createClassLevel CharacterClass.Rogue 1
  let duelistChoices = Program.buildActionChoices rogue dummyEnemy
  Assert.Contains("Finesse Cadence: Rapid Probing", duelistChoices.[0])
  Assert.Contains("Finesse Cadence: Relentless Blitz", duelistChoices.[1])

  // 3. Soldier (Discipline Vector): Prowess / Poise must be top choices
  let soldier = TierFactory.createClassLevel CharacterClass.Soldier 1
  let wardenChoices = Program.buildActionChoices soldier dummyEnemy
  Assert.Contains("Prowess Strike: Stance Pressure", wardenChoices.[0])
  Assert.Contains("Prowess Strike: Invitational Bait", wardenChoices.[1])

  // 4. Mage (Power Mental Vector): its own mental strikes must lead. A plain Warrior dummy is
  // defensively opaque to mental attacks, so every Mage strike lands near-zero there and the exact
  // winner is noise — assert the family, not one action.
  let mage = TierFactory.createClassLevel CharacterClass.Mage 1
  let inquisitorChoices = Program.buildActionChoices mage dummyEnemy
  let mentalStrikes = [ "Arcane Cataclysm"; "Synaptic Glamour"; "Mirror Illusion"; "Authority Decree" ]
  Assert.True(
    mentalStrikes |> List.exists inquisitorChoices.[0].Contains,
    sprintf "Mage must lead with a mental strike, got %s" inquisitorChoices.[0]
  )

[<Fact>]
let ``All built action choices parse successfully to non-fallback ActionIntent`` () =
  let dummyEnemy = TierFactory.createClassLevel CharacterClass.Warrior 1
  let classes = [
    CharacterClass.Warrior
    CharacterClass.Rogue
    CharacterClass.Soldier
    CharacterClass.Fighter
    CharacterClass.Mage
  ]

  for cls in classes do
    let player = TierFactory.createClassLevel cls 1
    let choices = Program.buildActionChoices player dummyEnemy
    for choice in choices do
      let intent = Program.parseActionChoice choice
      if not (choice.Contains("Steady Form")) then
        match intent with
        | ActionIntent.RecoveryAction SteadyForm ->
          Assert.True(false, sprintf "Choice '%s' unexpectedly fell through to default SteadyForm fallback!" choice)
        | _ -> ()

[<Fact>]
let ``meter-gated gambit labels parse to their intended intents`` () =
  let dummyEnemy = TierFactory.createClassLevel CharacterClass.Warrior 1

  for cls in [ CharacterClass.Warrior; CharacterClass.Rogue ] do
    let player = TierFactory.createClassLevel cls 1
    // Each cash-out gambit unlocks at two stacks in its own meter.
    let readied = { player with Momentum = 2; Flow = 2; Focus = 3 }
    let choices = Program.buildActionChoices readied dummyEnemy
    Assert.Contains(choices, fun (c: string) -> c.Contains("Force Strike: Insight Cleave"))
    Assert.Contains(choices, fun (c: string) -> c.Contains("Finesse Cadence: Threaded Opening"))

    let intentOf (anchor: string) =
      choices |> List.find (fun (c: string) -> c.Contains anchor) |> Program.parseActionChoice

    match intentOf "Force Strike: Insight Cleave" with
    | ActionIntent.StandardAttack ForceInsightStrike -> ()
    | other -> Assert.True(false, sprintf "Force insight cleave parsed to %A" other)

    match intentOf "Finesse Cadence: Threaded Opening" with
    | ActionIntent.StandardAttack FinesseInsightStrike -> ()
    | other -> Assert.True(false, sprintf "Finesse threaded opening parsed to %A" other)

  // The gambits stay hidden below their 2-stack floor
  let spent = { TierFactory.createClassLevel CharacterClass.Warrior 1 with Momentum = 1; Flow = 1; Focus = 2 }
  let spentChoices = Program.buildActionChoices spent dummyEnemy
  Assert.DoesNotContain(spentChoices, fun (c: string) -> c.Contains("Insight Cleave"))
  Assert.DoesNotContain(spentChoices, fun (c: string) -> c.Contains("Threaded Opening"))
