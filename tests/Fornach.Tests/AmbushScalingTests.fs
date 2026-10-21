module Fornach.Tests.AmbushScalingTests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Spatial

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
/// The ambush duel is handed straight to group combat from the spawn event, so it never passes
/// through enemiesAggroed and never sees the engage-time clamp. Three level-20 Blight Sprites used
/// to land ApathyDoldrums on a level-2 player and collapse them (Cognitive Fatigue) in one round.
[<Fact>]
let ``ambush spawns are clamped to the player's level`` () =
    let player = playerAt 3
    let state = TowerSession.initSessionWithMode player 7 1 true

    let ambushPoints =
        state.CurrentFloor.Entities
        |> Map.toList
        |> List.choose (fun (pt, e) ->
            match e with
            | EntityEncounter (AmbushLair a) when not a.IsTriggered -> Some pt
            | _ -> None)

    Assert.True(not (List.isEmpty ambushPoints), "story floor 1 should seed an ambush")

    let delta dir =
        match dir with
        | Direction.North -> 0, -1
        | Direction.NorthEast -> 1, -1
        | Direction.East -> 1, 0
        | Direction.SouthEast -> 1, 1
        | Direction.South -> 0, 1
        | Direction.SouthWest -> -1, 1
        | Direction.West -> -1, 0
        | Direction.NorthWest -> -1, -1

    let spawnFrom =
        ambushPoints
        |> List.tryPick (fun pt ->
            [ Direction.North; Direction.East; Direction.South; Direction.West
              Direction.NorthEast; Direction.SouthEast; Direction.SouthWest; Direction.NorthWest ]
            |> List.tryPick (fun dir ->
                let dx, dy = delta dir
                let approach = { X = pt.X - dx; Y = pt.Y - dy }
                let prepared = { state with PlayerPosition = approach }
                let _, events = TowerSession.stepPlayer dir prepared
                events
                |> List.tryPick (function
                    | TowerEvent.AmbushTriggered(_, spawned) -> Some spawned
                    | _ -> None)))

    match spawnFrom with
    | None -> Assert.Fail("stepping onto the ambush tile produced no AmbushTriggered event")
    | Some spawned ->
        Assert.True(not (List.isEmpty spawned), "ambush spawned no enemies")

        for enemy in spawned do
            Assert.True(
                enemy.Combatant.Level <= player.Level + 2,
                sprintf "ambusher %s spawned at level %d against a level %d player"
                    enemy.Name enemy.Combatant.Level player.Level
            )

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
