namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine

module PreparationTests =

  let fixedRoller (rollVal: int) : DiceRoller =
    fun _ _ -> rollVal

  let createFighterWithClass (charClass: CharacterClass) (level: int) force fort finesse reflex prowess poise intellect resolve acuity intuition acumen composure =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, force; Fortitude, fort
        Finesse, finesse; Reflex, reflex
        Prowess, prowess; Poise, poise
        Intellect, intellect; Resolve, resolve
        Acuity, acuity; Intuition, intuition
        Acumen, acumen; Composure, composure
      ]
    Combatant.createWithClass id "TestFighter" 2000 2000 stats charClass level

  let createStandardFighter (charClass: CharacterClass) (level: int) =
    createFighterWithClass charClass level 50 50 50 50 50 50 50 50 50 50 50 50

  // =========================================================================
  // 1. Progression & Level Scaling Tests
  // =========================================================================

  [<Theory>]
  [<InlineData(45)>]
  [<InlineData(50)>]
  [<InlineData(100)>]
  [<InlineData(200)>]
  [<InlineData(441)>]
  [<InlineData(841)>]
  let ``CalculateMaxPrepUses is strictly limited to 2 uses per encounter`` (primaryStat: int) =
    let actualUses = ProgressionProfile.CalculateMaxPrepUses primaryStat
    Assert.Equal(2, actualUses)

  [<Fact>]
  let ``Combatant created with class initializes correct progression slots and capacity`` () =
    let level = 10
    let justicar = createFighterWithClass CharacterClass.Justicar level 50 50 50 50 200 200 50 50 50 50 50 50
    Assert.Equal(CharacterClass.Justicar, justicar.Class)
    Assert.Equal(level, justicar.Progression.Level)
    Assert.Equal(200, justicar.Progression.PrimaryStat)
    Assert.Equal(2, ProgressionProfile.CalculateMaxPrepUses 200)
    Assert.Equal(2, justicar.Preparations.Length)

    for slot in justicar.Preparations do
      Assert.Equal(2, slot.MaxUses)
      Assert.Equal(2, slot.RemainingUses)

  [<Fact>]
  let ``Spending preparation decrements remaining charges and blocks deployment when exhausted`` () =
    let justicar = createFighterWithClass CharacterClass.Justicar 1 50 50 50 50 100 100 50 50 50 50 50 50 // 2 uses
    let dummy = createStandardFighter CharacterClass.Berserker 1
    let roller = fixedRoller 3

    // Deploy 1st use
    let res1 = ActionResolver.resolve roller (DeployPreparation (PreparationType.BastionZoneControl, None)) justicar dummy
    Assert.True(res1.Actor.HasActivePreparation PreparationType.BastionZoneControl)
    Assert.Equal(1, res1.Actor.Progression.Preparations |> List.find (fun s -> s.Type = PreparationType.BastionZoneControl) |> fun s -> s.RemainingUses)

    // Deploy 2nd use
    let res2 = ActionResolver.resolve roller (DeployPreparation (PreparationType.BastionZoneControl, None)) res1.Actor dummy
    Assert.Equal(0, res2.Actor.Progression.Preparations |> List.find (fun s -> s.Type = PreparationType.BastionZoneControl) |> fun s -> s.RemainingUses)

    // Attempt 3rd use (exhausted)
    let res3 = ActionResolver.resolve roller (DeployPreparation (PreparationType.BastionZoneControl, None)) res2.Actor dummy
    let failedDeploy = res3.Events |> List.exists (function CombatEvent.ComboReset (_, r) when r.Contains("no preparation charges remaining") -> true | _ -> false)
    Assert.True(failedDeploy, "Should emit ComboReset indicating no remaining preparation charges.")

  [<Fact>]
  let ``Replenishing progression profile restores all preparations to maximum capacity`` () =
    let duelist = createFighterWithClass CharacterClass.Duelist 5 50 50 150 150 50 50 50 50 50 50 50 50 // 2 uses
    let spentOnce = duelist |> Combatant.spendPreparation PreparationType.ConcealedBlade
    let slot = spentOnce.Progression.Preparations |> List.find (fun s -> s.Type = PreparationType.ConcealedBlade)
    Assert.Equal(1, slot.RemainingUses)

    let replenishedProfile = spentOnce.Progression.ReplenishAll()
    let repSlot = replenishedProfile.Preparations |> List.find (fun s -> s.Type = PreparationType.ConcealedBlade)
    Assert.Equal(2, repSlot.RemainingUses)

  // =========================================================================
  // 2. Class Archetype & Preparation Mapping Tests
  // =========================================================================

  [<Fact>]
  let ``All 7 player character classes correctly map Vector, Plane, and 2 unique preparations (1 Crowd, 1 Duel)`` () =
    let playerClasses = CharacterClass.PlayerClasses
    Assert.Equal(7, playerClasses.Length)

    for cls in playerClasses do
      Assert.False(cls.IsGeneric)
      Assert.True(cls.IsPlayerClass)
      let preps = PreparationType.ForClass cls
      Assert.Equal(2, preps.Length)

      let crowdPreps = preps |> List.filter (fun p -> p.Category = PreparationCategory.CrowdControl)
      let duelPreps = preps |> List.filter (fun p -> p.Category = PreparationCategory.SingleTargetDuel)

      Assert.Single(crowdPreps) |> ignore
      Assert.Single(duelPreps) |> ignore

      for p in preps do
        Assert.False(String.IsNullOrWhiteSpace(p.Name))
        Assert.False(String.IsNullOrWhiteSpace(p.Description))

  [<Fact>]
  let ``All 4 generic NPC classes (Warrior, Rogue, Soldier, Mage) map correctly and possess zero preparations`` () =
    let genericClasses = CharacterClass.GenericClasses
    Assert.Equal(4, genericClasses.Length)
    Assert.Equal(11, CharacterClass.All.Length)

    for cls in genericClasses do
      Assert.True(cls.IsGeneric)
      Assert.False(cls.IsPlayerClass)
      let preps = PreparationType.ForClass cls
      Assert.Empty(preps)
      Assert.False(String.IsNullOrWhiteSpace(cls.Name))
      Assert.False(String.IsNullOrWhiteSpace(cls.Description))

    // Specific generic class checks
    Assert.Equal(Vector.Power, CharacterClass.Warrior.Vector)
    Assert.Equal(Plane.Physical, CharacterClass.Warrior.Plane)

    Assert.Equal(Vector.Agility, CharacterClass.Rogue.Vector)
    Assert.Equal(Plane.Physical, CharacterClass.Rogue.Plane)

    Assert.Equal(Vector.Discipline, CharacterClass.Soldier.Vector)
    Assert.Equal(Plane.Physical, CharacterClass.Soldier.Plane)

    Assert.Equal(Vector.Power, CharacterClass.Mage.Vector)
    Assert.Equal(Plane.Mental, CharacterClass.Mage.Plane)

  // =========================================================================
  // 3. Multi-Opponent Crowd Control Tests: BastionZoneControl
  // =========================================================================

  [<Fact>]
  let ``BastionZoneControl prevents multi-opponent encirclement penalties beyond 3 simultaneous attackers`` () =
    // Attacker has Force 60, defender Fortitude 30.
    // Fixed roller of 4 so defense hits and attack hits are consistent.
    let roller = fixedRoller 4
    let attacker = createFighterWithClass CharacterClass.Berserker 1 60 40 40 40 40 40 10 10 10 10 10 10
    let baseDefender = createFighterWithClass CharacterClass.Justicar 5 40 30 40 40 40 40 10 10 10 10 10 10

    // Without BastionZoneControl at priorDefenses = 5 (6th attacker in a round)
    let resWithoutBastion = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) attacker baseDefender 5
    let contestWithout = resWithoutBastion.Contest.Value
    Assert.True(contestWithout.EncirclementPenalty > 0, "Defender without Bastion should suffer heavy encirclement penalty against 6th attacker.")

    let penalizedEventEmitted =
      resWithoutBastion.Events
      |> List.exists (function CombatEvent.EncirclementPenalized _ -> true | _ -> false)
    Assert.True(penalizedEventEmitted, "EncirclementPenalized event should be emitted without Bastion.")

    // With BastionZoneControl active on defender, effective prior defenses is capped to 2 (3 simultaneous attackers)
    let bastionActive =
      baseDefender
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.BastionZoneControl None 3)

    let resWithBastion = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) attacker bastionActive 5
    let contestWith = resWithBastion.Contest.Value
    Assert.True(contestWith.EncirclementPenalty < contestWithout.EncirclementPenalty, "Encirclement penalty with Bastion capped to 3 attackers should be smaller than uncapped 6th attacker penalty.")

  // =========================================================================
  // 4. Single-Target Duel Tests: ConcealedBlade & PrismaticFlare
  // =========================================================================

  [<Fact>]
  let ``ConcealedBlade interrupts incoming attack from the Nach with immediate counter-puncture`` () =
    // High finesse defender with ConcealedBlade
    let defender =
      createFighterWithClass CharacterClass.Duelist 5 30 30 100 80 40 40 10 10 10 10 10 10
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.ConcealedBlade None 3)

    // Attacker with lower reflex
    let attacker = createFighterWithClass CharacterClass.Berserker 1 60 50 30 30 40 40 10 10 10 10 10 10

    // Roller 1 ensures reliable puncture and disruption
    let roller = fixedRoller 1
    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) attacker defender

    // Concealed blade should deal damage to attacker and consume preparation
    let counterEvt =
      res.Events
      |> List.tryPick (function CombatEvent.ConcealedBladeCounter (_, _, dmg, disrupted) -> Some (dmg, disrupted) | _ -> None)

    Assert.True(counterEvt.IsSome, "ConcealedBladeCounter event must be emitted.")
    let dmg, disrupted = counterEvt.Value
    Assert.True(dmg > 0, "Concealed blade counter-puncture must deal damage.")
    Assert.True(disrupted, "High finesse disparity should disrupt the incoming strike.")

    // Preparation should have been consumed from defender
    Assert.False(res.Target.HasActivePreparation PreparationType.ConcealedBlade, "Concealed Blade should be consumed after execution.")

    // Since strike was disrupted, attacker strike did not land on defender health
    Assert.Equal(defender.Health.Current, res.Target.Health.Current)

  [<Fact>]
  let ``PrismaticFlare inflicts escalating Morale drain and confusion when target gains Recklessness`` () =
    let attacker = createFighterWithClass CharacterClass.Mesmer 5 30 30 40 40 40 40 60 50 70 50 50 50
    let defender =
      createFighterWithClass CharacterClass.Berserker 1 50 50 30 30 40 40 30 30 30 30 30 30
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.PrismaticFlare None 3)

    let initialMorale = defender.Morale.Current

    // Attacker uses GuileDeception which inflicts Recklessness on defender
    let roller = fixedRoller 6 // ensures hit
    let res = ActionResolver.resolve roller (StandardAttack (GuileDeception false)) attacker defender

    let flareEvt =
      res.Events
      |> List.tryPick (function CombatEvent.PrismaticFlareBlinded (_, reckSpike, drain) -> Some (reckSpike, drain) | _ -> None)

    Assert.True(flareEvt.IsSome, "PrismaticFlareBlinded event must be emitted when target gains Recklessness.")
    let reckSpike, drain = flareEvt.Value
    Assert.True(reckSpike > 0, "Recklessness spike must be positive.")
    Assert.True(drain > 0, "Morale drain must be positive.")
    Assert.True(res.Target.Morale.Current < initialMorale, "Target Morale must be reduced by Prismatic Flare.")
    Assert.True(res.Target.Meters.Confusion.Value >= 20, "Target must suffer confusion from Prismatic Flare glitter burst.")

  // =========================================================================
  // 5. Tactical Timing & Indes Tests: ParryingBuckler
  // =========================================================================

  [<Fact>]
  let ``ParryingBuckler reduces Indes threshold by 1, widening window to seize the Vor`` () =
    let standardJusticar = createStandardFighter CharacterClass.Justicar 1
    Assert.Equal(Indes.defaultThreshold, Indes.calculateThreshold standardJusticar)
    Assert.Equal(3, Indes.calculateThreshold standardJusticar)

    let bucklerJusticar =
      standardJusticar
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.ParryingBuckler None 3)

    Assert.Equal(2, Indes.calculateThreshold bucklerJusticar)

  [<Fact>]
  let ``ParryingBuckler enables seizing the Vor at 2 net defense hits on a whiffed attack`` () =
    // Attacker attacks, but defender defense hits exceed attack hits by 2.
    // With Parrying Buckler, threshold is 2, so IndesSeized should trigger.
    let attacker = createFighterWithClass CharacterClass.Berserker 1 20 20 20 20 20 20 10 10 10 10 10 10
    let defender =
      createFighterWithClass CharacterClass.Justicar 5 40 80 40 40 40 40 10 10 10 10 10 10
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.ParryingBuckler None 3)

    // Roller where defender rolls hits and attacker rolls whiffs
    // Roller returning 4 (hits on Fortitude for TN 4+)
    let roller = fixedRoller 4
    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) attacker defender

    let seizedEvt =
      res.Events
      |> List.tryPick (function CombatEvent.IndesSeized (_, _, thresh, margin) -> Some (thresh, margin) | _ -> None)

    Assert.True(seizedEvt.IsSome, "IndesSeized event should be emitted when threshold is met with Parrying Buckler.")
    let thresh, margin = seizedEvt.Value
    Assert.Equal(2, thresh)
    Assert.True(margin >= thresh)

  // =========================================================================
  // 6. Multi-Opponent Shockwave Slam & Group Turn Tests
  // =========================================================================

  [<Fact>]
  let ``ShockwaveSlam distributes flat surplus damage across all flankers on NetHits 3 or more`` () =
    let berserker =
      createFighterWithClass CharacterClass.Berserker 5 120 80 30 30 40 40 10 10 10 10 10 10
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.ShockwaveSlam None 3)

    let primaryTarget = createStandardFighter CharacterClass.Duelist 1
    let flanker1 = createStandardFighter CharacterClass.Duelist 1
    let flanker2 = createStandardFighter CharacterClass.Duelist 1

    let roller = fixedRoller 6 // Max rolls for NetHits >= 3
    let groupRes =
      ActionResolver.resolveGroupTurn
        roller
        (StandardAttack (ForceStrike false))
        berserker
        primaryTarget
        [ flanker1; flanker2 ]

    let shockwaveEvents =
      groupRes.Events
      |> List.choose (function CombatEvent.ShockwaveSurplusDamage (a, t, excess, dmg) -> Some (t, excess, dmg) | _ -> None)

    Assert.Equal(2, shockwaveEvents.Length)
    for (targetId, excessHits, flatDmg) in shockwaveEvents do
      Assert.True(excessHits >= 1)
      Assert.True(flatDmg >= 15)

  // =========================================================================
  // 7. Tactical Duel Finishers: SocraticDossier & SynapticBrand
  // =========================================================================

  [<Fact>]
  let ``SocraticDossier converts opponent Recklessness directly to unmitigated Morale damage`` () =
    let strategist = createStandardFighter CharacterClass.Strategist 5
    let targetWithReck =
      createStandardFighter CharacterClass.Berserker 1
      |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + 40 })

    let initialMorale = targetWithReck.Morale.Current
    let roller = fixedRoller 3

    let res = ActionResolver.resolve roller (DeployPreparation (PreparationType.SocraticDossier, Some targetWithReck.Id)) strategist targetWithReck

    let dossierEvt =
      res.Events
      |> List.tryPick (function CombatEvent.SocraticDossierExecuted (_, _, reckConv, moraleDmg) -> Some (reckConv, moraleDmg) | _ -> None)

    Assert.True(dossierEvt.IsSome, "SocraticDossierExecuted event must be emitted.")
    let reckConv, moraleDmg = dossierEvt.Value
    Assert.Equal(40, reckConv)
    Assert.Equal(40, moraleDmg)
    Assert.Equal(initialMorale - 40, res.Target.Morale.Current)
    Assert.Equal(0, res.Target.Meters.Recklessness.Value)

  // =========================================================================
  // 8. Tiered Archetypes & Matchup Factory (Novice / Veteran / Master / GrandMaster)
  // =========================================================================


  [<Fact>]
  let ``TierFactory initializes all 44 archetypes across 11 classes and 4 tiers correctly`` () =
    let tiers = [ Novice; Veteran; Master; GrandMaster ]
    let classes = CharacterClass.All

    Assert.Equal(11, classes.Length)

    for cls in classes do
      for tier in tiers do
        let fighter = TierFactory.createClassTier cls tier
        Assert.Equal(cls, fighter.Class)

        let expectedLevel =
          match tier with
          | Novice -> 1
          | Veteran -> 40
          | Master -> 100
          | GrandMaster -> 200

        Assert.Equal(expectedLevel, fighter.Progression.Level)

        if cls.IsGeneric then
          // Generic NPC classes do NOT receive specialized preparations or slots
          Assert.Empty(fighter.Preparations)
          Assert.Equal(0, fighter.Progression.TotalRemainingPrepUses)
        else
          // Player classes receive 2 preparation slots scaled with level
          let expectedPrepUses = ProgressionProfile.CalculateMaxPrepUses fighter.Progression.PrimaryStat
          Assert.Equal(2, fighter.Preparations.Length)
          for slot in fighter.Preparations do
            Assert.Equal(expectedPrepUses, slot.MaxUses)
            Assert.Equal(expectedPrepUses, slot.RemainingUses)

        // Health, Morale, and Armor must be positive and scale with tier
        Assert.True(fighter.Health.Current > 0)
        Assert.True(fighter.Morale.Current > 0)
        Assert.True(fighter.Armor.Current > 0)

  [<Fact>]
  let ``Generic NPC classes cannot deploy specialized player preparations`` () =
    let genericClasses = CharacterClass.GenericClasses
    let roller = fixedRoller 4
    let target = TierFactory.createClassTier CharacterClass.Warrior Novice

    for cls in genericClasses do
      let npc = TierFactory.createClassTier cls Novice
      Assert.Empty(npc.Preparations)
      Assert.Equal(0, npc.Progression.TotalRemainingPrepUses)

      // Attempting to deploy any specialized preparation should fail gracefully with ComboReset
      let attemptDeploy = ActionResolver.resolve roller (DeployPreparation (PreparationType.ShockwaveSlam, None)) npc target
      let resetEvt =
        attemptDeploy.Events
        |> List.tryPick (function CombatEvent.ComboReset (_, r) -> Some r | _ -> None)

      Assert.True(resetEvt.IsSome, sprintf "Generic class %s must not be able to deploy ShockwaveSlam." cls.Name)
      Assert.Contains("no preparation charges remaining", resetEvt.Value)
      // NPC's active preparations remain empty
      Assert.Empty(attemptDeploy.Actor.ActivePreparations)

  // =========================================================================
  // 9. Duel Matchups Across Tiers (1v1)
  // =========================================================================

  [<Fact>]
  let ``Duel: Novice Duelist uses Concealed Blade to interrupt and defuse Novice Warrior strike`` () =
    let duelist =
      TierFactory.createClassTier CharacterClass.Duelist Novice
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.ConcealedBlade None 3)
    let warrior = TierFactory.createClassTier CharacterClass.Warrior Novice
    let roller = fixedRoller 1 // triggers puncture & disruption

    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) warrior duelist

    let counterEvt =
      res.Events
      |> List.tryPick (function CombatEvent.ConcealedBladeCounter (_, _, dmg, disrupted) -> Some (dmg, disrupted) | _ -> None)

    Assert.True(counterEvt.IsSome, "Novice Duelist should trigger Concealed Blade counter-puncture.")
    let dmg, disrupted = counterEvt.Value
    Assert.True(dmg > 0)
    Assert.True(disrupted)
    // Warrior strike was defused; duelist took 0 damage
    Assert.Equal(duelist.Health.Current, res.Target.Health.Current)

  [<Fact>]
  let ``Duel: Veteran Mesmer deploys PrismaticFlare on Veteran Warrior, draining Morale on Recklessness gain`` () =
    let mesmer = TierFactory.createClassTier CharacterClass.Mesmer Veteran
    let warrior = TierFactory.createClassTier CharacterClass.Warrior Veteran
    let roller = fixedRoller 6

    // Mesmer deploys PrismaticFlare
    let deployRes = ActionResolver.resolve roller (DeployPreparation (PreparationType.PrismaticFlare, Some warrior.Id)) mesmer warrior
    let warriorFlared = deployRes.Target

    // Mesmer strikes with GuileDeception, causing Warrior to gain Recklessness
    let attackRes = ActionResolver.resolve roller (StandardAttack (GuileDeception false)) deployRes.Actor warriorFlared

    let flareDrainEvt =
      attackRes.Events
      |> List.tryPick (function CombatEvent.PrismaticFlareBlinded (_, reckSpike, drain) -> Some (reckSpike, drain) | _ -> None)

    Assert.True(flareDrainEvt.IsSome, "Prismatic Flare should drain Morale when Veteran Warrior gains Recklessness.")
    let _, drain = flareDrainEvt.Value
    Assert.True(drain > 0)
    Assert.True(attackRes.Target.Morale.Current < warriorFlared.Morale.Current)

  [<Fact>]
  let ``Duel: Master Justicar with Parrying Buckler seizes Vor in Indes against Master Duelist`` () =
    let justicar =
      TierFactory.createClassTier CharacterClass.Justicar Master
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.ParryingBuckler None 3)
    let duelist = TierFactory.createClassTier CharacterClass.Duelist Master

    // Parrying Buckler reduces Indes threshold from 3 to 2
    Assert.Equal(2, Indes.calculateThreshold justicar)

    // Roller where justicar's high defense hits generate margin of 2+ on whiff
    let mutable rollCount = 0
    let roller _ _ =
      rollCount <- rollCount + 1
      if rollCount <= 10 then 3 else 5
    let res = ActionResolver.resolve roller (StandardAttack (ProwessStrike false)) duelist justicar

    let seizedEvt =
      res.Events
      |> List.tryPick (function CombatEvent.IndesSeized (_, _, thresh, margin) -> Some (thresh, margin) | _ -> None)

    Assert.True(seizedEvt.IsSome, "Master Justicar should seize the Vor in Indes with Parrying Buckler active.")
    let thresh, margin = seizedEvt.Value
    Assert.Equal(2, thresh)
    Assert.True(margin >= 2)

  [<Fact>]
  let ``Duel: Master Inquisitor triggers Synaptic Brand for double Morale damage against Master Warrior`` () =
    let inquisitor = TierFactory.createClassTier CharacterClass.Inquisitor Master
    let warrior = TierFactory.createClassTier CharacterClass.Warrior Master
    let roller = fixedRoller 6 // ensures critical hit

    // Inquisitor marks warrior with Synaptic Brand
    let deployRes = ActionResolver.resolve roller (DeployPreparation (PreparationType.SynapticBrand, Some warrior.Id)) inquisitor warrior
    let warriorBranded = deployRes.Target

    // Inquisitor lands a critical strike with AuthorityDecree
    let attackRes = ActionResolver.resolve roller (StandardAttack (AuthorityDecree true)) deployRes.Actor warriorBranded

    let brandEvt =
      attackRes.Events
      |> List.tryPick (function CombatEvent.SynapticBrandTriggered (_, _, bonusDmg) -> Some bonusDmg | _ -> None)

    Assert.True(brandEvt.IsSome, "Synaptic Brand should detonate upon critical strike.")
    Assert.True(brandEvt.Value > 0)

  [<Fact>]
  let ``Duel: GrandMaster Duelist completely outmatches Novice Warrior via deterministic floor hits`` () =
    // GrandMaster Duelist has 841 Finesse -> 56 guaranteed floor hits (841 / 15)
    // Novice Warrior has 45 Fortitude -> 3 guaranteed floor hits (45 / 15)
    let duelist = TierFactory.createClassTier CharacterClass.Duelist GrandMaster
    let novice = TierFactory.createClassTier CharacterClass.Warrior Novice
    let roller = fixedRoller 4

    let res = ActionResolver.resolve roller (StandardAttack (FinesseCadence false)) duelist novice

    Assert.NotNull(res.Contest)
    let contest = res.Contest.Value
    Assert.False(contest.IsWhiff)
    Assert.True(contest.NetHits >= 10, sprintf "GrandMaster NetHits (%d) should overwhelmingly crush Novice" contest.NetHits)
    Assert.True(res.Target.Health.Current < novice.Health.Current / 2, "Novice should take catastrophic damage from GrandMaster strike.")

  [<Fact>]
  let ``Duel: Novice Warrior Force strike cleanly whiffs against GrandMaster Justicar with 0 Overwhelm`` () =
    // Novice has Force 45; GrandMaster Justicar has Poise 841 & Fortitude 841 (56 floor hits)
    let novice = TierFactory.createClassTier CharacterClass.Warrior Novice
    let grandmaster = TierFactory.createClassTier CharacterClass.Justicar GrandMaster
    let roller = fixedRoller 4

    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) novice grandmaster

    Assert.NotNull(res.Contest)
    Assert.True(res.Contest.Value.IsWhiff, "Novice strike must cleanly whiff against GrandMaster Poise.")
    Assert.Equal(0, res.Target.Meters.Overwhelm.Value)
    Assert.Equal(grandmaster.Health.Current, res.Target.Health.Current)

  [<Fact>]
  let ``Duel: GrandMaster Strategist converts GrandMaster Mage Recklessness to unmitigated Morale damage`` () =
    let strategist = TierFactory.createClassTier CharacterClass.Strategist GrandMaster
    let mage =
      TierFactory.createClassTier CharacterClass.Mage GrandMaster
      |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + 60 })

    let initialMorale = mage.Morale.Current
    let roller = fixedRoller 3

    let res = ActionResolver.resolve roller (DeployPreparation (PreparationType.SocraticDossier, Some mage.Id)) strategist mage

    let dossierEvt =
      res.Events
      |> List.tryPick (function CombatEvent.SocraticDossierExecuted (_, _, reckConv, moraleDmg) -> Some (reckConv, moraleDmg) | _ -> None)

    Assert.True(dossierEvt.IsSome, "Socratic Dossier should convert Recklessness to Morale damage.")
    let reckConv, moraleDmg = dossierEvt.Value
    Assert.Equal(60, reckConv)
    Assert.Equal(60, moraleDmg)
    Assert.Equal(initialMorale - 60, res.Target.Morale.Current)
    Assert.Equal(0, res.Target.Meters.Recklessness.Value)

  [<Fact>]
  let ``Duel: Generic Novice Soldier vs Novice Rogue duels without preparations testing pure martial mechanics`` () =
    let soldier = TierFactory.createClassTier CharacterClass.Soldier Novice
    let rogue = TierFactory.createClassTier CharacterClass.Rogue Novice
    let roller = fixedRoller 4

    Assert.Empty(soldier.Preparations)
    Assert.Empty(rogue.Preparations)

    let res = ActionResolver.resolve roller (StandardAttack (ProwessStrike false)) soldier rogue

    Assert.NotNull(res.Contest)
    // No preparation events should be emitted
    let prepEvents =
      res.Events
      |> List.filter (function
        | CombatEvent.PreparationDeployed _
        | CombatEvent.ConcealedBladeCounter _
        | CombatEvent.PrismaticFlareBlinded _
        | CombatEvent.SynapticBrandTriggered _
        | CombatEvent.RetributionReflected _
        | CombatEvent.DestabilizingWardTripped _ -> true
        | _ -> false)
    Assert.Empty(prepEvents)

  [<Fact>]
  let ``Duel: Generic Veteran Warrior vs Veteran Mage duels across physical and mental planes`` () =
    let warrior = TierFactory.createClassTier CharacterClass.Warrior Veteran
    let mage = TierFactory.createClassTier CharacterClass.Mage Veteran
    let roller = fixedRoller 4

    Assert.Equal(Plane.Physical, warrior.Class.Plane)
    Assert.Equal(Plane.Mental, mage.Class.Plane)
    Assert.Empty(warrior.Preparations)
    Assert.Empty(mage.Preparations)

    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) warrior mage
    Assert.NotNull(res.Contest)
    Assert.True(res.Target.Health.Current < mage.Health.Current, "Mage should take physical damage from Warrior Force strike.")

  [<Fact>]
  let ``Duel: Generic Master Soldier vs Master Duelist duels with high defense poise`` () =
    let soldier = TierFactory.createClassTier CharacterClass.Soldier Master
    let duelist = TierFactory.createClassTier CharacterClass.Duelist Master
    let roller = fixedRoller 4

    // Duelist attacks Soldier with ProwessStrike (duelist secStat vs soldier primStat) -> Whiffs
    let res = ActionResolver.resolve roller (StandardAttack (ProwessStrike false)) duelist soldier
    Assert.NotNull(res.Contest)
    Assert.True(res.Contest.Value.IsWhiff, "Duelist secondary Prowess strike whiffs against Soldier Master Poise.")

  [<Fact>]
  let ``Duel: Generic GrandMaster Warrior vs GrandMaster Soldier duels in clash of brute force and guard`` () =
    let warrior = TierFactory.createClassTier CharacterClass.Warrior GrandMaster
    let soldier = TierFactory.createClassTier CharacterClass.Soldier GrandMaster
    // Attacker rolls high (6s) while defender rolls lower (4s)
    let mutable rollCount = 0
    let roller _ _ =
      rollCount <- rollCount + 1
      if rollCount <= 10 then 6 else 4

    // Warrior Force strike against Soldier Fortitude (both primStat 841)
    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) warrior soldier
    Assert.NotNull(res.Contest)
    Assert.False(res.Contest.Value.IsWhiff, "High rolling GrandMaster Warrior strike overcomes guard.")
    Assert.True(res.Target.Health.Current < soldier.Health.Current)

  // =========================================================================
  // 10. 1 vs N Swarm Matchups Across Tiers
  // =========================================================================

  [<Fact>]
  let ``1vsN: GrandMaster Berserker with Shockwave Slam and Power Cleave decimates 5 Novice Rogues`` () =
    let berserker =
      TierFactory.createClassTier CharacterClass.Berserker GrandMaster
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.ShockwaveSlam None 3)

    let primaryNovice = TierFactory.createClassTier CharacterClass.Rogue Novice
    let flankers = List.init 4 (fun _ -> TierFactory.createClassTier CharacterClass.Rogue Novice)

    let roller = fixedRoller 6 // guarantees NetHits >= 3
    let groupRes =
      ActionResolver.resolveGroupTurn
        roller
        (StandardAttack (ForceStrike false))
        berserker
        primaryNovice
        flankers

    // Shockwave surplus flat damage should hit all 4 secondary flankers
    let shockwaveEvents =
      groupRes.Events
      |> List.choose (function CombatEvent.ShockwaveSurplusDamage (a, t, excess, dmg) -> Some (t, excess, dmg) | _ -> None)

    Assert.Equal(4, shockwaveEvents.Length)
    for (_, excessHits, flatDmg) in shockwaveEvents do
      Assert.True(excessHits >= 1)
      Assert.True(flatDmg >= 15)

    // In Power Stance with Shockwave Slam, Cleave should strike across all flankers (up to 4)
    let cleaveEvents =
      groupRes.Events
      |> List.choose (function CombatEvent.CleaveExecuted (a, t, dmg, reck) -> Some (t, dmg, reck) | _ -> None)

    Assert.True(cleaveEvents.Length >= 2, sprintf "Cleave should strike multiple flankers (Actual: %d)" cleaveEvents.Length)

  [<Fact>]
  let ``1vsN: GrandMaster Justicar with Bastion Zone Control completely negates encirclement penalties against 4 Novice Warriors`` () =
    let justicar =
      TierFactory.createClassTier CharacterClass.Justicar GrandMaster
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.BastionZoneControl None 3)

    let novices = List.init 4 (fun _ -> TierFactory.createClassTier CharacterClass.Warrior Novice)
    let roller = fixedRoller 4

    // 4 Novice Warriors attack in succession
    let mutable currentJusticar = justicar
    for i in 0 .. 3 do
      let novice = novices.[i]
      let res = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) novice currentJusticar i

      // With BastionZoneControl active, effective prior defenses is capped to 0
      match res.Contest with
      | Some contest ->
        Assert.Equal(0, contest.EncirclementPenalty)
      | None ->
        // Strike was intercepted and defused by Attack of Opportunity
        let aooEvt = res.Events |> List.exists (function CombatEvent.AttackOfOpportunityTriggered _ -> true | _ -> false)
        Assert.True(aooEvt, "If contest was bypassed, attack must have been defused by Attack of Opportunity.")

      let penalizedEvt =
        res.Events
        |> List.exists (function CombatEvent.EncirclementPenalized _ -> true | _ -> false)
      Assert.False(penalizedEvt, sprintf "Attack #%d should suffer 0 encirclement penalty under Bastion Zone Control." (i + 1))

      currentJusticar <- res.Target

  [<Fact>]
  let ``1vsN: GrandMaster Duelist with Caltrop Pouch strips flank penalties and uses AoO to defuse 4 Novice Soldiers`` () =
    let duelist =
      TierFactory.createClassTier CharacterClass.Duelist GrandMaster
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.CaltropPouch None 2)

    let noviceFlanker = TierFactory.createClassTier CharacterClass.Soldier Novice
    let roller = fixedRoller 1 // triggers AoO

    // Novice attacks as 3rd flanker (priorDefenses = 2)
    let res = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) noviceFlanker duelist 2

    // Preemptive Attack of Opportunity triggered and defused the incoming flank attack
    let aooEvt =
      res.Events
      |> List.tryPick (function CombatEvent.AttackOfOpportunityTriggered (_, _, vec, dmg, disrupted) -> Some (vec, dmg, disrupted) | _ -> None)

    Assert.True(aooEvt.IsSome, "Attack of Opportunity should trigger on flank attempt.")
    let vec, dmg, disrupted = aooEvt.Value
    Assert.Equal("Agility", vec)
    Assert.True(dmg > 0)
    Assert.True(disrupted, "GrandMaster Agility disparity must disrupt novice flank attack.")
    Assert.True(res.Contest.IsNone, "Attack contest should be bypassed when strike is defused by AoO.")
    Assert.Equal(duelist.Health.Current, res.Target.Health.Current)

    let penalizedEvt =
      res.Events
      |> List.exists (function CombatEvent.EncirclementPenalized _ -> true | _ -> false)
    Assert.False(penalizedEvt, "Caltrop Pouch strips flank penalties; EncirclementPenalized must not be emitted.")

  [<Fact>]
  let ``1vsN: GrandMaster Mesmer with Mirror Mirage confuses flankers and defuses flank attacks from Novice Warriors`` () =
    let mesmer =
      TierFactory.createClassTier CharacterClass.Mesmer GrandMaster
      |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.MirrorMirage None 2)

    let noviceFlanker = TierFactory.createClassTier CharacterClass.Warrior Novice
    let roller = fixedRoller 4

    // Secondary flanker attacks (priorDefenses = 1)
    let res = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) noviceFlanker mesmer 1

    let mirageEvt =
      res.Events
      |> List.tryPick (function CombatEvent.MirrorMirageDeceived (_, _, confusion) -> Some confusion | _ -> None)

    Assert.True(mirageEvt.IsSome, "Flanker should hit a Mirror Mirage phantasm.")
    Assert.Equal(20, mirageEvt.Value)
    Assert.True(res.Actor.Meters.Confusion.Value >= 20, "Flanker should incur +20 Confusion.")
    Assert.Equal(mesmer.Health.Current, res.Target.Health.Current)

  [<Fact>]
  let ``1vsN: GrandMaster Inquisitor with Dread Warhorn inflicts Cognitive Fatigue across entire swarm of 5 Novice Mages`` () =
    let inquisitor = TierFactory.createClassTier CharacterClass.Inquisitor GrandMaster
    let primaryNovice = TierFactory.createClassTier CharacterClass.Mage Novice
    let flankers = List.init 4 (fun _ -> TierFactory.createClassTier CharacterClass.Mage Novice)
    let roller = fixedRoller 3

    let groupRes =
      ActionResolver.resolveGroupTurn
        roller
        (DeployPreparation (PreparationType.DreadWarhorn, None))
        inquisitor
        primaryNovice
        flankers

    // Primary target should have gained Cognitive Fatigue
    Assert.True(groupRes.PrimaryTarget.Meters.CognitiveFatigue.Value >= 25)

    // All 4 secondary flankers must have gained +25 Cognitive Fatigue
    Assert.Equal(4, groupRes.SecondaryTargets.Length)
    for sec in groupRes.SecondaryTargets do
      Assert.True(sec.Meters.CognitiveFatigue.Value >= 25, "Each flanker should suffer +25 Cognitive Fatigue from Dread Warhorn.")

  [<Fact>]
  let ``1vsN: GrandMaster Strategist with Heraldic Treatise generates massive Study Stacks across swarm of 5 Novice Soldiers`` () =
    let strategist = TierFactory.createClassTier CharacterClass.Strategist GrandMaster
    let primaryNovice = TierFactory.createClassTier CharacterClass.Soldier Novice
    let flankers = List.init 4 (fun _ -> TierFactory.createClassTier CharacterClass.Soldier Novice)
    let roller = fixedRoller 3

    let groupRes =
      ActionResolver.resolveGroupTurn
        roller
        (DeployPreparation (PreparationType.HeraldicTreatise, None))
        strategist
        primaryNovice
        flankers

    // Base deployment grants +2 Study Stacks; group deployment across 4 flankers adds +8 (2 per flanker) = 10 total stacks
    Assert.True(groupRes.Actor.StudyStacks >= 10, sprintf "Strategist should gain massive Study Stacks across swarm (Actual: %d)" groupRes.Actor.StudyStacks)

  [<Fact>]
  let ``1vsN Control: Novice Warrior without preparations suffers escalating encirclement against 4 Novice Rogues`` () =
    let noviceDefender = TierFactory.createClassTier CharacterClass.Warrior Novice
    let attacker = TierFactory.createClassTier CharacterClass.Rogue Novice
    let roller = fixedRoller 4

    // Novice attacks against 1st defense (prior = 0) vs 4th defense (prior = 3)
    let res0 = ActionResolver.resolveEx roller (StandardAttack (FinesseCadence false)) attacker noviceDefender 0
    let res3 = ActionResolver.resolveEx roller (StandardAttack (FinesseCadence false)) attacker noviceDefender 3

    let pen0 = res0.Contest.Value.EncirclementPenalty
    let pen3 = res3.Contest.Value.EncirclementPenalty

    Assert.Equal(0, pen0)
    Assert.True(pen3 > 0, "Novice defender without Bastion must suffer significant encirclement penalty at priorDefenses = 3.")
    Assert.True(pen3 >= 10, sprintf "Encirclement penalty should be steep for unassisted novice (Actual: %d)" pen3)

  [<Fact>]
  let ``Caltrop Pouch deploys with 2 turns duration at Novice and scales with tier progression`` () =
    let noviceDuelist = TierFactory.createClassTier CharacterClass.Duelist Novice
    let vetDuelist = TierFactory.createClassTier CharacterClass.Duelist Veteran
    let gmDuelist = TierFactory.createClassTier CharacterClass.Duelist GrandMaster
    let opponent = TierFactory.createClassTier CharacterClass.Soldier Novice
    let roller = fixedRoller 3

    let resNovice = ActionResolver.resolve roller (DeployPreparation (PreparationType.CaltropPouch, None)) noviceDuelist opponent
    let prepNovice = resNovice.Actor.ActivePreparations |> List.find (fun p -> p.Type = PreparationType.CaltropPouch)
    Assert.Equal(2, prepNovice.DurationTurns)

    let resVet = ActionResolver.resolve roller (DeployPreparation (PreparationType.CaltropPouch, None)) vetDuelist opponent
    let prepVet = resVet.Actor.ActivePreparations |> List.find (fun p -> p.Type = PreparationType.CaltropPouch)
    Assert.True(prepVet.DurationTurns > prepNovice.DurationTurns, "Veteran Caltrop Pouch should last longer than Novice.")

    let resGM = ActionResolver.resolve roller (DeployPreparation (PreparationType.CaltropPouch, None)) gmDuelist opponent
    let prepGM = resGM.Actor.ActivePreparations |> List.find (fun p -> p.Type = PreparationType.CaltropPouch)
    Assert.True(prepGM.DurationTurns > prepVet.DurationTurns, "Grandmaster Caltrop Pouch should scale even further.")

  [<Fact>]
  let ``Berserker always cleaves adjacent targets on physical hit regardless of stance`` () =
    // Even shifted into Agility Stance, a Berserker inherently cleaves adjacent foes
    let berserker = { TierFactory.createClassTier CharacterClass.Berserker Veteran with Stance = CombatStance.AgilityStance }
    let primary = TierFactory.createClassTier CharacterClass.Warrior Novice
    let flanker1 = TierFactory.createClassTier CharacterClass.Warrior Novice
    let flanker2 = TierFactory.createClassTier CharacterClass.Warrior Novice
    let roller = fixedRoller 4 // Lands hit

    let groupRes = ActionResolver.resolveGroupTurn roller (StandardAttack (ForceStrike false)) berserker primary [ flanker1; flanker2 ]

    let cleaveEvents =
      groupRes.Events
      |> List.choose (function CombatEvent.CleaveExecuted (a, t, dmg, reck) -> Some (t, dmg, reck) | _ -> None)

    Assert.Equal(2, cleaveEvents.Length)
    Assert.True(groupRes.SecondaryTargets.[0].Health.Current < flanker1.Health.Current)
    Assert.True(groupRes.SecondaryTargets.[1].Health.Current < flanker2.Health.Current)

  [<Fact>]
  let ``Mental attacks with severe stat disparity inflict cranial hemorrhage bleed stacks`` () =
    let grandmaster = TierFactory.createClassTier CharacterClass.Inquisitor GrandMaster
    let novice = TierFactory.createClassTier CharacterClass.Warrior Novice
    let roller = fixedRoller 4

    let res = ActionResolver.resolve roller (StandardAttack (ArcaneCataclysm false)) grandmaster novice

    let hemorrhageEvt =
      res.Events
      |> List.tryPick (function CombatEvent.PsychicHemorrhageInflicted (_, _, stacks, disp) -> Some (stacks, disp) | _ -> None)

    Assert.True(hemorrhageEvt.IsSome, "PsychicHemorrhageInflicted event must be emitted on severe mental disparity.")
    let stacks, disp = hemorrhageEvt.Value
    Assert.True(stacks >= 2, sprintf "Expected at least 2 bleed stacks, got %d" stacks)
    Assert.True(disp >= 100, sprintf "Disparity should be huge (Actual: %d)" disp)
    Assert.True(res.Target.BleedStacks >= 2, "Defender must possess BleedStacks from cranial rupture.")

  [<Fact>]
  let ``Mesmer and Strategist attacks inflict heavy disparity-scaled Recklessness on opponents`` () =
    let mesmer = TierFactory.createClassTier CharacterClass.Mesmer GrandMaster
    let strategist = TierFactory.createClassTier CharacterClass.Strategist GrandMaster
    let noviceSoldier = TierFactory.createClassTier CharacterClass.Soldier Novice
    let roller = fixedRoller 4

    // Mesmer Synaptic Glamour
    let resMesmer = ActionResolver.resolve roller (StandardAttack (SynapticGlamour false)) mesmer noviceSoldier
    Assert.True(resMesmer.Target.Meters.Recklessness.Value >= 40,
      sprintf "Mesmer should inflict heavy Recklessness with GrandMaster disparity (Actual: %d)" resMesmer.Target.Meters.Recklessness.Value)

    // Strategist Acumen Interrogation
    let resStrat = ActionResolver.resolve roller (StandardAttack (AcumenInterrogation false)) strategist noviceSoldier
    Assert.True(resStrat.Target.Meters.Recklessness.Value >= 50,
      sprintf "Strategist should inflict massive Recklessness with GrandMaster disparity (Actual: %d)" resStrat.Target.Meters.Recklessness.Value)

  [<Fact>]
  let ``Mental attacks resonate Area of Effect psychic shockwave to adjacent flankers`` () =
    let mesmer = TierFactory.createClassTier CharacterClass.Mesmer GrandMaster
    let primary = TierFactory.createClassTier CharacterClass.Soldier Novice
    let flanker1 = TierFactory.createClassTier CharacterClass.Soldier Novice
    let flanker2 = TierFactory.createClassTier CharacterClass.Soldier Novice
    let roller = fixedRoller 4

    let groupRes = ActionResolver.resolveGroupTurn roller (StandardAttack (SynapticGlamour false)) mesmer primary [ flanker1; flanker2 ]

    let resonanceEvts =
      groupRes.Events
      |> List.choose (function CombatEvent.PsychicShockwaveResonated (c, t, dmg) -> Some (t, dmg) | _ -> None)

    Assert.Equal(2, resonanceEvts.Length)
    Assert.True(groupRes.SecondaryTargets.[0].Morale.Current < flanker1.Morale.Current, "Secondary flanker 1 should take psychic splash damage.")
    Assert.True(groupRes.SecondaryTargets.[1].Morale.Current < flanker2.Morale.Current, "Secondary flanker 2 should take psychic splash damage.")

  [<Fact>]
  let ``AegisOfRetribution reduces incoming damage taken by 35% and reflects 50% back as retribution with Frustration`` () =
    let abjurer = TierFactory.createClassTier CharacterClass.Abjurer Veteran
    let ally = TierFactory.createClassTier CharacterClass.Soldier Veteran
    let attacker = TierFactory.createClassTier CharacterClass.Warrior Veteran
    let roller = fixedRoller 4

    // 1. Abjurer deploys Aegis of Retribution onto ally
    let deployRes = ActionResolver.resolve roller (DeployPreparation (PreparationType.AegisOfRetribution, Some ally.Id)) abjurer ally
    let shieldedAlly = deployRes.Target
    Assert.True(shieldedAlly.HasActivePreparation PreparationType.AegisOfRetribution)

    // Attacker strikes shielded ally with ForceStrike
    let attackRes = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) attacker shieldedAlly

    let reflectEvt =
      attackRes.Events
      |> List.tryPick (function CombatEvent.RetributionReflected (_, _, dmg, frust) -> Some (dmg, frust) | _ -> None)

    Assert.True(reflectEvt.IsSome, "RetributionReflected event must be emitted when attacker strikes target with Aegis of Retribution.")
    let dmg, frust = reflectEvt.Value
    Assert.True(dmg > 0, "Reflected retribution damage must be positive.")
    Assert.True(frust >= 15, "Frustration inflicted on attacker must be >= 15.")
    Assert.True(attackRes.Actor.Meters.Frustration.Value >= 15, "Attacker Frustration meter must have increased.")
    Assert.True(attackRes.Target.Health.Current < ally.Health.Current, "Ally should take damage, but reduced by 35%.")

    // 2. Abjurer deploys Aegis of Retribution on self; flanker beyond ground wards (priorDefenses = 5) strikes
    let deploySelfRes = ActionResolver.resolve roller (DeployPreparation (PreparationType.AegisOfRetribution, None)) abjurer attacker
    let abjurerShielded = deploySelfRes.Actor
    let abjurerAttackRes = ActionResolver.resolveEx roller (StandardAttack (ForceStrike false)) attacker abjurerShielded 5
    let selfReflectEvt =
      abjurerAttackRes.Events
      |> List.tryPick (function CombatEvent.RetributionReflected (_, _, d, f) -> Some (d, f) | _ -> None)
    Assert.True(selfReflectEvt.IsSome, "RetributionReflected event must be emitted when flanker strikes Abjurer directly.")

  [<Fact>]
  let ``BerserkTincture mitigates incoming physical damage by 35% and emits EnrageDamageShrugged`` () =
    let berserker = TierFactory.createClassTier CharacterClass.Berserker Veteran
    let attacker = TierFactory.createClassTier CharacterClass.Warrior Master
    let roller = fixedRoller 4

    // Deploy Berserk Tincture (consume potion, lose health, gain Recklessness + active preparation)
    let deployRes = ActionResolver.resolve roller (DeployPreparation (PreparationType.BerserkTincture, None)) berserker attacker
    let enragedBerserker = deployRes.Actor
    Assert.True(enragedBerserker.HasActivePreparation PreparationType.BerserkTincture)

    // Attacker strikes enraged Berserker with ForceStrike
    let attackRes = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) attacker enragedBerserker

    let shrugEvt =
      attackRes.Events
      |> List.tryPick (function CombatEvent.EnrageDamageShrugged (_, dmgIgnored) -> Some dmgIgnored | _ -> None)

    Assert.True(shrugEvt.IsSome, "EnrageDamageShrugged event must be emitted when physical damage hits an enraged Berserker.")
    Assert.True(shrugEvt.Value > 0, "Ignored physical damage must be positive.")

  [<Fact>]
  let ``Grandmaster Berserker cleaves up to 5 adjacent targets with massive Force and Shockwave synergy`` () =
    let gmBerserker = TierFactory.createClassTier CharacterClass.Berserker GrandMaster
    let primaryTarget = TierFactory.createClassTier CharacterClass.Soldier Novice
    let flankers = [ for i in 1 .. 6 -> TierFactory.createClassTier CharacterClass.Soldier Novice ]
    let roller = fixedRoller 5

    // GM Berserker executes ForceStrike against primary target with 6 adjacent flankers
    let groupRes = ActionResolver.resolveGroupTurn roller (StandardAttack (ForceStrike false)) gmBerserker primaryTarget flankers

    let cleaveHits =
      groupRes.Events
      |> List.choose (function CombatEvent.CleaveExecuted (_, tid, dmg, _) -> Some (tid, dmg) | _ -> None)

    // Grandmaster Force (~160) gives min(5, max(2, 160/35)) = 4 or 5 targets
    Assert.True(cleaveHits.Length >= 4, sprintf "Grandmaster Berserker should cleave at least 4 adjacent targets (Actual: %d)" cleaveHits.Length)
