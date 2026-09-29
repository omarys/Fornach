module Fornach.Tests.ClassBalanceTests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

[<Fact>]
let ``Physical sub-classes follow exact 1.0 to 0.75 to 0.50 stat ratios`` () =
  let level = 200 // GrandMaster tier

  // 1. Berserker: Power / Agility / Discipline
  let berserker = TierFactory.createClassLevel CharacterClass.Berserker level
  Assert.Equal(841, berserker.GetStat Force)
  Assert.Equal(631, berserker.GetStat Finesse)
  Assert.Equal(420, berserker.GetStat Prowess)

  // 2. Juggernaut: Power / Discipline / Agility
  let juggernaut = TierFactory.createClassLevel CharacterClass.Juggernaut level
  Assert.Equal(841, juggernaut.GetStat Force)
  Assert.Equal(631, juggernaut.GetStat Prowess)
  Assert.Equal(420, juggernaut.GetStat Finesse)

  // 3. Duelist: Agility / Discipline / Power
  let duelist = TierFactory.createClassLevel CharacterClass.Duelist level
  Assert.Equal(841, duelist.GetStat Finesse)
  Assert.Equal(631, duelist.GetStat Prowess)
  Assert.Equal(420, duelist.GetStat Force)

  // 4. Assassin: Agility / Power / Discipline
  let assassin = TierFactory.createClassLevel CharacterClass.Assassin level
  Assert.Equal(841, assassin.GetStat Finesse)
  Assert.Equal(631, assassin.GetStat Force)
  Assert.Equal(420, assassin.GetStat Prowess)

  // 5. Warden: Discipline / Power / Agility
  let warden = TierFactory.createClassLevel CharacterClass.Warden level
  Assert.Equal(841, warden.GetStat Prowess)
  Assert.Equal(631, warden.GetStat Force)
  Assert.Equal(420, warden.GetStat Finesse)

  // 6. Ranger: Discipline / Agility / Power
  let ranger = TierFactory.createClassLevel CharacterClass.Ranger level
  Assert.Equal(841, ranger.GetStat Prowess)
  Assert.Equal(631, ranger.GetStat Finesse)
  Assert.Equal(420, ranger.GetStat Force)

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
let ``ActionResolver computeTierMultiplier is normalized with diminishing returns and soft-capped at 12.0x`` () =
  Assert.Equal(0.0, ActionResolver.computeTierMultiplier 0)
  Assert.Equal(1.50, ActionResolver.computeTierMultiplier 3)
  Assert.Equal(3.50, ActionResolver.computeTierMultiplier 8)

  // NetHits = 18: previously was 9.0x, now capped/smooth at 8.5x
  let mult18 = ActionResolver.computeTierMultiplier 18
  Assert.True(mult18 <= 12.00)

  // NetHits = 36: previously was 18.0x, now strictly soft-capped at 12.00x
  let mult36 = ActionResolver.computeTierMultiplier 36
  Assert.Equal(12.00, mult36)

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
  let gmAssassin = TierFactory.createClassLevel CharacterClass.Assassin level
  let gmBerserker = TierFactory.createClassLevel CharacterClass.Berserker level
  let gmWarden = TierFactory.createClassLevel CharacterClass.Warden level
  let gmMesmer = TierFactory.createClassLevel CharacterClass.Mesmer level

  // Agility archetype: boosted Intuition (offDef = 420) and Acuity (offOff = 317)
  Assert.Equal(420, gmAssassin.GetStat Intuition)
  Assert.Equal(317, gmAssassin.GetStat Acuity)
  Assert.Equal(214, gmAssassin.GetStat Composure)

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
  let gmAssassin = TierFactory.createClassTier CharacterClass.Assassin CombatTier.GrandMaster

  let attack = AttackClassification.FinesseCadence false
  let intent = ActionIntent.StandardAttack attack
  let result = ActionResolver.resolve roller intent gmAssassin gmMesmer

  // Decoy intercepted and shattered
  let shatteredEvent =
    result.Events
    |> List.tryPick (function CombatEvent.MirrorCloneShattered (_, _, blastDmg, _) -> Some blastDmg | _ -> None)
  Assert.True(shatteredEvent.IsSome)
  Assert.True(shatteredEvent.Value > 0)

  // Attacker took retaliatory Morale damage from shatter
  Assert.True(result.Actor.Morale.Current < gmAssassin.Morale.Current)
  // Attacker gained confusion and combo reset from shatter shockwave
  Assert.True(result.Actor.Meters.Confusion.Value > 0)
  Assert.Equal(0, result.Actor.ComboTracker.ConsecutiveHits)
