module Fornach.Tests.LootConsumableTests

open System
open Xunit
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine

let private stats =
  StatBlock.Create
    [
      StatId.Force, 100
      StatId.Fortitude, 60
      StatId.Finesse, 50
      StatId.Reflex, 50
      StatId.Prowess, 50
      StatId.Poise, 60
      StatId.Intellect, 100
      StatId.Resolve, 60
      StatId.Acuity, 50
      StatId.Intuition, 50
      StatId.Acumen, 50
      StatId.Composure, 60
    ]

/// A Warrior delver with 200-point pools: the class the plain drop paths assume.
let private delver () =
  Combatant.create (CombatantId.New()) "Delver" 200 200 stats

/// A Fighter reads Force as its offensive stat, which is where a weapon material must land.
let private fighter () =
  Combatant.createWithClass (CombatantId.New()) "Delver" 200 200 stats CharacterClass.Fighter 1

let private allCategories =
  [
    LootCategory.Humanoid
    LootCategory.Beast
    LootCategory.Plant
    LootCategory.Construct
    LootCategory.Spirit
  ]

let private allKinds =
  [
    ConsumableKind.ArmorRepairKit
    ConsumableKind.GrindingStone
    ConsumableKind.Ration
    ConsumableKind.SpiritSeed
    ConsumableKind.WeaponMaterial
    ConsumableKind.MentalMaterial
  ]

let private bagOf (entries: (ConsumableKind * int) list) (state: TowerRunState) = { state with Consumables = entries }

let private unidentifiedAs (kinds: ConsumableKind list) (state: TowerRunState) =
  { state with
      UnidentifiedConsumables = Set.ofList kinds
  }

// ---------------------------------------------------------------------------
// Loot category axis and consumable drop table
// ---------------------------------------------------------------------------

[<Fact>]
let ``every loot category drops a consumable and every kind belongs to exactly one category`` () =
  for category in allCategories do
    let table = Consumables.dropsFor category
    Assert.False(table.IsEmpty, sprintf "%A has no consumable drop" category)

    for (_, pct) in table do
      Assert.InRange(pct, 1, 100)

  for kind in allKinds do
    let owners =
      allCategories
      |> List.filter (fun category -> Consumables.dropsFor category |> List.exists (fun (k, _) -> k = kind))

    Assert.Equal<LootCategory list>([ kind.DropCategory ], owners)

[<Fact>]
let ``the bestiary family mapping holds and the two botanical aberrations are plants`` () =
  for template in Bestiary.allMonsters do
    let expected =
      match template.Family with
      | MonsterFamily.Beast -> LootCategory.Beast
      | MonsterFamily.Construct -> LootCategory.Construct
      | MonsterFamily.UndeadWraith
      | MonsterFamily.GriefManifestation
      | MonsterFamily.Aberration -> LootCategory.Spirit

    match template.Id with
    | "blight_sprite"
    | "thorn_weaver" -> Assert.Equal(LootCategory.Plant, template.Loot.Category)
    | _ -> Assert.Equal(expected, template.Loot.Category)

[<Fact>]
let ``one roll decides the drop and a kill can still drop nothing`` () =
  Assert.Equal(Some ConsumableKind.ArmorRepairKit, Consumables.rollDrop 0 LootCategory.Humanoid)
  Assert.Equal(Some ConsumableKind.ArmorRepairKit, Consumables.rollDrop 44 LootCategory.Humanoid)
  Assert.Equal(Some ConsumableKind.GrindingStone, Consumables.rollDrop 45 LootCategory.Humanoid)
  Assert.Equal(Some ConsumableKind.GrindingStone, Consumables.rollDrop 69 LootCategory.Humanoid)
  Assert.Equal(None, Consumables.rollDrop 70 LootCategory.Humanoid)
  Assert.Equal(None, Consumables.rollDrop 99 LootCategory.Humanoid)
  Assert.Equal(Some ConsumableKind.Ration, Consumables.rollDrop 49 LootCategory.Beast)
  Assert.Equal(None, Consumables.rollDrop 50 LootCategory.Beast)
  Assert.Equal(Some ConsumableKind.SpiritSeed, Consumables.rollDrop 25 LootCategory.Plant)
  Assert.Equal(Some ConsumableKind.WeaponMaterial, Consumables.rollDrop 0 LootCategory.Construct)
  Assert.Equal(None, Consumables.rollDrop 35 LootCategory.Construct)
  Assert.Equal(Some ConsumableKind.MentalMaterial, Consumables.rollDrop 29 LootCategory.Spirit)
  Assert.Equal(None, Consumables.rollDrop 30 LootCategory.Spirit)

