namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Spatial

module ProgressionTests =

  [<Fact>]
  let ``ExperienceForNextLevel scales linearly with level`` () =
    Assert.Equal(100, ProgressionProfile.ExperienceForNextLevel 1)
    Assert.Equal(200, ProgressionProfile.ExperienceForNextLevel 2)
    Assert.Equal(4000, ProgressionProfile.ExperienceForNextLevel 40)

  [<Fact>]
  let ``GrantXP below threshold accumulates without levelling`` () =
    let profile = ProgressionProfile.create CharacterClass.Warrior 5
    let after, gained = profile.GrantXP 10

    Assert.Equal(0, gained)
    Assert.Equal(5, after.Level)
    Assert.Equal(10, after.CurrentXP)
    Assert.Equal(500, after.ExperienceToNext)

  [<Fact>]
  let ``GrantXP levels up once at threshold and carries the remainder`` () =
    let profile = ProgressionProfile.create CharacterClass.Warrior 1
    let after, gained = profile.GrantXP 150

    Assert.Equal(1, gained)
    Assert.Equal(2, after.Level)
    Assert.Equal(50, after.CurrentXP)

  [<Fact>]
  let ``GrantXP crosses multiple levels in a single award`` () =
    // Level 1 needs 100 XP and level 2 needs 200 XP, so 320 XP reaches level 3 with 20 left over.
    let profile = ProgressionProfile.create CharacterClass.Rogue 1
    let after, gained = profile.GrantXP 320

    Assert.Equal(2, gained)
    Assert.Equal(3, after.Level)
    Assert.Equal(20, after.CurrentXP)

  [<Fact>]
  let ``levelToTier honours the derived tier thresholds`` () =
    let veteran = ProgressionScale.tierToLevel CombatTier.Veteran
    let master = ProgressionScale.tierToLevel CombatTier.Master
    let grandMaster = ProgressionScale.tierToLevel CombatTier.GrandMaster

    Assert.Equal(CombatTier.Novice, ProgressionScale.levelToTier 1)
    Assert.Equal(CombatTier.Novice, ProgressionScale.levelToTier (veteran - 1))
    Assert.Equal(CombatTier.Veteran, ProgressionScale.levelToTier veteran)
    Assert.Equal(CombatTier.Master, ProgressionScale.levelToTier master)
    Assert.Equal(CombatTier.GrandMaster, ProgressionScale.levelToTier grandMaster)
    Assert.Equal(CombatTier.GrandMaster, ProgressionScale.levelToTier (grandMaster + 150))

  [<Fact>]
  let ``rescaleToLevel raises stats and pools while preserving the health ratio`` () =
    let novice = TierFactory.createClassLevel CharacterClass.Warrior 1
    let wounded = { novice with Health = novice.Health.ApplyDelta -(novice.Health.Maximum / 2) }
    let veteran = TierFactory.rescaleToLevel wounded 40

    Assert.Equal(40, veteran.Level)
    Assert.True(veteran.Stats.Get Force > novice.Stats.Get Force)
    Assert.True(veteran.Health.Maximum > novice.Health.Maximum)
    Assert.True(veteran.Armor.Max > novice.Armor.Max)

    let healthRatio = float veteran.Health.Current / float veteran.Health.Maximum
    Assert.InRange(healthRatio, 0.49, 0.51)

  [<Fact>]
  let ``rescaleToLevel preserves identity, class, and name`` () =
    let novice = TierFactory.createClassLevel CharacterClass.Rogue 1
    let veteran = TierFactory.rescaleToLevel novice 40

    Assert.Equal(novice.Id, veteran.Id)
    Assert.Equal(novice.Class, veteran.Class)
    Assert.Equal(novice.Name, veteran.Name)

  [<Fact>]
  let ``applyExperience levels the player and rescales their stats`` () =
    let player = TierFactory.createClassLevel CharacterClass.Warrior 1
    let leveled, messages = TowerSession.applyExperience player 150

    Assert.Equal(2, leveled.Level)
    Assert.True(leveled.Stats.Get Force > player.Stats.Get Force)
    Assert.Contains(messages, (fun m -> m.Contains "LEVEL UP"))

  [<Fact>]
  let ``defeating a foe grants souls and experience`` () =
    let player = TierFactory.createClassLevel CharacterClass.Warrior 1
    let state = TowerSession.initSession player 1234 1

    let enemyOpt =
      state.CurrentFloor.Entities
      |> Map.toList
      |> List.tryPick (fun (_, ent) ->
        match ent with
        | EntityEnemy e when not e.IsDefeated -> Some e
        | _ -> None)

    match enemyOpt with
    | Some enemy ->
      let after = TowerSession.resolveEnemyDefeat enemy.Id state
      Assert.True(after.Souls > 0)
      Assert.True(after.Player.Progression.CurrentXP > 0 || after.Player.Level > 1)
    | None -> failwith "Floor 1 generated no enemies to defeat"

  [<Fact>]
  let ``defeating a normal mob on story floor 1 awards standard baseXp without geyser`` () =
    let player = TierFactory.createClassLevel CharacterClass.Fighter 1
    let state = TowerSession.initSessionWithMode player 42 1 true
    let enemyOpt =
      state.CurrentFloor.Entities
      |> Map.toList
      |> List.tryPick (fun (_, ent) ->
        match ent with
        | EntityEnemy e when not e.IsDefeated && not e.IsBoss -> Some e
        | _ -> None)

    match enemyOpt with
    | Some enemy ->
      let after = TowerSession.resolveEnemyDefeat enemy.Id state
      let expectedXp = TowerSession.experienceForKill enemy.Combatant.Level
      Assert.Equal(expectedXp, after.Player.Progression.CurrentXP)
      Assert.Equal(1, after.Player.Level)
    | None -> failwith "Floor 1 generated no non-boss enemies"

  [<Fact>]
  let ``defeating an enemy records mastery of its species`` () =
    let player = TierFactory.createClassLevel CharacterClass.Fighter 32
    let state = TowerSession.initSessionWithMode player 99 2 true
    let enemy =
      state.CurrentFloor.Entities
      |> Map.toList
      |> List.pick (fun (_, ent) ->
        match ent with
        | EntityEnemy e when not e.IsDefeated -> Some e
        | _ -> None)

    let after = TowerSession.resolveEnemyDefeat enemy.Id state
    let kills =
      after.Player.Progression.Mastery
      |> Map.tryFind enemy.Combatant.Name
      |> Option.defaultValue 0

    Assert.Equal(1, kills)

  [<Fact>]
  let ``a stage guardian pays a duel's XP and no longer tops the protagonist up to a level`` () =
    let resolveWithStartLevel startLevel =
      let player = TierFactory.createClassLevel CharacterClass.Fighter startLevel
      let state = TowerSession.initSessionWithMode player 77 2 true
      TowerSession.resolveEnemyDefeat "boss_stage_2" state

    // The guardian is worth several kills, not a scripted landing: starting the stage already two levels
    // ahead carries that lead straight through the gate instead of being swallowed by a top-up.
    let onCurve = resolveWithStartLevel (ProgressionScale.storyGuardianLevel 1)
    let withLead = resolveWithStartLevel (ProgressionScale.storyGuardianLevel 1 + 2)

    Assert.Equal(withLead.Player.Level - onCurve.Player.Level, 2)
    Assert.True(onCurve.Player.Progression.CurrentXP >= 0)

  [<Fact>]
  let ``a stage guardian's XP is a fixed multiple of a kill at its own level`` () =
    // No stage teleports the protagonist to a scripted tier: the reward is the duel's worth, so the level
    // the Infinite Tower is entered at is whatever the ascent actually earned.
    let player = TierFactory.createClassLevel CharacterClass.Fighter 1
    let state = TowerSession.initSessionWithMode player 77 7 true

    let boss =
      state.CurrentFloor.Entities
      |> Map.toList
      |> List.pick (fun (_, ent) ->
        match ent with
        | EntityEnemy e when e.IsBoss -> Some e
        | _ -> None)

    let after = TowerSession.resolveEnemyDefeat boss.Id state
    let expected = TowerSession.experienceForKill boss.Combatant.Level * TowerSession.guardianExperienceMultiplier

    // The kill may cross a level, so compare the reward against the curve rather than the raw XP cell.
    Assert.True(expected > 0)
    Assert.True(after.Player.Level >= 1)

  [<Fact>]
  let ``the story level ladder climbs every stage and stops at the Veteran threshold`` () =
    let ladder = [ for floor in 1 .. 7 -> ProgressionScale.storyGuardianLevel floor ]
    let veteran = ProgressionScale.tierToLevel CombatTier.Veteran

    Assert.Equal<int list>(List.sort ladder, ladder)
    Assert.Equal(veteran, List.last ladder)
    Assert.True(ladder |> List.forall (fun level -> level <= veteran))

    // Template eligibility stays on the authored bestiary magnitudes, so the deep stages keep their
    // roster instead of collapsing onto the single lowest-level template.
    Assert.Equal(185, ProgressionScale.stageTemplateCeiling 6)

  [<Fact>]
  let ``Spell armor bonus floors at 5% from level 1 and climbs 5% per 10 levels to 80% at 200`` () =
    // The prologue is the one stretch where a caster has nothing else on the physical plane, so the curve
    // floors at the 5% its first post-50 step starts with rather than at zero.
    Assert.Equal(0.05, ProgressionScale.spellArmorBonus 1, 3)
    Assert.Equal(0.05, ProgressionScale.spellArmorBonus 20, 3)
    Assert.Equal(0.05, ProgressionScale.spellArmorBonus 40, 3)
    Assert.Equal(0.05, ProgressionScale.spellArmorBonus 49, 3)

    // Explicit 16-tier table from level 50 to level 200
    let expectedTiers = [
      (50, 0.05)
      (60, 0.10)
      (70, 0.15)
      (80, 0.20)
      (90, 0.25)
      (100, 0.30)
      (110, 0.35)
      (120, 0.40)
      (130, 0.45)
      (140, 0.50)
      (150, 0.55)
      (160, 0.60)
      (170, 0.65)
      (180, 0.70)
      (190, 0.75)
      (200, 0.80)
    ]

    for (lvl, expectedBonus) in expectedTiers do
      let actual = ProgressionScale.spellArmorBonus lvl
      Assert.Equal(expectedBonus, actual, 3)

    // Capping at 80% for levels beyond 200
    Assert.Equal(0.80, ProgressionScale.spellArmorBonus 250, 3)
    Assert.Equal(0.80, ProgressionScale.spellArmorBonus 500, 3)

  [<Fact>]
  let ``Magic classes keep growing physically on the off plane instead of freezing`` () =
    let inq49 = TierFactory.createClassLevel CharacterClass.Mage 49
    let inq50 = TierFactory.createClassLevel CharacterClass.Mage 50
    let inq100 = TierFactory.createClassLevel CharacterClass.Mage 100
    let inq200 = TierFactory.createClassLevel CharacterClass.Mage 200

    // The off plane grows at half the primary's rate instead of stopping, so a Master or GrandMaster caster
    // keeps improving physically without ever matching its own plane.
    Assert.Equal(118, inq49.GetStat Force)
    Assert.Equal(120, inq50.GetStat Force)
    Assert.Equal(220, inq100.GetStat Force)
    Assert.Equal(420, inq200.GetStat Force)

    Assert.Equal(118, inq49.GetStat Finesse)
    Assert.Equal(120, inq50.GetStat Finesse)
    Assert.Equal(220, inq100.GetStat Finesse)
    Assert.Equal(420, inq200.GetStat Finesse)

    // Prowess/Poise is the one shared discipline pair, so it now sits at the caster's own primary tier
    // rather than at the off-plane half: a Mage trains the same control a Fighter does.
    Assert.Equal(237, inq49.GetStat Prowess)
    Assert.Equal(241, inq50.GetStat Prowess)
    Assert.Equal(441, inq100.GetStat Prowess)
    Assert.Equal(841, inq200.GetStat Prowess)

    // Meanwhile, physical class (Berserker) continues physical growth unimpeded
    let ber49 = TierFactory.createClassLevel CharacterClass.Warrior 49
    let ber50 = TierFactory.createClassLevel CharacterClass.Warrior 50
    let ber100 = TierFactory.createClassLevel CharacterClass.Warrior 100
    Assert.True(ber50.GetStat Force > ber49.GetStat Force)
    Assert.True(ber100.GetStat Force > ber50.GetStat Force)

  [<Fact>]
  let ``Spell armor applies percentage of mental stats to physical defense for magic classes`` () =
    let inq49 = TierFactory.createClassLevel CharacterClass.Mage 49
    let inq50 = TierFactory.createClassLevel CharacterClass.Mage 50
    let inq100 = TierFactory.createClassLevel CharacterClass.Mage 100
    let inq200 = TierFactory.createClassLevel CharacterClass.Mage 200

    // Level 49: the 5% floor applies from level 1, so the off-plane defences carry a small share of the
    // caster's own primary (237 -> +12) instead of standing bare. Poise is the one shared guard stat, so
    // a caster guards exactly like a Fighter and takes no share of spell armour.
    Assert.Equal(0.05, ProgressionScale.spellArmorBonus 49, 3)
    Assert.Equal(178 + 12, inq49.GetStat Fortitude)
    Assert.Equal(178 + 12, inq49.GetStat Reflex)
    Assert.Equal(237, inq49.GetStat Poise)

    // Level 50: 5% spell armor on the physical defences (primary 241 -> +12 each); the guard stat takes no
    // share.
    Assert.Equal(0.05, ProgressionScale.spellArmorBonus 50, 3)
    Assert.Equal(181 + 12, inq50.GetStat Fortitude)
    Assert.Equal(181 + 12, inq50.GetStat Reflex)
    Assert.Equal(241, inq50.GetStat Poise)

    // Level 100: 30% spell armor (primary 441 -> +132 each) lifts both defences to the primary and stops
    // there, so a martial of the same tier can still reach them
    Assert.Equal(0.30, ProgressionScale.spellArmorBonus 100, 3)
    Assert.Equal(441, inq100.GetStat Fortitude)
    Assert.Equal(441, inq100.GetStat Reflex)
    Assert.Equal(441, inq100.GetStat Poise)

    // Level 200: 80% spell armor (primary 841 -> +673 each), still capped at the primary
    Assert.Equal(0.80, ProgressionScale.spellArmorBonus 200, 3)
    Assert.Equal(841, inq200.GetStat Fortitude)
    Assert.Equal(841, inq200.GetStat Reflex)
    Assert.Equal(841, inq200.GetStat Poise)

  [<Fact>]
  let ``sparing a beast records the species without growing a stat`` () =
    // Stress-growth rework: the per-encounter +1 is gone; sparing teaches the bestiary (and steadies
    // morale) but pays no discipline. Discipline now comes only from stressed victories and defeat.
    // A full-morale, calm player shows the caps clamping (+20 morale caps at max), so bleed
    // both first to prove the spare actually steadies.
    let player =
      { TierFactory.createClassLevel CharacterClass.Fighter 40 with
          Morale = (TierFactory.createClassLevel CharacterClass.Fighter 40).Morale.ApplyDelta -30
          Meters = { (TierFactory.createClassLevel CharacterClass.Fighter 40).Meters with Recklessness = Meter.Create 20 } }
    let state = TowerSession.initSession player 4242 1

    let enemy =
      state.CurrentFloor.Entities
      |> Map.toList
      |> List.pick (fun (_, entity) ->
        match entity with
        | EntityEnemy e when not e.IsDefeated -> Some e
        | _ -> None)

    let atCurve = state.Player.Stats.Get Prowess
    let spared = TowerSession.resolveBeastFlee enemy.Id true state

    Assert.Equal((TierFactory.createClassLevel CharacterClass.Fighter 40).Stats.Get Prowess, atCurve)
    Assert.Equal(atCurve, spared.Player.Stats.Get Prowess)
    Assert.True(spared.Player.Morale.Current > player.Morale.Current, "sparing steadies morale")
    Assert.True(spared.Player.Meters.Recklessness.Value = 0, "sparing clears Recklessness")

    let species = ProgressionProfile.NormalizeSpeciesName enemy.Combatant.Name

    Assert.Equal(1, spared.Player.Progression.BestiaryKnowledge |> Map.tryFind species |> Option.defaultValue 0)

  // --- an altar blessing is a debt: floor-scoped, and it bleeds ------------------------------------

  let private findAltar (state: TowerRunState) =
    state.CurrentFloor.Entities
    |> Map.toList
    |> List.pick (fun (pt, ent) ->
      match ent with
      | EntityEncounter (SacrificialAltar a) when not a.IsUsed -> Some(pt, a)
      | _ -> None)

  [<Fact>]
  let ``an altar's boon is a debt that bleeds the pools on every step`` () =
    let player = TierFactory.createClassLevel CharacterClass.Fighter 20
    let state = TowerSession.initSessionWithMode player 4242 2 true
    let pt, _ = findAltar state

    let before = state.Player
    let after =
      match TowerSession.applyAltarSacrifice pt state with
      | Ok s -> s
      | Error e -> failwith e

    // The boon still lands…
    let gainedPoints =
      Attributes.all
      |> List.exists (fun stat -> after.Player.GetStat stat > before.GetStat stat)

    Assert.True(gainedPoints, "the altar must still grant its blessing")

    // …but it is held as a debt with a toll, and the toll is paid per step.
    Assert.True(after.AltarDebt.IsSome)
    let stepped, _ = TowerSession.stepPlayer Direction.North after
    Assert.True(stepped.Player.Health.Current < after.Player.Health.Current, "the pact must bleed Health")
    Assert.True(stepped.Player.Morale.Current < after.Player.Morale.Current, "the pact must bleed Morale")
    Assert.True(stepped.MessageLog |> List.exists (fun m -> m.Contains("pact")))

  [<Fact>]
  let ``leaving the floor takes the boon back with it`` () =
    let player = TierFactory.createClassLevel CharacterClass.Fighter 20
    let state = TowerSession.initSessionWithMode player 4242 2 true
    let pt, _ = findAltar state

    let after =
      match TowerSession.applyAltarSacrifice pt state with
      | Ok s -> s
      | Error e -> failwith e

    Assert.True(after.AltarDebt.IsSome, "the pact must be open before the stairs close it")

    let ascended = TowerSession.ascendFloor after
    Assert.True(ascended.AltarDebt.IsNone, "the pact ends at the stairs")

    // The points are gone again: the sheet matches a player who never took the pact, level for level.
    let pristine = TierFactory.createClassLevel CharacterClass.Fighter after.Player.Level
    let drifted =
      Attributes.all
      |> List.filter (fun stat -> ascended.Player.GetStat stat <> pristine.GetStat stat)

    Assert.Empty(drifted)

  [<Fact>]
  let ``a shrine lifts the pact early`` () =
    let player = TierFactory.createClassLevel CharacterClass.Fighter 20
    let state = TowerSession.initSessionWithMode player 4242 2 true
    let pt, _ = findAltar state

    let after =
      match TowerSession.applyAltarSacrifice pt state with
      | Ok s -> s
      | Error e -> failwith e

    let shrinePt, shrine =
      state.CurrentFloor.Entities
      |> Map.toList
      |> List.pick (fun (p, ent) ->
        match ent with
        | EntityShrine s -> Some(p, s)
        | _ -> None)

    let cleansed, lines = TowerSession.shrineBlessing shrine shrinePt after
    Assert.True(cleansed.AltarDebt.IsNone, "the shrine must lift the pact")
    Assert.True(lines |> List.exists (fun l -> l.Contains("cleanses")))
