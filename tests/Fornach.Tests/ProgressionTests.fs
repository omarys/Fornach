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
    let profile = ProgressionProfile.create CharacterClass.Warden 5
    let after, gained = profile.GrantXP 10

    Assert.Equal(0, gained)
    Assert.Equal(5, after.Level)
    Assert.Equal(10, after.CurrentXP)
    Assert.Equal(500, after.ExperienceToNext)

  [<Fact>]
  let ``GrantXP levels up once at threshold and carries the remainder`` () =
    let profile = ProgressionProfile.create CharacterClass.Berserker 1
    let after, gained = profile.GrantXP 150

    Assert.Equal(1, gained)
    Assert.Equal(2, after.Level)
    Assert.Equal(50, after.CurrentXP)

  [<Fact>]
  let ``GrantXP crosses multiple levels in a single award`` () =
    // Level 1 needs 100 XP and level 2 needs 200 XP, so 320 XP reaches level 3 with 20 left over.
    let profile = ProgressionProfile.create CharacterClass.Duelist 1
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
    let novice = TierFactory.createClassLevel CharacterClass.Berserker 1
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
    let novice = TierFactory.createClassLevel CharacterClass.Duelist 1
    let veteran = TierFactory.rescaleToLevel novice 40

    Assert.Equal(novice.Id, veteran.Id)
    Assert.Equal(novice.Class, veteran.Class)
    Assert.Equal(novice.Name, veteran.Name)

  [<Fact>]
  let ``applyExperience levels the player and rescales their stats`` () =
    let player = TierFactory.createClassLevel CharacterClass.Berserker 1
    let leveled, messages = TowerSession.applyExperience player 150

    Assert.Equal(2, leveled.Level)
    Assert.True(leveled.Stats.Get Force > player.Stats.Get Force)
    Assert.Contains(messages, (fun m -> m.Contains "LEVEL UP"))

  [<Fact>]
  let ``defeating a foe grants souls and experience`` () =
    let player = TierFactory.createClassLevel CharacterClass.Berserker 1
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
