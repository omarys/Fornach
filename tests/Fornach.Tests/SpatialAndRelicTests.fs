namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Spatial

module SpatialAndRelicTests =

  [<Fact>]
  let ``Rear flank attack bypasses Nach +1 defense posture hit`` () =
    let roller: DiceRoller = fun _ max -> max
    let actor =
      TierFactory.createClassLevel CharacterClass.Fighter 1
      |> Combatant.seizeVor
    let target =
      TierFactory.createClassLevel CharacterClass.Fighter 1
      |> Combatant.yieldVor

    // 1. Head-on (Front): Nach posture grants +1 defensive hit
    let resFront = ActionResolver.resolveExWithDefenseFlank roller (StandardAttack (ForceStrike false)) None actor target 0 1.0
    // 2. Rear attack: Nach posture hit bypassed
    let resRear = ActionResolver.resolveExWithDefenseFlank roller (StandardAttack (ForceStrike false)) None actor target 0 1.30

    match resFront.Contest, resRear.Contest with
    | Some cFront, Some cRear ->
      // Rear attacker meets exactly 1 fewer defensive hit than front attacker with identical rolls
      Assert.Equal(cFront.Defender.TotalHits - 1, cRear.Defender.TotalHits)
    | _ ->
      Assert.Fail("Contest expected for standard attack")

  [<Fact>]
  let ``Force strike with surplus net hits inflicts knockback event`` () =
    let roller: DiceRoller = fun _ max -> max
    let actor =
      TierFactory.createClassLevel CharacterClass.Fighter 50
      |> Combatant.seizeVor
    let target =
      TierFactory.createClassLevel CharacterClass.Fighter 1
      |> Combatant.yieldVor

    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike true)) actor target
    let hasKnockback =
      res.Events
      |> List.exists (function CombatEvent.CombatantKnockedBack(aId, tId, 1) when aId = actor.Id && tId = target.Id -> true | _ -> false)

    Assert.True(hasKnockback, "Expected CombatantKnockedBack event on heavy wild blow with high net hits")

  [<Fact>]
  let ``Relic hook fires on whiff to grant Momentum`` () =
    // A relic modifier that gives +1 Momentum when a strike whiffs
    let kineticRelic: RelicModifier = {
      Id = "relic_kinetic"
      Name = "Kinetic Dynamo"
      Description = "Whiffs grant +1 Momentum instead of penalty"
      Apply = fun trigger actor targetOpt ->
        match trigger with
        | RelicHookTrigger.OnWhiff true ->
          let updated = { actor with Momentum = actor.Momentum + 1 }
          let evt = CombatEvent.RelicEffectTriggered(actor.Id, "Kinetic Dynamo", "Whiff converted to +1 Momentum.")
          updated, targetOpt, [ evt ]
        | _ -> actor, targetOpt, []
    }

    let lowRoller: DiceRoller = fun min _ -> min
    let actor =
      { TierFactory.createClassLevel CharacterClass.Fighter 1 with
          RelicModifiers = [ kineticRelic ] }
      |> Combatant.seizeVor
    let target =
      TierFactory.createClassLevel CharacterClass.Fighter 100
      |> Combatant.yieldVor

    let res = ActionResolver.resolveExWithDefense lowRoller (StandardAttack (ForceStrike false)) None actor target 0

    Assert.True(res.Contest.Value.IsWhiff, "Strike should whiff against level 100 opponent with minimum rolls")
    Assert.True(res.Actor.Momentum >= 1, "Kinetic Dynamo relic should have granted +1 Momentum on whiff")
    let hasRelicEvt =
      res.Events
      |> List.exists (function CombatEvent.RelicEffectTriggered(_, "Kinetic Dynamo", _) -> true | _ -> false)
    Assert.True(hasRelicEvt, "RelicEffectTriggered event should be emitted")

  [<Fact>]
  let ``resolveKnockbackHazard applies damage and events for lava, acid, deep current, and wall`` () =
    let emptyFloor: TowerFloor = {
      FloorNumber = 1
      Theme = FloorTheme.QuarryPlazas
      Width = 10
      Height = 10
      Tiles = [ for x in 0 .. 9 do for y in 0 .. 9 -> { X = x; Y = y }, Floor SurfaceType.PavedStone ] |> Map.ofList
      Entities = Map.empty
      StairwayLocation = { X = 0; Y = 0 }
      SpawnLocation = { X = 5; Y = 5 }
      Explored = Set.empty
      Visible = Set.empty
      SeenEnemies = Set.empty
      ActiveQuests = []
      InitialPlayerLevel = 1
    }

    let floorWithHazards =
      let tiles =
        emptyFloor.Tiles
        |> Map.add { X = 6; Y = 5 } (Hazard LavaRift)
        |> Map.add { X = 4; Y = 5 } (Hazard AcidSlag)
        |> Map.add { X = 5; Y = 6 } (Hazard DeepCurrent)
        |> Map.add { X = 5; Y = 4 } Pillar // Wall
      { emptyFloor with Tiles = tiles }

    let player = TierFactory.createClassLevel CharacterClass.Fighter 1
    let attackerPos = { X = 5; Y = 5 }

    // 1. Push East into LavaRift at (6, 5): Target at (5, 5), pushed by attacker at (4, 5) -> dest (6, 5)
    let damagedLava, lavaEvts =
      TowerSession.resolveKnockbackHazard floorWithHazards { X = 4; Y = 5 } { X = 5; Y = 5 } player
    Assert.Equal(player.Health.Current - 15, damagedLava.Health.Current)
    Assert.Contains(lavaEvts, (function CombatEvent.HazardPushed(_, "Molten Lava Rift", 15, _) -> true | _ -> false))

    // 2. Push West into AcidSlag at (4, 5): Target at (5, 5), pushed by attacker at (6, 5) -> dest (4, 5)
    let damagedAcid, acidEvts =
      TowerSession.resolveKnockbackHazard floorWithHazards { X = 6; Y = 5 } { X = 5; Y = 5 } player
    Assert.Equal(player.Armor.Current - 15, damagedAcid.Armor.Current)
    Assert.Contains(acidEvts, (function CombatEvent.HazardPushed(_, "Corrosive Acid Slag", 15, _) -> true | _ -> false))

    // 3. Push South into DeepCurrent at (5, 6): Target at (5, 5), pushed by attacker at (5, 4) -> dest (5, 6)
    let damagedCurrent, currentEvts =
      TowerSession.resolveKnockbackHazard floorWithHazards { X = 5; Y = 4 } { X = 5; Y = 5 } player
    Assert.Equal(player.Meters.Exhaustion + 15, damagedCurrent.Meters.Exhaustion)
    Assert.Contains(currentEvts, (function CombatEvent.HazardPushed(_, "Deep Floodwater Current", 15, _) -> true | _ -> false))

    // 4. Push North into Pillar/Wall at (5, 4): Target at (5, 5), pushed by attacker at (5, 6) -> dest (5, 4)
    let stunnedWall, wallEvts =
      TowerSession.resolveKnockbackHazard floorWithHazards { X = 5; Y = 6 } { X = 5; Y = 5 } player
    Assert.Equal(player.Meters.Frustration + 10, stunnedWall.Meters.Frustration)
    Assert.Contains(wallEvts, (function CombatEvent.HazardPushed(_, "Solid Stone Wall", 10, _) -> true | _ -> false))

  [<Fact>]
  let ``AI adaptToDefensiveRepetition detects repeated guards against elite enemy`` () =
    let enemy = TierFactory.createClassLevel CharacterClass.Fighter 5
    let guardFort = Some(Guard (FortitudeGuard false))
    let guardReflex = Some(Guard (ReflexSlip false))

    // Consecutive identical guard
    Assert.True(AI.adaptToDefensiveRepetition enemy guardFort guardFort)

    // Alternating guards - not repeated
    Assert.False(AI.adaptToDefensiveRepetition enemy guardFort guardReflex)

  [<Fact>]
  let ``AI feintConvertVector converts Power strike to Finesse when player chooses FortitudeGuard`` () =
    let original = StandardAttack (ForceStrike true)
    let converted = AI.feintConvertVector original (FortitudeGuard false)
    match converted with
    | StandardAttack (FinesseCadence isHeavy) -> Assert.True(isHeavy)
    | _ -> Assert.Fail("Expected conversion to FinesseCadence true")

  [<Fact>]
  let ``recklessnessXpBonus calculates tiered multipliers and labels`` () =
    // 0 - 24%: 1.0x, None
    let m0, l0 = TowerSession.recklessnessXpBonus 0
    Assert.Equal(1.0, m0)
    Assert.True(l0.IsNone)

    let m20, l20 = TowerSession.recklessnessXpBonus 20
    Assert.Equal(1.0, m20)
    Assert.True(l20.IsNone)

    // 25 - 49%: 1.25x Aggressive Drive
    let m25, l25 = TowerSession.recklessnessXpBonus 25
    Assert.Equal(1.25, m25)
    Assert.Contains("Aggressive Drive", l25.Value)

    // 50 - 74%: 1.50x Fever Pitch
    let m50, l50 = TowerSession.recklessnessXpBonus 50
    Assert.Equal(1.50, m50)
    Assert.Contains("Fever Pitch", l50.Value)

    // 75+%: 2.0x Razor's Edge
    let m75, l75 = TowerSession.recklessnessXpBonus 75
    Assert.Equal(2.0, m75)
    Assert.Contains("RAZOR'S EDGE", l75.Value)

    let m90, l90 = TowerSession.recklessnessXpBonus 90
    Assert.Equal(2.0, m90)
    Assert.Contains("RAZOR'S EDGE", l90.Value)

  [<Fact>]
  let ``resolveEnemyDefeat scales XP by 2x when player defeats foe at Razor's Edge (Recklessness >= 75)`` () =
    let basePlayer = TierFactory.createClassLevel CharacterClass.Fighter 1
    let stateCalm = TowerSession.initSessionWithMode basePlayer 42 1 true
    let enemy =
      stateCalm.CurrentFloor.Entities
      |> Map.toList
      |> List.pick (fun (_, ent) ->
        match ent with
        | EntityEnemy e when not e.IsDefeated && not e.IsBoss -> Some e
        | _ -> None)

    let afterCalm = TowerSession.resolveEnemyDefeat enemy.Id stateCalm
    let calmXp = afterCalm.Player.Progression.CurrentXP
    let baseXp = enemy.Combatant.Level * 10 + 25
    Assert.Equal(baseXp, calmXp)

    // Player with 80% Recklessness (Razor's Edge)
    let recklessPlayer =
      basePlayer
      |> Combatant.updateMeters (fun m -> { m with Recklessness = Meter.Create 80 })
    let stateReckless = { stateCalm with Player = recklessPlayer }
    let afterReckless = TowerSession.resolveEnemyDefeat enemy.Id stateReckless
    let recklessXp = afterReckless.Player.Progression.CurrentXP

    // 2.0x bonus XP awarded
    Assert.Equal(baseXp * 2, recklessXp)
    Assert.Contains(afterReckless.MessageLog, (fun msg -> msg.Contains("RAZOR'S EDGE")))
