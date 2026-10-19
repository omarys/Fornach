module Fornach.Tests.AmbushScalingTests

open Xunit
open Fornach.Domain
open Fornach.Engine

let private playerAt (level: int) =
    { TierFactory.createClassLevel CharacterClass.Fighter level with PlayerStyle = true }

let private physicalAttacks =
    [ AttackClassification.ForceStrike false
      AttackClassification.FinesseCadence false
      AttackClassification.ProwessStrike false ]

let private best (a: Combatant) (d: Combatant) =
    physicalAttacks |> List.map (fun x -> AttackEstimator.estimate a d x) |> List.max

/// Ambush packs are authored at fixed bestiary levels, so they must be clamped to the player before
/// they are fought - otherwise a first-floor ambush is unwinnable rather than hard.
[<Fact>]
let ``a clamped bestiary ambush is winnable at level one`` () =
    let player = playerAt 1
    let ambusher = TierFactory.clampEngagedToPlayerLevel (Bestiary.createMonster Bestiary.blightSprite) player

    Assert.True(best player ambusher > 0.0, "player deals no damage to a clamped ambusher")
    Assert.True(
        best ambusher player < float player.Health.Maximum,
        "a single clamped ambush hit still one-shots the player"
    )

[<Fact>]
let ``clamping an ambusher twice is the same as clamping it once`` () =
    // handleGroup clamps enemies that adjacentEnemies has already clamped, so this has to be safe.
    let player = playerAt 1
    let sprite = Bestiary.createMonster Bestiary.blightSprite
    let once = TierFactory.clampEngagedToPlayerLevel sprite player
    let twice = TierFactory.clampEngagedToPlayerLevel once player

    Assert.Equal(once.Level, twice.Level)
    Assert.Equal(once.Health.Maximum, twice.Health.Maximum)
    Assert.Equal(once.Armor.Max, twice.Armor.Max)
