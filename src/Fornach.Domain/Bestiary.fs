namespace Fornach.Domain

open System

/// Thematic alchemical material or trophy dropped upon monster defeat
type AlchemicalTrophy =
  { Id: string
    Name: string
    Description: string
    EssenceValue: int }

/// Reward table entry for defeating a monster adversary
type MonsterLootEntry =
  { MinSouls: int
    MaxSouls: int
    Trophy: AlchemicalTrophy option
    TrophyDropChancePct: int }

/// Declarative template defining a non-humanoid monster adversary
type MonsterTemplate =
  { Id: string
    Name: string
    Family: MonsterFamily
    Role: MonsterRole
    NativeBiome: FloorTheme
    Level: int
    Tier: CombatTier
    MaxHealth: int
    MaxMorale: int
    ArmorDurability: int
    Stats: (StatId * int) list
    PreferredStance: CombatStance
    PreferredComplexForm: ComplexForm option
    Traits: MonsterTrait list
    Loot: MonsterLootEntry
    Glyph: char
    ColorHex: string
    Description: string }

  /// Instantiates a zero-allocation, combat-ready Combatant from this monster template
  member this.ToCombatant (uniqueId: CombatantId) : Combatant =
    let statBlock = StatBlock.Create this.Stats
    let nearestClass =
      match this.Family, this.Role with
      | MonsterFamily.Construct, (MonsterRole.Brute | MonsterRole.Colossus) -> CharacterClass.Warden
      | MonsterFamily.Beast, (MonsterRole.Swarmer | MonsterRole.Brute) -> CharacterClass.Berserker
      | MonsterFamily.Beast, (MonsterRole.Skirmisher | MonsterRole.Stalker) -> CharacterClass.Duelist
      | MonsterFamily.UndeadWraith, _ -> CharacterClass.Mesmer
      | MonsterFamily.Aberration, (MonsterRole.Caster | MonsterRole.Colossus) -> CharacterClass.Inquisitor
      | MonsterFamily.GriefManifestation, _ -> CharacterClass.Abjurer
      | _ -> CharacterClass.Warrior

    let prog = ProgressionProfile.create nearestClass this.Level
    let baseArmor = ArmorIntegrity.Create this.ArmorDurability
    { Combatant.create uniqueId this.Name this.MaxHealth this.MaxMorale statBlock with
        Progression = prog
        Armor = baseArmor
        Stance = this.PreferredStance
        ComplexForm = this.PreferredComplexForm
        MonsterFamily = Some this.Family
        MonsterTraits = this.Traits }

