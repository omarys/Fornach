namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

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
  let ``levelToTier honours canonical tier thresholds`` () =
    Assert.Equal(CombatTier.Novice, ProgressionScale.levelToTier 1)
    Assert.Equal(CombatTier.Novice, ProgressionScale.levelToTier 39)
    Assert.Equal(CombatTier.Veteran, ProgressionScale.levelToTier 40)
    Assert.Equal(CombatTier.Master, ProgressionScale.levelToTier 100)
    Assert.Equal(CombatTier.GrandMaster, ProgressionScale.levelToTier 200)
    Assert.Equal(CombatTier.GrandMaster, ProgressionScale.levelToTier 350)

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
      let expectedXp = enemy.Combatant.Level * 10 + 25
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
  let ``optional kills carry their level lead through the stage gate`` () =
    let resolveWithStartLevel startLevel =
      let player = TierFactory.createClassLevel CharacterClass.Fighter startLevel
      let state = TowerSession.initSessionWithMode player 77 2 true
      TowerSession.resolveEnemyDefeat "boss_stage_2" state

    // Floor 2's guardian hands over the level floor 3 assumes.
    let bossOnly = resolveWithStartLevel (ProgressionScale.storyGuardianLevel 1)
    Assert.Equal(ProgressionScale.storyGuardianLevel 2, bossOnly.Player.Level)

    // Two levels earned from optional fights survive the gate instead of being swallowed by the top-up.
    let withLead = resolveWithStartLevel (ProgressionScale.storyGuardianLevel 1 + 2)
    Assert.Equal(ProgressionScale.storyGuardianLevel 2 + 2, withLead.Player.Level)

  [<Fact>]
  let ``clearing the last story stage lands the protagonist on the Veteran threshold`` () =
    // The Infinite Tower (story floor 8) is entered as a Veteran, not a GrandMaster.
    let player = TierFactory.createClassLevel CharacterClass.Fighter 1
    let state = TowerSession.initSessionWithMode player 77 7 true
    let after = TowerSession.resolveEnemyDefeat "boss_stage_7" state

    Assert.Equal(40, after.Player.Level)
    Assert.Equal(CombatTier.Veteran, ProgressionScale.levelToTier after.Player.Level)

  [<Fact>]
  let ``the story level ladder climbs every stage and stops at the Veteran threshold`` () =
    let ladder = [ for floor in 1 .. 7 -> ProgressionScale.storyGuardianLevel floor ]
    Assert.Equal<int list>(List.sort ladder, ladder)
    Assert.Equal(40, List.last ladder)
    Assert.True(ladder |> List.forall (fun level -> level <= 40))

    // Template eligibility stays on the authored bestiary magnitudes, so the deep stages keep their
    // roster instead of collapsing onto the single lowest-level template.
    Assert.Equal(185, ProgressionScale.stageTemplateCeiling 6)

  [<Fact>]
  let ``Spell armor bonus progression matches the 5% per 10 levels table from level 50 to 200`` () =
    // Sub-50 levels have zero spell armor
    Assert.Equal(0.0, ProgressionScale.spellArmorBonus 1)
    Assert.Equal(0.0, ProgressionScale.spellArmorBonus 20)
    Assert.Equal(0.0, ProgressionScale.spellArmorBonus 40)
    Assert.Equal(0.0, ProgressionScale.spellArmorBonus 49)

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

    Assert.Equal(118, inq49.GetStat Prowess)
    Assert.Equal(120, inq50.GetStat Prowess)
    Assert.Equal(220, inq100.GetStat Prowess)
    Assert.Equal(420, inq200.GetStat Prowess)

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

    // Level 49: 0% spell armor. The off-plane physical defences hold `offDef` (178), and the guard pair
    // straddles the planes (Poise off, Composure own) so a caster guards exactly like a Fighter.
    Assert.Equal(0.0, inq49.SpellArmorBonus, 3)
    Assert.Equal(178, inq49.GetStat Fortitude)
    Assert.Equal(178, inq49.GetStat Reflex)
    Assert.Equal(178, inq49.GetStat Poise)
    Assert.Equal(237, inq49.GetStat Composure)

    // Level 50: 5% spell armor on the physical defences (primary 241 -> +12 each); the guard stat takes no
    // share.
    Assert.Equal(0.05, inq50.SpellArmorBonus, 3)
    Assert.Equal(181 + 12, inq50.GetStat Fortitude)
    Assert.Equal(181 + 12, inq50.GetStat Reflex)
    Assert.Equal(181, inq50.GetStat Poise)

    // Level 100: 30% spell armor (primary 441 -> +132 each) lifts both defences to the primary and stops
    // there, so a martial of the same tier can still reach them
    Assert.Equal(0.30, inq100.SpellArmorBonus, 3)
    Assert.Equal(441, inq100.GetStat Fortitude)
    Assert.Equal(441, inq100.GetStat Reflex)
    Assert.Equal(331, inq100.GetStat Poise)

    // Level 200: 80% spell armor (primary 841 -> +673 each), still capped at the primary
    Assert.Equal(0.80, inq200.SpellArmorBonus, 3)
    Assert.Equal(841, inq200.GetStat Fortitude)
    Assert.Equal(841, inq200.GetStat Reflex)
    Assert.Equal(631, inq200.GetStat Poise)

  [<Fact>]
  let ``sparing a beast still grows a stat past the class curve`` () =
    let player = TierFactory.createClassLevel CharacterClass.Fighter 40
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

    // Growth is unbounded: a spared beast pays a real point even at the curve, alongside the
    // steadying morale and the familiarity.
    Assert.Equal((TierFactory.createClassLevel CharacterClass.Fighter 40).Stats.Get Prowess, atCurve)
    Assert.Equal(atCurve + 1, spared.Player.Stats.Get Prowess)

    let species = ProgressionProfile.NormalizeSpeciesName enemy.Combatant.Name

    Assert.Equal(5, spared.Player.Progression.BestiaryKnowledge |> Map.tryFind species |> Option.defaultValue 0)