// ---------------------------------------------------------------------------
// Souls: spirits carry half again
// ---------------------------------------------------------------------------

let private soulEntry (category: LootCategory) : MonsterLootEntry =
  {
    MinSouls = 200
    MaxSouls = 200
    Trophy = None
    TrophyDropChancePct = 0
    Category = category
  }

[<Fact>]
let ``a spirit kill yields more souls than the same-tier non-spirit kill`` () =
  let spirit = soulEntry LootCategory.Spirit
  let beast = soulEntry LootCategory.Beast

  // Same authored range, same draw: the category alone moves the payout.
  Assert.Equal(200, beast.SoulsFor 200)
  Assert.Equal(300, spirit.SoulsFor 200)
  Assert.True(spirit.SoulsFor 200 > beast.SoulsFor 200)

[<Fact>]
let ``the kill path banks the multiplied souls for a spirit species and the authored range otherwise`` () =
  let seed = 7
  let state = TowerSession.initSession (delver ()) seed 1

  // resolveEnemyDefeat seeds its roller with the run seed, the floor, and souls before the kill.
  let rawDraw (template: MonsterTemplate) =
    Random(seed + 1 * 31 + 0).Next(template.Loot.MinSouls, template.Loot.MaxSouls + 1)

  let killSouls (template: MonsterTemplate) =
    let enemy =
      {
        Id = "foe_" + template.Id
        Name = template.Name
        Combatant = Bestiary.createMonster template
        DropsKeyId = None
        IsDefeated = false
        ChaseCharge = 0.0
      }

    let pos = { X = 25; Y = 25 }

    let withEnemy =
      { state with
          CurrentFloor =
            { state.CurrentFloor with
                Entities = state.CurrentFloor.Entities |> Map.add pos (EntityEnemy enemy)
            }
      }

    (TowerSession.resolveEnemyDefeat enemy.Id withEnemy).Souls

  let spirit = Bestiary.obsidianFiend
  let construct = Bestiary.basaltGolem

  Assert.Equal(LootCategory.Spirit, spirit.Loot.Category)
  Assert.Equal(LootCategory.Construct, construct.Loot.Category)
  Assert.Equal(spirit.Loot.SoulsFor(rawDraw spirit), killSouls spirit)
  Assert.Equal(construct.Loot.SoulsFor(rawDraw construct), killSouls construct)
  // The spirit's own draw never pays out unmultiplied.
  Assert.True(killSouls spirit > rawDraw spirit)

[<Fact>]
let ``a humanoid kill rolls on the humanoid table and pays the flat level toll`` () =
  let state = TowerSession.initSession (delver ()) 42 1

  let bandit =
    {
      Id = "bandit_foe"
      Name = "Bandit"
      Combatant = TierFactory.createClassLevel CharacterClass.Warrior 4
      DropsKeyId = None
      IsDefeated = false
      ChaseCharge = 0.0
    }

  let pos = { X = 25; Y = 25 }

  let withBandit =
    { state with
        CurrentFloor =
          { state.CurrentFloor with
              Entities = state.CurrentFloor.Entities |> Map.add pos (EntityEnemy bandit)
          }
    }

  let updated = TowerSession.resolveEnemyDefeat bandit.Id withBandit

  Assert.Equal(bandit.Combatant.Level * 15, updated.Souls)
  Assert.True(updated.Trophies.IsEmpty)

  for (kind, count) in updated.Consumables do
    Assert.Equal(LootCategory.Humanoid, kind.DropCategory)
    Assert.True(count > 0)

  // Whatever was salvaged arrives unidentified.
  for (kind, _) in updated.Consumables do
    Assert.Contains(kind, updated.UnidentifiedConsumables)

// ---------------------------------------------------------------------------
// Consumable effects beat the rest caps
// ---------------------------------------------------------------------------

[<Fact>]
let ``an armor repair kit is the only way past the eighty percent repair cap`` () =
  let worn =
    let p = delver ()

    { p with
        Armor = { Current = 30; Max = 100 }
    }

  Assert.Equal(80, (TowerSession.restRepairArmor worn.Armor 1000).Current)
  Assert.Equal(100, (Consumables.apply ConsumableKind.ArmorRepairKit worn).Armor.Current)