module Bestiary =

  // ---------------------------------------------------------------------------
  // 1. Alchemical Trophies Catalog
  // ---------------------------------------------------------------------------
  let slagHoundCore =
    { Id = "trophy_slag_hound_core"
      Name = "Slag Hound Core"
      Description = "A glowing, solidified magma core radiating kinetic heat and brimstone."
      EssenceValue = 25 }

  let gargoyleTalisman =
    { Id = "trophy_gargoyle_talisman"
      Name = "Gargoyle Talisman"
      Description = "A petrified stone claw inscribed with ancient basalt protective runes."
      EssenceValue = 45 }

  let overseerSeal =
    { Id = "trophy_overseer_seal"
      Name = "Overseer's Iron Seal"
      Description = "A heavy forged iron seal used to brand quarry megaliths and slave chains."
      EssenceValue = 120 }

  let blightPollen =
    { Id = "trophy_blight_pollen"
      Name = "Blight Pollen"
      Description = "Corrosive botanical spores that decompose organic matter on contact."
      EssenceValue = 30 }

  let weaverSilk =
    { Id = "trophy_weaver_silk"
      Name = "Weaver Silk"
      Description = "Iridescent arachnid silk strand infused with paralyzing neurotoxic venom."
      EssenceValue = 55 }

  let mistStalkerPelt =
    { Id = "trophy_mist_stalker_pelt"
      Name = "Mist Stalker Pelt"
      Description = "Vaporous predator fur that partially phases out of reality in ambient shadow."
      EssenceValue = 140 }

  let calderaCarapace =
    { Id = "trophy_caldera_carapace"
      Name = "Caldera Carapace"
      Description = "Volcanic chitin plates resistant to extreme heat and crushing kinetic blows."
      EssenceValue = 60 }

  let basaltCore =
    { Id = "trophy_basalt_core"
      Name = "Basalt Core"
      Description = "The pulsing obsidian heart of an ancient caldera sentinel."
      EssenceValue = 180 }

  let fiendGlassHorn =
    { Id = "trophy_fiend_glass_horn"
      Name = "Fiend's Glass Horn"
      Description = "A razor-sharp obsidian horn humming with chaotic thermal resonance."
      EssenceValue = 350 }

  let mantisScythe =
    { Id = "trophy_mantis_scythe"
      Name = "Mantis Scythe"
      Description = "An ultra-dense chitinous scythe honed to a monomolecular cutting edge."
      EssenceValue = 70 }

  let tempestFeather =
    { Id = "trophy_tempest_feather"
      Name = "Tempest Feather"
      Description = "A crackling avian quill that vibrates with high-altitude barometric charge."
      EssenceValue = 190 }

  let drakeScale =
    { Id = "trophy_drake_scale"
      Name = "Storm Drake Scale"
      Description = "A shimmering azure dragon scale that deflects lightning and kinetic force."
      EssenceValue = 400 }

  let leechMandible =
    { Id = "trophy_leech_mandible"
      Name = "Leech Mandible"
      Description = "A serrated needle-tooth dripping anticoagulant swamp venom."
      EssenceValue = 75 }

  let barnacleHeart =
    { Id = "trophy_barnacle_heart"
      Name = "Barnacle Heart"
      Description = "A calcified, salt-encrusted heart dripping cold marine floodwater."
      EssenceValue = 210 }

  let specterDust =
    { Id = "trophy_specter_dust"
      Name = "Specter Dust"
      Description = "Luminescent psychic residue left behind by dissolved apparitions of grief."
      EssenceValue = 420 }

  let dawnBellFragment =
    { Id = "trophy_dawn_bell_fragment"
      Name = "Dawn Bell Fragment"
      Description = "A chime fragment of resonant silver that repels psychological despair."
      EssenceValue = 220 }

  let sphinxRiddleTablet =
    { Id = "trophy_sphinx_riddle_tablet"
      Name = "Sphinx Riddle Tablet"
      Description = "A miniature stone tablet deciphering cognitive illusions and mental labyrinths."
      EssenceValue = 450 }

  let seraphicPlume =
    { Id = "trophy_seraphic_plume"
      Name = "Seraphic Plume"
      Description = "A radiant golden celestial feather radiating unwavering composure and poise."
      EssenceValue = 500 }

  let starlightShard =
    { Id = "trophy_starlight_shard"
      Name = "Starlight Shard"
      Description = "A crystalline prism containing the frozen primordial light of a dead star."
      EssenceValue = 460 }

  let voidChitin =
    { Id = "trophy_void_chitin"
      Name = "Void Chitin"
      Description = "A segment of null-space exoskeleton that drinks in ambient photons and sound."
      EssenceValue = 520 }

  let tesseractFragment =
    { Id = "trophy_tesseract_fragment"
      Name = "Tesseract Fragment"
      Description = "A hyperdimensional geometric puzzle-piece vibrating across non-linear time."
      EssenceValue = 600 }

  // ---------------------------------------------------------------------------
  // 2. Biome Bestiary Catalog (21 Unique Species across 7 Biomes)
  // ---------------------------------------------------------------------------

  // --- Biome 1: The Obsidian Quarry Plazas ---
  let slagHound =
    { Id = "slag_hound"
      Name = "Slag Hound"
      Family = MonsterFamily.Beast
      Role = MonsterRole.Swarmer
      NativeBiome = FloorTheme.QuarryPlazas
      Level = 15
      Tier = CombatTier.Novice
      MaxHealth = 1150
      MaxMorale = 950
      ArmorDurability = 45
      Stats = [
        Force, 105; Fortitude, 95; Finesse, 80; Reflex, 75
        Prowess, 70; Poise, 70; Intellect, 35; Resolve, 60
        Acuity, 45; Intuition, 55; Acumen, 40; Composure, 50 ]
      PreferredStance = CombatStance.PowerStance
      PreferredComplexForm = None
      Traits = [ PackTactics 2; MoltenAura 8 ]
      Loot = { MinSouls = 15; MaxSouls = 30; Trophy = Some slagHoundCore; TrophyDropChancePct = 40 }
      Glyph = 'h'
      ColorHex = "#ff5555"
      Description = "Feral quadruped hound formed from cooled iron slag and pulsing core magma. Hunts in packs." }

  let stoneGargoyle =
    { Id = "stone_gargoyle"
      Name = "Stone Gargoyle"
      Family = MonsterFamily.Construct
      Role = MonsterRole.Skirmisher
      NativeBiome = FloorTheme.QuarryPlazas
      Level = 35
      Tier = CombatTier.Veteran
      MaxHealth = 2300
      MaxMorale = 1800
      ArmorDurability = 120
      Stats = [
        Force, 180; Fortitude, 220; Finesse, 230; Reflex, 240
        Prowess, 170; Poise, 200; Intellect, 60; Resolve, 160
        Acuity, 80; Intuition, 140; Acumen, 70; Composure, 150 ]
      PreferredStance = CombatStance.AgilityStance
      PreferredComplexForm = None
      Traits = [ EtherealCarapace 0.20; AcidicBlood 10 ]
      Loot = { MinSouls = 40; MaxSouls = 75; Trophy = Some gargoyleTalisman; TrophyDropChancePct = 35 }
      Glyph = 'g'
      ColorHex = "#6272a4"
      Description = "Petrified stone sentinel perched upon quarry colonnades. Flanks rapidly and sheds sharp granite shards." }

  let quarryOverseer =
    { Id = "quarry_overseer"
      Name = "Quarry Overseer"
      Family = MonsterFamily.Construct
      Role = MonsterRole.Brute
      NativeBiome = FloorTheme.QuarryPlazas
      Level = 75
      Tier = CombatTier.Master
      MaxHealth = 5800
      MaxMorale = 4600
      ArmorDurability = 200
      Stats = [
        Force, 490; Fortitude, 520; Finesse, 340; Reflex, 350
        Prowess, 460; Poise, 420; Intellect, 150; Resolve, 380
        Acuity, 170; Intuition, 300; Acumen, 200; Composure, 390 ]
      PreferredStance = CombatStance.DisciplineStance
      PreferredComplexForm = None
      Traits = [ RelentlessFerocity 25; AcidicBlood 15 ]
      Loot = { MinSouls = 120; MaxSouls = 220; Trophy = Some overseerSeal; TrophyDropChancePct = 50 }
      Glyph = 'O'
      ColorHex = "#f1fa8c"
      Description = "Colossal iron automaton forged to supervise excavation. Wields crushing industrial hammers." }

  // --- Biome 2: The Shrouded Pine Cloisters ---
  let blightSprite =
    { Id = "blight_sprite"
      Name = "Blight Sprite"
      Family = MonsterFamily.Aberration
      Role = MonsterRole.Swarmer
      NativeBiome = FloorTheme.PineCloisters
      Level = 20
      Tier = CombatTier.Novice
      MaxHealth = 1250
      MaxMorale = 1200
      ArmorDurability = 30
      Stats = [
        Force, 50; Fortitude, 60; Finesse, 120; Reflex, 100
        Prowess, 80; Poise, 70; Intellect, 90; Resolve, 80
        Acuity, 130; Intuition, 110; Acumen, 75; Composure, 70 ]
      PreferredStance = CombatStance.AgilityStance
      PreferredComplexForm = Some ComplexForm.PhantasmalDiffusion
      Traits = [ VenomousSting 1; PackTactics 2 ]
      Loot = { MinSouls = 20; MaxSouls = 40; Trophy = Some blightPollen; TrophyDropChancePct = 40 }
      Glyph = 's'
      ColorHex = "#50fa7b"
      Description = "Ephemeral woodland wisp flickering between ancient trunks. Stings with rotting blight spores." }

  let thornWeaver =
    { Id = "thorn_weaver"
      Name = "Thorn Weaver"
      Family = MonsterFamily.Beast
      Role = MonsterRole.Skirmisher
      NativeBiome = FloorTheme.PineCloisters
      Level = 45
      Tier = CombatTier.Veteran
      MaxHealth = 2700
      MaxMorale = 2200
      ArmorDurability = 75
      Stats = [
        Force, 160; Fortitude, 180; Finesse, 280; Reflex, 270
        Prowess, 220; Poise, 190; Intellect, 110; Resolve, 170
        Acuity, 220; Intuition, 200; Acumen, 140; Composure, 180 ]
      PreferredStance = CombatStance.AgilityStance
      PreferredComplexForm = None
      Traits = [ VenomousSting 2; PackTactics 3 ]
      Loot = { MinSouls = 50; MaxSouls = 90; Trophy = Some weaverSilk; TrophyDropChancePct = 45 }
      Glyph = 'w'
      ColorHex = "#8be9fd"
      Description = "Giant predatory arachnid that weaves barbed silk barriers across forest colonnades." }

  let mistStalker =
    { Id = "mist_stalker"
      Name = "Mist Stalker"
      Family = MonsterFamily.Beast
      Role = MonsterRole.Stalker
      NativeBiome = FloorTheme.PineCloisters
      Level = 85
      Tier = CombatTier.Master
      MaxHealth = 5400
      MaxMorale = 4800
      ArmorDurability = 110
      Stats = [
        Force, 420; Fortitude, 440; Finesse, 540; Reflex, 510
        Prowess, 460; Poise, 450; Intellect, 210; Resolve, 360
        Acuity, 380; Intuition, 410; Acumen, 260; Composure, 400 ]
      PreferredStance = CombatStance.AgilityStance
      PreferredComplexForm = None
      Traits = [ RelentlessFerocity 30; PetrifyingGaze 20 ]
      Loot = { MinSouls = 140; MaxSouls = 240; Trophy = Some mistStalkerPelt; TrophyDropChancePct = 40 }
      Glyph = 'S'
      ColorHex = "#bd93f9"
      Description = "Vaporous apex feline that stalks the periphery of vision, lunging from fog with surgical precision." }

  // --- Biome 3: The Basalt Caldera Causeways ---
  let magmaCrawler =
    { Id = "magma_crawler"
      Name = "Magma Crawler"
      Family = MonsterFamily.Beast
      Role = MonsterRole.Swarmer
      NativeBiome = FloorTheme.BasaltCalderas
      Level = 40
      Tier = CombatTier.Veteran
      MaxHealth = 2500
      MaxMorale = 1900
      ArmorDurability = 110
      Stats = [
        Force, 260; Fortitude, 270; Finesse, 160; Reflex, 190
        Prowess, 190; Poise, 210; Intellect, 70; Resolve, 180
        Acuity, 80; Intuition, 130; Acumen, 90; Composure, 170 ]
      PreferredStance = CombatStance.PowerStance
      PreferredComplexForm = None
      Traits = [ MoltenAura 15; PackTactics 2 ]
      Loot = { MinSouls = 45; MaxSouls = 80; Trophy = Some calderaCarapace; TrophyDropChancePct = 45 }
      Glyph = 'c'
      ColorHex = "#ff5555"
      Description = "Heavy crustacean scuttling along basalt causeways, radiating waves of blistering lava heat." }

  let basaltGolem =
    { Id = "basalt_golem"
      Name = "Basalt Golem"
      Family = MonsterFamily.Construct
      Role = MonsterRole.Colossus
      NativeBiome = FloorTheme.BasaltCalderas
      Level = 95
      Tier = CombatTier.Master
      MaxHealth = 6800
      MaxMorale = 5200
      ArmorDurability = 260
      Stats = [
        Force, 590; Fortitude, 620; Finesse, 280; Reflex, 310
        Prowess, 510; Poise, 550; Intellect, 180; Resolve, 460
        Acuity, 200; Intuition, 340; Acumen, 220; Composure, 480 ]
      PreferredStance = CombatStance.DisciplineStance
      PreferredComplexForm = None
      Traits = [ EtherealCarapace 0.25; AcidicBlood 20 ]
      Loot = { MinSouls = 160; MaxSouls = 280; Trophy = Some basaltCore; TrophyDropChancePct = 60 }
      Glyph = 'G'
      ColorHex = "#ffb86c"
      Description = "Imposing monolith of molten obsidian and basalt stone. Shrugs off kinetic shock with ease." }

  let obsidianFiend =
    { Id = "obsidian_fiend"
      Name = "Obsidian Fiend"
      Family = MonsterFamily.Aberration
      Role = MonsterRole.Brute
      NativeBiome = FloorTheme.BasaltCalderas
      Level = 160
      Tier = CombatTier.GrandMaster
      MaxHealth = 11500
      MaxMorale = 9800
      ArmorDurability = 210
      Stats = [
        Force, 940; Fortitude, 920; Finesse, 650; Reflex, 680
        Prowess, 780; Poise, 760; Intellect, 780; Resolve, 720
        Acuity, 690; Intuition, 670; Acumen, 550; Composure, 710 ]
      PreferredStance = CombatStance.PowerStance
      PreferredComplexForm = Some ComplexForm.ResonanceSpike
      Traits = [ RelentlessFerocity 35; MoltenAura 25 ]
      Loot = { MinSouls = 300; MaxSouls = 500; Trophy = Some fiendGlassHorn; TrophyDropChancePct = 50 }
      Glyph = 'F'
      ColorHex = "#ff5555"
      Description = "Demonic entity of rage and scorched glass. Unleashes devastating kinetic shockwaves." }

  // --- Biome 4: The Tempest Promontory Terraces ---
  let cloudMantis =
    { Id = "cloud_mantis"
      Name = "Cloud Mantis"
      Family = MonsterFamily.Beast
      Role = MonsterRole.Skirmisher
      NativeBiome = FloorTheme.TempestTerraces
      Level = 50
      Tier = CombatTier.Veteran
      MaxHealth = 2900
      MaxMorale = 2400
      ArmorDurability = 85
      Stats = [
        Force, 200; Fortitude, 210; Finesse, 310; Reflex, 300
        Prowess, 240; Poise, 220; Intellect, 100; Resolve, 180
        Acuity, 190; Intuition, 210; Acumen, 130; Composure, 190 ]
      PreferredStance = CombatStance.AgilityStance
      PreferredComplexForm = None
      Traits = [ PackTactics 3; VenomousSting 1 ]
      Loot = { MinSouls = 60; MaxSouls = 110; Trophy = Some mantisScythe; TrophyDropChancePct = 40 }
      Glyph = 'm'
      ColorHex = "#8be9fd"
      Description = "Agile insectoid predator gliding on terrace downdrafts. Strikes with twin scythes." }

  let galeHarpy =
    { Id = "gale_harpy"
      Name = "Gale Harpy"
      Family = MonsterFamily.Aberration
      Role = MonsterRole.Caster
      NativeBiome = FloorTheme.TempestTerraces
      Level = 105
      Tier = CombatTier.Master
      MaxHealth = 5900
      MaxMorale = 6400
      ArmorDurability = 90
      Stats = [
        Force, 320; Fortitude, 360; Finesse, 480; Reflex, 470
        Prowess, 410; Poise, 390; Intellect, 510; Resolve, 490
        Acuity, 620; Intuition, 580; Acumen, 450; Composure, 480 ]
      PreferredStance = CombatStance.AgilityStance
      PreferredComplexForm = Some ComplexForm.PhantasmalDiffusion
      Traits = [ PsychicDoldrums 15; PetrifyingGaze 25 ]
      Loot = { MinSouls = 180; MaxSouls = 300; Trophy = Some tempestFeather; TrophyDropChancePct = 45 }
      Glyph = 'H'
      ColorHex = "#50fa7b"
      Description = "Winged screeching phantom riding hurricane updrafts. Disorients prey with psychic shrieks." }

  let stormDrake =
    { Id = "storm_drake"
      Name = "Storm Drake"
      Family = MonsterFamily.Beast
      Role = MonsterRole.Colossus
      NativeBiome = FloorTheme.TempestTerraces
      Level = 175
      Tier = CombatTier.GrandMaster
      MaxHealth = 13500
      MaxMorale = 11200
      ArmorDurability = 280
      Stats = [
        Force, 1020; Fortitude, 980; Finesse, 740; Reflex, 760
        Prowess, 880; Poise, 850; Intellect, 640; Resolve, 790
        Acuity, 710; Intuition, 720; Acumen, 580; Composure, 800 ]
      PreferredStance = CombatStance.DisciplineStance
      PreferredComplexForm = None
      Traits = [ EtherealCarapace 0.30; RelentlessFerocity 30 ]
      Loot = { MinSouls = 350; MaxSouls = 600; Trophy = Some drakeScale; TrophyDropChancePct = 65 }
      Glyph = 'D'
      ColorHex = "#8be9fd"
      Description = "Apex dragon presiding over storm terraces. Deflects kinetic impacts with gale barrier scales." }

  // --- Biome 5: The Sunken Metropolis Boulevards ---
  let mireLeech =
    { Id = "mire_leech"
      Name = "Mire Leech"
      Family = MonsterFamily.Beast
      Role = MonsterRole.Swarmer
      NativeBiome = FloorTheme.SunkenBoulevards
      Level = 55
      Tier = CombatTier.Veteran
      MaxHealth = 3100
      MaxMorale = 2500
      ArmorDurability = 60
      Stats = [
        Force, 330; Fortitude, 310; Finesse, 260; Reflex, 250
        Prowess, 220; Poise, 210; Intellect, 110; Resolve, 220
        Acuity, 180; Intuition, 200; Acumen, 140; Composure, 200 ]
      PreferredStance = CombatStance.PowerStance
      PreferredComplexForm = None
      Traits = [ VenomousSting 2; PackTactics 3 ]
      Loot = { MinSouls = 65; MaxSouls = 120; Trophy = Some leechMandible; TrophyDropChancePct = 45 }
      Glyph = 'l'
      ColorHex = "#bd93f9"
      Description = "Bloated amphibious parasite lurking beneath flooded flagstones. Latches onto unarmored flesh." }

  let drownedHusk =
    { Id = "drowned_husk"
      Name = "Drowned Husk"
      Family = MonsterFamily.UndeadWraith
      Role = MonsterRole.Brute
      NativeBiome = FloorTheme.SunkenBoulevards
      Level = 110
      Tier = CombatTier.Master
      MaxHealth = 6900
      MaxMorale = 6200
      ArmorDurability = 140
      Stats = [
        Force, 540; Fortitude, 660; Finesse, 350; Reflex, 380
        Prowess, 460; Poise, 520; Intellect, 390; Resolve, 680
        Acuity, 360; Intuition, 480; Acumen, 340; Composure, 550 ]
      PreferredStance = CombatStance.DisciplineStance
      PreferredComplexForm = None
      Traits = [ ChillingPresence 15; EtherealCarapace 0.20 ]
      Loot = { MinSouls = 190; MaxSouls = 320; Trophy = Some barnacleHeart; TrophyDropChancePct = 50 }
      Glyph = 'U'
      ColorHex = "#6272a4"
      Description = "Sodden corpse animated by lingering depression. Marches forward with relentless, cold weight." }

  let sirenSpecter =
    { Id = "siren_specter"
      Name = "Siren Specter"
      Family = MonsterFamily.UndeadWraith
      Role = MonsterRole.Caster
      NativeBiome = FloorTheme.SunkenBoulevards
      Level = 180
      Tier = CombatTier.GrandMaster
      MaxHealth = 11800
      MaxMorale = 14200
      ArmorDurability = 80
      Stats = [
        Force, 480; Fortitude, 620; Finesse, 780; Reflex, 810
        Prowess, 680; Poise, 740; Intellect, 980; Resolve, 940
        Acuity, 1040; Intuition, 920; Acumen, 780; Composure, 890 ]
      PreferredStance = CombatStance.AgilityStance
      PreferredComplexForm = Some ComplexForm.PhantasmalDiffusion
      Traits = [ PsychicDoldrums 20; ChillingPresence 20 ]
      Loot = { MinSouls = 380; MaxSouls = 650; Trophy = Some specterDust; TrophyDropChancePct = 60 }
      Glyph = 'W'
      ColorHex = "#bd93f9"
      Description = "Luminescent sorrowful apparition singing melancholic melodies across drowned boulevards." }

  // --- Biome 6: The Elysian Meadow Sanctuaries ---
  let dawnHerald =
    { Id = "dawn_herald"
      Name = "Dawn Herald"
      Family = MonsterFamily.GriefManifestation
      Role = MonsterRole.Skirmisher
      NativeBiome = FloorTheme.ElysianSanctuaries
      Level = 115
      Tier = CombatTier.Master
      MaxHealth = 6500
      MaxMorale = 7200
      ArmorDurability = 130
      Stats = [
        Force, 440; Fortitude, 490; Finesse, 520; Reflex, 550
        Prowess, 580; Poise, 540; Intellect, 520; Resolve, 610
        Acuity, 580; Intuition, 610; Acumen, 690; Composure, 660 ]
      PreferredStance = CombatStance.DisciplineStance
      PreferredComplexForm = Some ComplexForm.AegisLattice
      Traits = [ RelentlessFerocity 25; PackTactics 2 ]
      Loot = { MinSouls = 200; MaxSouls = 350; Trophy = Some dawnBellFragment; TrophyDropChancePct = 50 }
      Glyph = 'd'
      ColorHex = "#f1fa8c"
      Description = "Radiant winged avatar of acceptance. Rings sacred chimes that dismantle hostile aggression." }

  let solarSphinx =
    { Id = "solar_sphinx"
      Name = "Solar Sphinx"
      Family = MonsterFamily.GriefManifestation
      Role = MonsterRole.Caster
      NativeBiome = FloorTheme.ElysianSanctuaries
      Level = 185
      Tier = CombatTier.GrandMaster
      MaxHealth = 12800
      MaxMorale = 14800
      ArmorDurability = 160
      Stats = [
        Force, 620; Fortitude, 780; Finesse, 720; Reflex, 790
        Prowess, 840; Poise, 820; Intellect, 1060; Resolve, 960
        Acuity, 1010; Intuition, 950; Acumen, 890; Composure, 920 ]
      PreferredStance = CombatStance.DisciplineStance
      PreferredComplexForm = Some ComplexForm.ResonanceSpike
      Traits = [ PetrifyingGaze 30; PsychicDoldrums 20 ]
      Loot = { MinSouls = 400; MaxSouls = 700; Trophy = Some sphinxRiddleTablet; TrophyDropChancePct = 65 }
      Glyph = 'X'
      ColorHex = "#f1fa8c"
      Description = "Golden guardian testing travelers with dialectical interrogations of purpose and surrender." }

  let seraphicWarden =
    { Id = "seraphic_warden"
      Name = "Seraphic Warden"
      Family = MonsterFamily.Construct
      Role = MonsterRole.Colossus
      NativeBiome = FloorTheme.ElysianSanctuaries
      Level = 195
      Tier = CombatTier.GrandMaster
      MaxHealth = 15200
      MaxMorale = 13500
      ArmorDurability = 320
      Stats = [
        Force, 890; Fortitude, 960; Finesse, 780; Reflex, 820
        Prowess, 1080; Poise, 1120; Intellect, 720; Resolve, 980
        Acuity, 790; Intuition, 860; Acumen, 880; Composure, 1040 ]
      PreferredStance = CombatStance.DisciplineStance
      PreferredComplexForm = Some ComplexForm.AegisLattice
      Traits = [ EtherealCarapace 0.35; RelentlessFerocity 30 ]
      Loot = { MinSouls = 450; MaxSouls = 800; Trophy = Some seraphicPlume; TrophyDropChancePct = 70 }
      Glyph = 'W'
      ColorHex = "#50fa7b"
      Description = "Towering celestial sentinel clad in gilded filigree plate. Its impenetrable bastion deflects all harm." }

  // --- Biome 7: The Celestial Spire Bridges ---
  let starlitEidolon =
    { Id = "starlit_eidolon"
      Name = "Starlit Eidolon"
      Family = MonsterFamily.UndeadWraith
      Role = MonsterRole.Skirmisher
      NativeBiome = FloorTheme.CelestialSpires
      Level = 190
      Tier = CombatTier.GrandMaster
      MaxHealth = 13000
      MaxMorale = 14000
      ArmorDurability = 120
      Stats = [
        Force, 680; Fortitude, 750; Finesse, 1050; Reflex, 980
        Prowess, 840; Poise, 810; Intellect, 880; Resolve, 920
        Acuity, 1020; Intuition, 940; Acumen, 820; Composure, 910 ]
      PreferredStance = CombatStance.AgilityStance
      PreferredComplexForm = Some ComplexForm.PhantasmalDiffusion
      Traits = [ EtherealCarapace 0.25; PackTactics 2 ]
      Loot = { MinSouls = 420; MaxSouls = 720; Trophy = Some starlightShard; TrophyDropChancePct = 60 }
      Glyph = 'e'
      ColorHex = "#ff79c6"
      Description = "Geometric phantom shimmering with starlight. Teleports across the void with flawless evasion." }

  let voidReaver =
    { Id = "void_reaver"
      Name = "Void Reaver"
      Family = MonsterFamily.Aberration
      Role = MonsterRole.Stalker
      NativeBiome = FloorTheme.CelestialSpires
      Level = 200
      Tier = CombatTier.GrandMaster
      MaxHealth = 14500
      MaxMorale = 13000
      ArmorDurability = 190
      Stats = [
        Force, 1120; Fortitude, 1020; Finesse, 890; Reflex, 880
        Prowess, 940; Poise, 910; Intellect, 820; Resolve, 890
        Acuity, 1060; Intuition, 880; Acumen, 780; Composure, 900 ]
      PreferredStance = CombatStance.PowerStance
      PreferredComplexForm = None
      Traits = [ RelentlessFerocity 40; AcidicBlood 25 ]
      Loot = { MinSouls = 480; MaxSouls = 820; Trophy = Some voidChitin; TrophyDropChancePct = 65 }
      Glyph = 'R'
      ColorHex = "#ff5555"
      Description = "Extraplanar apex carnivore that tears open holes in spatial fabric to ambush prey." }

  let chronoAnomaly =
    { Id = "chrono_anomaly"
      Name = "Chrono-Anomaly"
      Family = MonsterFamily.Aberration
      Role = MonsterRole.Colossus
      NativeBiome = FloorTheme.CelestialSpires
      Level = 210
      Tier = CombatTier.GrandMaster
      MaxHealth = 16500
      MaxMorale = 16000
      ArmorDurability = 260
      Stats = [
        Force, 890; Fortitude, 980; Finesse, 840; Reflex, 880
        Prowess, 1050; Poise, 1020; Intellect, 1150; Resolve, 1080
        Acuity, 1020; Intuition, 990; Acumen, 1100; Composure, 1080 ]
      PreferredStance = CombatStance.DisciplineStance
      PreferredComplexForm = Some ComplexForm.ResonanceSpike
      Traits = [ PsychicDoldrums 25; PetrifyingGaze 35; EtherealCarapace 0.30 ]
      Loot = { MinSouls = 550; MaxSouls = 950; Trophy = Some tesseractFragment; TrophyDropChancePct = 80 }
      Glyph = 'A'
      ColorHex = "#ff79c6"
      Description = "Living fracture in spacetime hovering above the highest spires. Warps turn cadence and focus." }

  // ---------------------------------------------------------------------------
  // 3. Complete Bestiary Registry & Lookup Functions
  // ---------------------------------------------------------------------------
  let allMonsters : MonsterTemplate list = [
    // Quarry Plazas
    slagHound; stoneGargoyle; quarryOverseer
    // Pine Cloisters
    blightSprite; thornWeaver; mistStalker
    // Basalt Calderas
    magmaCrawler; basaltGolem; obsidianFiend
    // Tempest Terraces
    cloudMantis; galeHarpy; stormDrake
    // Sunken Boulevards
    mireLeech; drownedHusk; sirenSpecter
    // Elysian Sanctuaries
    dawnHerald; solarSphinx; seraphicWarden
    // Celestial Spires
    starlitEidolon; voidReaver; chronoAnomaly
  ]

  /// Retrieves all monster templates associated with a specific Tower biome
  let byBiome (biome: FloorTheme) : MonsterTemplate list =
    allMonsters |> List.filter (fun m -> m.NativeBiome = biome)

  /// Looks up a monster template by its unique identifier
  let byId (id: string) : MonsterTemplate option =
    allMonsters |> List.tryFind (fun m -> m.Id = id)

  /// Filters monsters by ecological family
  let byFamily (family: MonsterFamily) : MonsterTemplate list =
    allMonsters |> List.filter (fun m -> m.Family = family)

  /// Filters monsters by tactical role
  let byRole (role: MonsterRole) : MonsterTemplate list =
    allMonsters |> List.filter (fun m -> m.Role = role)

  /// Instantiates a Combatant from a monster template with a newly generated unique ID
  let createMonster (template: MonsterTemplate) : Combatant =
    template.ToCombatant (CombatantId.New())

  /// Rolls for monster loot upon defeat: returns (souls, trophy option)
  let rollLoot (roller: int -> int -> int) (monster: MonsterTemplate) : int * AlchemicalTrophy option =
    let souls = roller monster.Loot.MinSouls monster.Loot.MaxSouls
    let trophyOpt =
      match monster.Loot.Trophy with
      | Some trophy ->
        let roll = roller 1 100
        if roll <= monster.Loot.TrophyDropChancePct then Some trophy else None
      | None -> None
    souls, trophyOpt
