module Fornach.Tests.ClassBalanceTests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

[<Fact>]
let ``Physical sub-classes follow exact 1.0 to 0.75 to 0.75 stat ratios`` () =
  let level = 200 // GrandMaster tier

  // 1. Berserker: Power Primary (Power 1.0, Agility 0.75, Discipline 0.75)
  let berserker = TierFactory.createClassLevel CharacterClass.Berserker level
  Assert.Equal(841, berserker.GetStat Force)
  Assert.Equal(631, berserker.GetStat Finesse)
  Assert.Equal(631, berserker.GetStat Prowess)

  // 2. Duelist: Agility Primary (Agility 1.0, Discipline 0.75, Power 0.75)
  let duelist = TierFactory.createClassLevel CharacterClass.Duelist level
  Assert.Equal(841, duelist.GetStat Finesse)
  Assert.Equal(631, duelist.GetStat Prowess)
  Assert.Equal(631, duelist.GetStat Force)

  // 3. Warden: Discipline Primary (Discipline 1.0, Power 0.75, Agility 0.75)
  let warden = TierFactory.createClassLevel CharacterClass.Warden level
  Assert.Equal(841, warden.GetStat Prowess)
  Assert.Equal(631, warden.GetStat Force)
  Assert.Equal(631, warden.GetStat Finesse)

[<Fact>]
let ``Magic classes follow 1.0 to 0.75 to 0.75 mental utility ratio`` () =
  let level = 200 // GrandMaster tier

  // Inquisitor: Power Mental Primary, Agility & Discipline Secondary
  let inquisitor = TierFactory.createClassLevel CharacterClass.Inquisitor level
  Assert.Equal(756, inquisitor.GetStat Intellect)
  Assert.Equal(567, inquisitor.GetStat Acuity)
  Assert.Equal(567, inquisitor.GetStat Acumen)

  // Mesmer: Agility Mental Primary, Power & Discipline Secondary
  let mesmer = TierFactory.createClassLevel CharacterClass.Mesmer level
  Assert.Equal(756, mesmer.GetStat Acuity)
  Assert.Equal(567, mesmer.GetStat Intellect)
  Assert.Equal(567, mesmer.GetStat Acumen)

  // Strategist: Discipline Mental Primary, Power & Agility Secondary
  let strategist = TierFactory.createClassLevel CharacterClass.Strategist level
  Assert.Equal(756, strategist.GetStat Acumen)
  Assert.Equal(567, strategist.GetStat Intellect)
  Assert.Equal(567, strategist.GetStat Acuity)

[<Fact>]
let ``Mesmer active Phantasmal Decoy Swap defuses incoming kinetic strike in the Nach`` () =
  let roller : DiceRoller = fun _ _ -> 1 // Guaranteed favorable roll
  let mesmer = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster
  let berserker = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster

  // Berserker attacks with ForceStrike: Wild Blow
  let attack = AttackClassification.ForceStrike true
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent berserker mesmer

  // Assert decoy intercepted
  let swapEvent =
    result.Events
    |> List.tryPick (function CombatEvent.PhantasmalSwapExecuted (_, _, success, _) -> Some success | _ -> None)
  Assert.True(swapEvent.IsSome)
  Assert.True(swapEvent.Value)

  // Mesmer Health must be undamaged!
  Assert.Equal(mesmer.Health.Current, result.Target.Health.Current)
  // Attacker gained confusion and combo reset
  Assert.True(result.Actor.Meters.Confusion.Value > 0)
  Assert.Equal(0, result.Actor.ComboTracker.ConsecutiveHits)

[<Fact>]
let ``Strategist Destabilizing Ground Ward disrupts incoming attacker mid-swing in the Nach`` () =
  let roller : DiceRoller = fun _ _ -> 6 // High roll for Strategist
  let strategist = TierFactory.createClassTier CharacterClass.Strategist CombatTier.GrandMaster
  let berserker = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster

  let attack = AttackClassification.ForceStrike true
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent berserker strategist

  let wardEvent =
    result.Events
    |> List.tryPick (function CombatEvent.DestabilizingWardTriggered (_, _, _, mitig) -> Some mitig | _ -> None)
  Assert.True(wardEvent.IsSome)