[<Fact>]
let ``a grinding stone is the only way back to pristine`` () =
  let damaged =
    let p = delver ()

    { p with
        WeaponCondition = WeaponCondition.Damaged
    }

  Assert.Equal(WeaponCondition.Notched, TowerSession.restHoneWeapon WeaponCondition.Damaged)
  Assert.Equal(WeaponCondition.Pristine, (Consumables.apply ConsumableKind.GrindingStone damaged).WeaponCondition)

[<Fact>]
let ``a ration and a spirit seed exceed the eighty percent rest cap`` () =
  let spent =
    let p = delver ()

    { p with
        Health = { p.Health with Current = 40 }
        Morale = { p.Morale with Current = 40 }
    }

  Assert.Equal(160, (TowerSession.restRecoverPool spent.Health 1000).Current)
  Assert.Equal(160, (TowerSession.restRecoverPool spent.Morale 1000).Current)
  Assert.Equal(200, (Consumables.apply ConsumableKind.Ration spent).Health.Current)
  Assert.Equal(200, (Consumables.apply ConsumableKind.SpiritSeed spent).Morale.Current)

// ---------------------------------------------------------------------------
// Rare materials: permanent, and spent when used
// ---------------------------------------------------------------------------

[<Fact>]
let ``materials apply permanently and the bag decrements on each use`` () =
  let start =
    TowerSession.initSession (fighter ()) 42 1
    |> bagOf [ ConsumableKind.WeaponMaterial, 2; ConsumableKind.MentalMaterial, 1 ]

  let forceBefore = start.Player.Stats.Get StatId.Force
  let intellectBefore = start.Player.Stats.Get StatId.Intellect

  Assert.Equal(
    StatId.Force,
    Consumables.materialStat ConsumableKind.WeaponMaterial CharacterClass.Fighter
    |> Option.get
  )

  Assert.Equal(
    StatId.Intellect,
    Consumables.materialStat ConsumableKind.MentalMaterial CharacterClass.Fighter
    |> Option.get
  )

  let once =
    match TowerSession.useConsumable ConsumableKind.WeaponMaterial start with
    | Ok s -> s
    | Error e -> failwith e

  Assert.Equal(forceBefore + Consumables.materialUpgrade, once.Player.Stats.Get StatId.Force)

  Assert.Equal<(ConsumableKind * int) list>(
    [ ConsumableKind.WeaponMaterial, 1; ConsumableKind.MentalMaterial, 1 ],
    once.Consumables
  )

  let twice =
    match TowerSession.useConsumable ConsumableKind.WeaponMaterial once with
    | Ok s -> s
    | Error e -> failwith e

  Assert.Equal(forceBefore + 2 * Consumables.materialUpgrade, twice.Player.Stats.Get StatId.Force)
  Assert.Equal<(ConsumableKind * int) list>([ ConsumableKind.MentalMaterial, 1 ], twice.Consumables)

  let mental =
    match TowerSession.useConsumable ConsumableKind.MentalMaterial twice with
    | Ok s -> s
    | Error e -> failwith e

  Assert.Equal(intellectBefore + Consumables.materialUpgrade, mental.Player.Stats.Get StatId.Intellect)
  Assert.True(mental.Consumables.IsEmpty)

  // Refused once the row is gone, and nothing is applied.
  match TowerSession.useConsumable ConsumableKind.WeaponMaterial mental with
  | Ok _ -> Assert.Fail "An empty row must not be usable."
  | Error msg -> Assert.Contains("No", msg)

// ---------------------------------------------------------------------------
// Identification gate and shrine consolidation
// ---------------------------------------------------------------------------

[<Fact>]
let ``an unidentified consumable cannot be used until a shrine reveals it`` () =
  let start =
    TowerSession.initSession (delver ()) 42 1
    |> bagOf [ ConsumableKind.Ration, 1 ]
    |> unidentifiedAs [ ConsumableKind.Ration ]

  match TowerSession.useConsumable ConsumableKind.Ration start with
  | Ok _ -> Assert.Fail "An unidentified consumable must not be usable."
  | Error msg -> Assert.Contains("unidentified", msg)

  let revealed, summary = TowerSession.shrineBlessing start

  Assert.True(revealed.UnidentifiedConsumables.IsEmpty)
  Assert.Contains(summary, fun line -> line.Contains "reveals")
  // Revealing identifies the kind, it does not spend it.
  Assert.Equal<(ConsumableKind * int) list>([ ConsumableKind.Ration, 1 ], revealed.Consumables)

  match TowerSession.useConsumable ConsumableKind.Ration revealed with
  | Ok used -> Assert.True(used.Consumables.IsEmpty)
  | Error e -> Assert.Fail e

