namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

module BestiaryTests =

  [<Fact>]
  let ``Bestiary catalog contains exactly 21 unique species across all 7 biomes`` () =
    let all = Bestiary.allMonsters
    Assert.Equal(21, all.Length)

    // IDs must be unique
    let uniqueIds = all |> List.map (fun m -> m.Id) |> Set.ofList
    Assert.Equal(21, uniqueIds.Count)

    // Each of the 7 FloorTheme biomes must have exactly 3 monsters
    let themes = [
      FloorTheme.QuarryPlazas
      FloorTheme.PineCloisters
      FloorTheme.BasaltCalderas
      FloorTheme.TempestTerraces
      FloorTheme.SunkenBoulevards
      FloorTheme.ElysianSanctuaries
      FloorTheme.CelestialSpires
    ]

    for theme in themes do
      let biomeMonsters = Bestiary.byBiome theme
      Assert.Equal(3, biomeMonsters.Length)

  [<Fact>]
  let ``Monster instantiation creates valid Combatant with proper stats, stance, and traits`` () =
    let slagHoundTemplate = Bestiary.byId "slag_hound" |> Option.get
    let monster = Bestiary.createMonster slagHoundTemplate

    Assert.Equal("Slag Hound", monster.Name)
    Assert.Equal(1150, monster.Health.Current)
    Assert.Equal(950, monster.Morale.Current)
    Assert.Equal(45, monster.Armor.Max)
    Assert.Equal(CombatStance.PowerStance, monster.Stance)
    Assert.Equal(Some MonsterFamily.Beast, monster.MonsterFamily)
    Assert.Equal(2, monster.MonsterTraits.Length)

    // Verify array-backed StatBlock lookup
    Assert.Equal(105, monster.GetStat Force)
    Assert.Equal(95, monster.GetStat Fortitude)
    Assert.Equal(80, monster.GetStat Finesse)

  [<Fact>]
  let ``Loot rolling returns souls within range and drops trophies based on probability`` () =
    let slagHoundTemplate = Bestiary.byId "slag_hound" |> Option.get

    // 1. Guaranteed drop roller (roll = 1 <= 40%)
    let luckyRoller min max = if min = 1 && max = 100 then 1 else min
    let souls, trophyOpt = Bestiary.rollLoot luckyRoller slagHoundTemplate

    Assert.Equal(15, souls)
    Assert.True(trophyOpt.IsSome)
    Assert.Equal("Slag Hound Core", trophyOpt.Value.Name)

    // 2. Unlucky roller (roll = 99 > 40%)
    let unluckyRoller min max = if min = 1 && max = 100 then 99 else max
    let souls2, trophyOpt2 = Bestiary.rollLoot unluckyRoller slagHoundTemplate

    Assert.Equal(30, souls2)
    Assert.True(trophyOpt2.IsNone)

  [<Fact>]
  let ``Monster Trait: VenomousSting applies bleed stacks upon landing a strike`` () =
    let rng = Random(42)
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    // Create a Thorn Weaver with VenomousSting 2
    let weaverTemplate = Bestiary.byId "thorn_weaver" |> Option.get
    let weaver = Bestiary.createMonster weaverTemplate

    // Target dummy
    let dummy = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Veteran
    Assert.Equal(0, dummy.BleedStacks)

    // Weaver attacks with FinesseCadence
    let action = StandardAttack (FinesseCadence false)
    let outcome = ActionResolver.resolveEx roller action weaver dummy 0

    // Target should have gained at least 2 bleed stacks
    Assert.True(outcome.Target.BleedStacks >= 2, sprintf "Target should have suffered venomous bleed (Actual: %d)" outcome.Target.BleedStacks)
    let hasTraitEvent = outcome.Events |> List.exists (function CombatEvent.MonsterTraitTriggered (_, "Venomous Sting", _) -> true | _ -> false)
    Assert.True(hasTraitEvent, "CombatEvent.MonsterTraitTriggered for Venomous Sting should be emitted")

  [<Fact>]
  let ``Monster Trait: AcidicBlood corrodes attacker armor when taking physical damage`` () =
    let rng = Random(123)
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    // Create a Stone Gargoyle with AcidicBlood 10
    let gargoyleTemplate = Bestiary.byId "stone_gargoyle" |> Option.get
    let gargoyle = Bestiary.createMonster gargoyleTemplate

    // Attacker with full armor
    let attacker = TierFactory.createClassTier CharacterClass.Berserker CombatTier.Veteran
    let initialArmor = attacker.Armor.Current

    // Berserker attacks with ForceStrike
    let action = StandardAttack (ForceStrike false)
    let outcome = ActionResolver.resolveEx roller action attacker gargoyle 0

    // Attacker's armor should have been corroded by at least 10
    Assert.True(outcome.Actor.Armor.Current <= initialArmor - 10, sprintf "Attacker armor should be corroded by acidic blood (Before: %d, After: %d)" initialArmor outcome.Actor.Armor.Current)
    let hasCorrosionEvent = outcome.Events |> List.exists (function CombatEvent.AcidicArmorCorroded _ -> true | _ -> false)
    Assert.True(hasCorrosionEvent, "CombatEvent.AcidicArmorCorroded should be emitted")

  [<Fact>]
  let ``Monster Trait: MoltenAura burns physical attackers with searing damage`` () =
    let rng = Random(999)
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    // Create a Magma Crawler with MoltenAura 15
    let crawlerTemplate = Bestiary.byId "magma_crawler" |> Option.get
    let crawler = Bestiary.createMonster crawlerTemplate

    let attacker = TierFactory.createClassTier CharacterClass.Duelist CombatTier.Veteran
    let initialHp = attacker.Health.Current

    let action = StandardAttack (FinesseCadence false)
    let outcome = ActionResolver.resolveEx roller action attacker crawler 0

    // Attacker should have suffered burn damage
    Assert.True(outcome.Actor.Health.Current < initialHp, sprintf "Attacker should take molten burn damage (Before: %d, After: %d)" initialHp outcome.Actor.Health.Current)
    let hasBurnEvent = outcome.Events |> List.exists (function CombatEvent.MoltenBurnInflicted _ -> true | _ -> false)
    Assert.True(hasBurnEvent, "CombatEvent.MoltenBurnInflicted should be emitted")

  [<Fact>]
  let ``Species Instinct AI: Beast prioritizes bleeding prey over healthy targets`` () =
    let houndTemplate = Bestiary.byId "slag_hound" |> Option.get
    let hound = Bestiary.createMonster houndTemplate

    let targetHealthy = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice
    let targetBleeding = { TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice with BleedStacks = 2 }

    let chosen = AI.chooseGroupTarget hound [ targetHealthy; targetBleeding ]
    Assert.Equal(2, chosen.BleedStacks)

  [<Fact>]
  let ``Species Instinct AI: Construct prioritizes highest armor frontline target`` () =
    let golemTemplate = Bestiary.byId "basalt_golem" |> Option.get
    let golem = Bestiary.createMonster golemTemplate

    let lightTarget = { TierFactory.createClassTier CharacterClass.Duelist CombatTier.Veteran with Armor = ArmorIntegrity.Create 30 }
    let heavyTarget = { TierFactory.createClassTier CharacterClass.Warden CombatTier.Veteran with Armor = ArmorIntegrity.Create 150 }

    let chosen = AI.chooseGroupTarget golem [ lightTarget; heavyTarget ]
    Assert.Equal(150, chosen.Armor.Max)

  [<Fact>]
  let ``Species Instinct AI: UndeadWraith prioritizes lowest Morale target`` () =
    let specterTemplate = Bestiary.byId "siren_specter" |> Option.get
    let specter = Bestiary.createMonster specterTemplate

    let stoicTarget = { TierFactory.createClassTier CharacterClass.Warden CombatTier.GrandMaster with Morale = Pool.Create 10000 }
    let shakenTarget = { TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster with Morale = Pool.Create 1500 }

    let chosen = AI.chooseGroupTarget specter [ stoicTarget; shakenTarget ]
    Assert.Equal(1500, chosen.Morale.Current)