[<Fact>]
let ``Inquisitor Synaptic Mind-Shock disrupts attacker focus in the Nach`` () =
  let roller : DiceRoller = fun _ _ -> 6
  let inquisitor = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
  let berserker = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent berserker inquisitor

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
let ``Grandmaster Mesmer fends off Novice Warrior with zero fatigue and zero exhaustion (Spherical Chicken)`` () =
  let roller : DiceRoller = fun _ _ -> 1
  let gmMesmer = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster
  let noviceWarrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent noviceWarrior gmMesmer

  // Passive clone weaving + Phantasmal Decoy Swap both incurred 0 drain due to massive disparity
  Assert.Equal(0, result.Target.Meters.CognitiveFatigue.Value)
  Assert.Equal(0, result.Target.Meters.Exhaustion.Value)
  Assert.Equal(gmMesmer.Health.Current, result.Target.Health.Current)

[<Fact>]
let ``Novice Mesmer facing Novice Warrior incurs disparity-scaled fatigue and exhaustion costs`` () =
  let roller : DiceRoller = fun _ _ -> 1
  let noviceMesmer = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.Novice
  let noviceWarrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent noviceWarrior noviceMesmer

  // Disparity is small (< 30), so both passive weave (+4) and active swap (+5 fatigue, +3 exhaustion) incur standard costs
  Assert.Equal(9, result.Target.Meters.CognitiveFatigue.Value)
  Assert.Equal(3, result.Target.Meters.Exhaustion.Value)
  Assert.Equal(noviceMesmer.Health.Current, result.Target.Health.Current)

[<Fact>]
let ``Grandmaster Abjurer fends off Novice Warrior with zero fatigue and zero exhaustion`` () =
  let roller : DiceRoller = fun _ _ -> 6
  let gmAbjurer = TierFactory.createClassTier CharacterClass.Abjurer CombatTier.GrandMaster
  let noviceWarrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent noviceWarrior gmAbjurer

  Assert.Equal(0, result.Target.Meters.CognitiveFatigue.Value)
  Assert.Equal(0, result.Target.Meters.Exhaustion.Value)
  Assert.True(result.Target.ArcaneWard > 0)

[<Fact>]
let ``Grandmaster Inquisitor fends off Novice Warrior with zero fatigue drain`` () =
  let roller : DiceRoller = fun _ _ -> 6
  let gmInquisitor = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
  let noviceWarrior = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent noviceWarrior gmInquisitor

  Assert.Equal(0, result.Target.Meters.CognitiveFatigue.Value)

[<Fact>]
let ``Physical archetypes receive thematic mental boosts and mages receive poise`` () =
  let level = 200 // GrandMaster tier
  let gmDuelist = TierFactory.createClassLevel CharacterClass.Duelist level
  let gmBerserker = TierFactory.createClassLevel CharacterClass.Berserker level
  let gmWarden = TierFactory.createClassLevel CharacterClass.Warden level
  let gmMesmer = TierFactory.createClassLevel CharacterClass.Mesmer level

  // Agility archetype: boosted Intuition (offDef = 420) and Acuity (offOff = 317)
  Assert.Equal(420, gmDuelist.GetStat Intuition)
  Assert.Equal(317, gmDuelist.GetStat Acuity)
  Assert.Equal(214, gmDuelist.GetStat Composure)

  // Power archetype: boosted Composure (offDef = 420) and Acumen (offOff = 317)
  Assert.Equal(420, gmBerserker.GetStat Composure)
  Assert.Equal(317, gmBerserker.GetStat Acumen)
  Assert.Equal(214, gmBerserker.GetStat Intuition)

  // Discipline archetype: boosted Intuition (offDef = 420) and Resolve (offDef = 420)
  Assert.Equal(420, gmWarden.GetStat Intuition)
  Assert.Equal(420, gmWarden.GetStat Resolve)
  Assert.Equal(214, gmWarden.GetStat Acuity)

  // Magic user: boosted physical Poise (offPoise = 512) for casting stance stability and tertiary Reflex (378) for evasive defense
  Assert.Equal(512, gmMesmer.GetStat Poise)
  Assert.Equal(378, gmMesmer.GetStat Reflex)