[<Fact>]
let ``a shrine blesses on every visit and never re-applies a material or a reveal`` () =
  let start = TowerSession.initSession (fighter ()) 42 1

  let shrinePos =
    {
      X = start.PlayerPosition.X + 1
      Y = start.PlayerPosition.Y
    }

  let shrine =
    {
      Id = "sanctuary_loot_test"
      Name = "Sanctuary of the Assemblage"
      BlessingDescription = "Restores all vitals and works carried materials"
      IsUsed = false
    }

  let wounded =
    { start with
        CurrentFloor =
          { start.CurrentFloor with
              Tiles = start.CurrentFloor.Tiles |> Map.add shrinePos (Floor SurfaceType.PavedStone)
              Entities = start.CurrentFloor.Entities |> Map.add shrinePos (EntityShrine shrine)
          }
        Player =
          { start.Player with
              Health =
                { start.Player.Health with
                    Current = 20
                }
              Morale =
                { start.Player.Morale with
                    Current = 20
                }
          }
        Consumables = [ ConsumableKind.WeaponMaterial, 2; ConsumableKind.Ration, 1 ]
        UnidentifiedConsumables = Set.ofList [ ConsumableKind.WeaponMaterial; ConsumableKind.Ration ]
    }

  let forceBefore = wounded.Player.Stats.Get StatId.Force

  let isShrineEvent ev =
    match ev with
    | TowerEvent.ShrineActivated s -> s.Id = shrine.Id
    | _ -> false

  let first, firstEvents = TowerSession.stepPlayer Direction.East wounded

  Assert.Contains(firstEvents, isShrineEvent)
  Assert.Equal(200, first.Player.Health.Current)
  Assert.Equal(200, first.Player.Morale.Current)
  Assert.Equal(forceBefore + 2 * Consumables.materialUpgrade, first.Player.Stats.Get StatId.Force)
  Assert.Equal<(ConsumableKind * int) list>([ ConsumableKind.Ration, 1 ], first.Consumables)
  Assert.True(first.UnidentifiedConsumables.IsEmpty)

  // A second visit from a second wound: full restore again, but no second helping of materials.
  let woundedAgain =
    { first with
        Player =
          { first.Player with
              Health =
                { first.Player.Health with
                    Current = 10
                }
          }
    }

  let second, secondEvents = TowerSession.stepPlayer Direction.East woundedAgain

  Assert.Contains(secondEvents, isShrineEvent)
  Assert.Equal(200, second.Player.Health.Current)
  Assert.Equal(forceBefore + 2 * Consumables.materialUpgrade, second.Player.Stats.Get StatId.Force)
  Assert.Equal<(ConsumableKind * int) list>([ ConsumableKind.Ration, 1 ], second.Consumables)
  Assert.True(second.UnidentifiedConsumables.IsEmpty)

  match Map.tryFind shrinePos second.CurrentFloor.Entities with
  | Some(EntityShrine still) -> Assert.False still.IsUsed
  | other -> Assert.Fail(sprintf "Expected the shrine to stay on the map, got %A" other)

// ---------------------------------------------------------------------------
// Altars: the cost is a percentage of the pool
// ---------------------------------------------------------------------------

let private stateWithAltar (cost: AltarCost) (player: Combatant) =
  let state = TowerSession.initSession player 42 1

  let altarPos =
    {
      X = state.PlayerPosition.X + 2
      Y = state.PlayerPosition.Y
    }

  let altar =
    {
      Id = "altar_loot_test"
      Name = "Altar of the Iron Quarryman"
      Description = "A dark granite crucible."
      Cost = cost
      Reward = AltarReward.StatBuff(StatId.Force, 15)
      IsUsed = false
    }

  let withAltar =
    { state with
        CurrentFloor =
          { state.CurrentFloor with
              Tiles = state.CurrentFloor.Tiles |> Map.add altarPos (Floor SurfaceType.PavedStone)
              Entities =
                state.CurrentFloor.Entities
                |> Map.add altarPos (EntityEncounter(SacrificialAltar altar))
          }
    }

  altarPos, withAltar