[<Fact>]
let ``Mesmer mirror decoy shatters upon being attacked, inflicting retaliatory Morale blast damage`` () =
  let roller : DiceRoller = fun _ _ -> 1
  let gmMesmer = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster
  let gmDuelist = TierFactory.createClassTier CharacterClass.Duelist CombatTier.GrandMaster

  let attack = AttackClassification.FinesseCadence false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent gmDuelist gmMesmer

  // Decoy intercepted and shattered
  let shatteredEvent =
    result.Events
    |> List.tryPick (function CombatEvent.MirrorCloneShattered (_, _, blastDmg, _) -> Some blastDmg | _ -> None)
  Assert.True(shatteredEvent.IsSome)
  Assert.True(shatteredEvent.Value > 0)

  // Attacker took retaliatory Morale damage from shatter
  Assert.True(result.Actor.Morale.Current < gmDuelist.Morale.Current)
  // Attacker gained confusion and combo reset from shatter shockwave
  Assert.True(result.Actor.Meters.Confusion.Value > 0)
  Assert.Equal(0, result.Actor.ComboTracker.ConsecutiveHits)

[<Fact>]
let ``Physical character cannot execute magic attacks and action is blocked`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let berserker = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster
  let mesmer = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster

  let attack = AttackClassification.ArcaneCataclysm false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent berserker mesmer

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
  let mesmer = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster
  let berserker = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent mesmer berserker

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
  let berserker = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster
  let warden = TierFactory.createClassTier CharacterClass.Warden CombatTier.GrandMaster

  let attack = AttackClassification.ForceStrike false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent berserker warden

  Assert.True(result.Contest.IsSome, "Valid physical attack must produce contest.")

[<Fact>]
let ``Magic character successfully executes magic strike and produces contest`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let mesmer = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster
  let berserker = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster

  let attack = AttackClassification.SynapticGlamour false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent mesmer berserker

  Assert.True(result.Contest.IsSome, "Valid magic attack must produce contest.")

[<Fact>]
let ``Mental character can thread complex forms and emits ComplexFormThreaded event`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let inquisitor = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
  let enemy = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster

  let intent = ActionIntent.ThreadComplexForm ComplexForm.AegisLattice
  let result = ActionResolver.resolve roller intent inquisitor enemy

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
  let berserker = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster
  let enemy = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster

  let intent = ActionIntent.ThreadComplexForm ComplexForm.ResonanceSpike
  let result = ActionResolver.resolve roller intent berserker enemy

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
  let inquisitor = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
  let enemy = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster

  let intent = ActionIntent.ShiftStance CombatStance.PowerStance
  let result = ActionResolver.resolve roller intent inquisitor enemy

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
  let inquisitor = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
  let enemy = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster

  // Inquisitor defaults to ResonanceSpike
  Assert.Equal(Some ComplexForm.ResonanceSpike, inquisitor.ComplexForm)

  let intent = ActionIntent.StandardAttack (AttackClassification.ArcaneCataclysm false)
  let result = ActionResolver.resolve roller intent inquisitor enemy

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
  let inquisitor =
    TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
    |> Combatant.setComplexForm (Some ComplexForm.AegisLattice)
    |> fun c -> { c with ArcaneWard = 20 }

  let updated, events = ActionResolver.applyTurnUpkeep inquisitor

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
  let mesmer =
    TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster
    |> fun c -> { c with MirrorClones = 0; ComplexForm = Some ComplexForm.PhantasmalDiffusion }

  let updated, events = ActionResolver.applyTurnUpkeep mesmer

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
  let inquisitor =
    TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
    |> Combatant.setComplexForm (Some ComplexForm.AegisLattice)
  let enemy = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster

  let intent = ActionIntent.StandardAttack (AttackClassification.ArcaneCataclysm true)
  let result = ActionResolver.resolve roller intent inquisitor enemy

  let gambitDeclared =
    result.Events
    |> List.exists (function
      | CombatEvent.GambitDeclared (_, name, _) when name.Contains("Overchanneled") -> true
      | _ -> false)
  Assert.False(gambitDeclared, "Aegis Lattice must prevent Overchannel gambit declaration.")
  Assert.True(result.Actor.Meters.Recklessness.Value < 35, "Must not suffer the +35 Recklessness gambit penalty.")

[<Fact>]
let ``Aegis Lattice reflects 50% damage and 15 Frustration when Arcane Ward absorbs incoming damage`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let berserker = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster
  let inquisitor =
    TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
    |> Combatant.setComplexForm (Some ComplexForm.AegisLattice)
    |> fun c -> { c with ArcaneWard = 100 }

  let intent = ActionIntent.StandardAttack (AttackClassification.ForceStrike false)
  let result = ActionResolver.resolve roller intent berserker inquisitor

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