[<Fact>]
let ``a health altar from full lands below the rest cap and cannot kill`` () =
  let altarPos, state = stateWithAltar (AltarCost.SacrificeHealth 25) (delver ())

  let restCap =
    int (float state.Player.Health.Maximum * TowerSession.restRecoveryFraction)

  match TowerSession.applyAltarSacrifice altarPos state with
  | Error e -> Assert.Fail e
  | Ok paid ->
    // 25% of 200 leaves 150: strictly under the 160 rest cap, and still standing.
    Assert.Equal(150, paid.Player.Health.Current)
    Assert.True(paid.Player.Health.Current < restCap)
    Assert.True(paid.Player.Health.Current > 0)

  // The same toll on a ten-times bigger pool still costs a quarter of it, not a flat 25.
  let big = Combatant.create (CombatantId.New()) "Veteran" 2000 2000 stats
  let bigPos, bigState = stateWithAltar (AltarCost.SacrificeHealth 25) big

  match TowerSession.applyAltarSacrifice bigPos bigState with
  | Error e -> Assert.Fail e
  | Ok paid -> Assert.Equal(1500, paid.Player.Health.Current)

  // A nearly-dead player is wounded further but never killed.
  let dying =
    let p = delver ()

    { p with
        Health = { p.Health with Current = 5 }
    }

  let dyingPos, dyingState = stateWithAltar (AltarCost.SacrificeHealth 25) dying

  match TowerSession.applyAltarSacrifice dyingPos dyingState with
  | Error e -> Assert.Fail e
  | Ok paid -> Assert.Equal(1, paid.Player.Health.Current)

[<Fact>]
let ``morale and armor altars also cost a percentage of the maximum`` () =
  let moralePos, moraleState =
    stateWithAltar (AltarCost.SacrificeMorale 30) (delver ())

  match TowerSession.applyAltarSacrifice moralePos moraleState with
  | Error e -> Assert.Fail e
  | Ok paid -> Assert.Equal(140, paid.Player.Morale.Current)

  let armorPos, armorState = stateWithAltar (AltarCost.ShredArmor 30) (delver ())
  let armorBefore = armorState.Player.Armor.Current

  match TowerSession.applyAltarSacrifice armorPos armorState with
  | Error e -> Assert.Fail e
  | Ok paid ->
    let expected =
      armorBefore - int (Math.Round(float armorState.Player.Armor.Max * 0.30))

    Assert.Equal(expected, paid.Player.Armor.Current)

[<Fact>]
let ``the inventory panel conceals an unidentified kind's name and description`` () =
  let bag = [ ConsumableKind.ArmorRepairKit, 2; ConsumableKind.Ration, 1 ]

  let rows =
    Consumables.describe (Set.ofList [ ConsumableKind.ArmorRepairKit ]) false bag

  let kit = rows |> List.find (fun (kind, _, _, _) -> kind = ConsumableKind.ArmorRepairKit)
  let (_, kitName, kitDescription, kitCount) = kit

  Assert.Equal("Unidentified", kitName)
  Assert.DoesNotContain("Repair", kitName)
  Assert.DoesNotContain("armor", kitDescription)
  Assert.Equal(2, kitCount)

  // A revealed kind shows its real text in the same call.
  let (_, rationName, rationDescription, _) =
    rows |> List.find (fun (kind, _, _, _) -> kind = ConsumableKind.Ration)

  Assert.Equal("Ration", rationName)
  Assert.Equal(ConsumableKind.Ration.Description, rationDescription)

[<Fact>]
let ``the inventory panel sorts by most recent or by name`` () =
  // add moves a kind to the front, so pickup order is newest-first.
  let bag =
    []
    |> Consumables.add ConsumableKind.Ration
    |> Consumables.add ConsumableKind.GrindingStone
    |> Consumables.add ConsumableKind.ArmorRepairKit

  let names (rows: (ConsumableKind * string * string * int) list) =
    rows |> List.map (fun (_, name, _, _) -> name)

  let byRecent = Consumables.describe Set.empty false bag
  Assert.Equal<string list>([ "Armor Repair Kit"; "Grinding Stone"; "Ration" ], names byRecent)

  let byName = Consumables.describe Set.empty true bag
  Assert.Equal<string list>([ "Armor Repair Kit"; "Grinding Stone"; "Ration" ], names byName)

  // Picking up the ration again makes it the most recent without changing the alphabetical order.
  let repicked = Consumables.add ConsumableKind.Ration bag
  Assert.Equal<string list>([ "Ration"; "Armor Repair Kit"; "Grinding Stone" ], names (Consumables.describe Set.empty false repicked))
  Assert.Equal<string list>([ "Armor Repair Kit"; "Grinding Stone"; "Ration" ], names (Consumables.describe Set.empty true repicked))
  Assert.Equal(2, Consumables.count ConsumableKind.Ration repicked)

[<Fact>]
let ``an empty bag lists nothing rather than a placeholder row`` () =
  Assert.Empty(Consumables.describe Set.empty false [])
