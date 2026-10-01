namespace Fornach.Cli

open System
open Fornach.Domain

module Archetypes =

  // =========================================================================
  // Archetype Tier Factories
  // =========================================================================

  let createClassTier (cls: CharacterClass) (tier: CombatTier) () =
    TierFactory.createClassTier cls tier

  let createVeteranBerserker () = TierFactory.createClassTier CharacterClass.Berserker CombatTier.Veteran
  let createVeteranDuelist () = TierFactory.createClassTier CharacterClass.Duelist CombatTier.Veteran
  let createVeteranWarden () = TierFactory.createClassTier CharacterClass.Warden CombatTier.Veteran
  let createVeteranInquisitor () = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.Veteran
  let createVeteranMesmer () = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.Veteran
  let createVeteranAbjurer () = TierFactory.createClassTier CharacterClass.Abjurer CombatTier.Veteran

  let createMasterBerserker () = TierFactory.createClassTier CharacterClass.Berserker CombatTier.Master
  let createMasterDuelist () = TierFactory.createClassTier CharacterClass.Duelist CombatTier.Master
  let createMasterWarden () = TierFactory.createClassTier CharacterClass.Warden CombatTier.Master
  let createMasterInquisitor () = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.Master
  let createMasterMesmer () = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.Master
  let createMasterAbjurer () = TierFactory.createClassTier CharacterClass.Abjurer CombatTier.Master

  let createGrandMasterBerserker () = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster
  let createGrandMasterInquisitor () = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
  let createGrandMasterDuelist () = TierFactory.createClassTier CharacterClass.Duelist CombatTier.GrandMaster
  let createGrandMasterMesmer () = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster
  let createGrandMasterWarden () = TierFactory.createClassTier CharacterClass.Warden CombatTier.GrandMaster
  let createGrandMasterJusticar () = TierFactory.createClassTier CharacterClass.Justicar CombatTier.GrandMaster
  let createGrandMasterAbjurer () = TierFactory.createClassTier CharacterClass.Abjurer CombatTier.GrandMaster
  let createGrandMasterStrategist = createGrandMasterAbjurer

  let createGrandmasterBerserker = createGrandMasterBerserker
  let createGrandmasterTitan = createGrandMasterBerserker
  let createGrandmasterInquisitor = createGrandMasterInquisitor
  let createGrandmasterDuelist = createGrandMasterDuelist
  let createGrandmasterMesmer = createGrandMasterMesmer
  let createGrandmasterWarden = createGrandMasterWarden
  let createGrandmasterJusticar = createGrandMasterJusticar
  let createGrandmasterAbjurer = createGrandMasterAbjurer
  let createGrandmasterStrategist = createGrandMasterAbjurer

  // =========================================================================
  // Generic NPC Archetypes (Novice Tier, Level 1, Zero Preparations)
  // =========================================================================

  let createNoviceWarrior () =
    let c = TierFactory.createClassLevel CharacterClass.Warrior 1
    { c with Name = "Warrior" }

  let createNoviceRogue () =
    let c = TierFactory.createClassLevel CharacterClass.Rogue 1
    { c with Name = "Rogue" }

  let createNoviceSoldier () =
    let c = TierFactory.createClassLevel CharacterClass.Soldier 1
    { c with Name = "Soldier" }

  let createNoviceMage () =
    let c = TierFactory.createClassLevel CharacterClass.Mage 1
    { c with Name = "Mage" }

  // =========================================================================
  // Roster Registry
  // =========================================================================

  let allArchetypes : ArchetypeInfo list = [
    { Name = "Veteran Berserker"
      Tier = Veteran
      Discipline = CombatMode.Physical
      Description = "Power Primary (1.0 : 0.75 : 0.75): seasoned kinetic slayer commanding heavy Force and cleaves."
      Factory = createVeteranBerserker }

    { Name = "Veteran Duelist"
      Tier = Veteran
      Discipline = CombatMode.Physical
      Description = "Agility Primary (1.0 : 0.75 : 0.75): agile fencer executing probing cadences seeking vital openings."
      Factory = createVeteranDuelist }

    { Name = "Veteran Warden"
      Tier = Veteran
      Discipline = CombatMode.Physical
      Description = "Discipline Primary (1.0 : 0.75 : 0.75): bastion knight commanding zone control and unyielding defense."
      Factory = createVeteranWarden }

    { Name = "Veteran Inquisitor"
      Tier = Veteran
      Discipline = CombatMode.Arcane
      Description = "Power Primary (1.0 : 0.75 : 0.75): psychic mystic shredding mental defenses via raw Intellect."
      Factory = createVeteranInquisitor }

    { Name = "Veteran Mesmer"
      Tier = Veteran
      Discipline = CombatMode.Arcane
      Description = "Agility Primary (1.0 : 0.75 : 0.75): guile illusionist conjuring decoy mirror swarms and sensory static."
      Factory = createVeteranMesmer }

    { Name = "Veteran Abjurer"
      Tier = Veteran
      Discipline = CombatMode.Arcane
      Description = "Discipline Primary (1.0 : 0.75 : 0.75): discipline abjurer commanding defensive wards and posture shockwaves."
      Factory = createVeteranAbjurer }

    { Name = "Master Berserker"
      Tier = Master
      Discipline = CombatMode.Physical
      Description = "Power Primary (1.0 : 0.75 : 0.75): unstoppable martial juggernaut wielding sweeping momentum."
      Factory = createMasterBerserker }

    { Name = "Master Duelist"
      Tier = Master
      Discipline = CombatMode.Physical
      Description = "Agility Primary (1.0 : 0.75 : 0.75): fencing master with razor precision and lethal counter-openings."
      Factory = createMasterDuelist }

    { Name = "Master Warden"
      Tier = Master
      Discipline = CombatMode.Physical
      Description = "Discipline Primary (1.0 : 0.75 : 0.75): bastion master commanding impenetrable zone control."
      Factory = createMasterWarden }

    { Name = "Master Inquisitor"
      Tier = Master
      Discipline = CombatMode.Arcane
      Description = "Power Primary (1.0 : 0.75 : 0.75): ascendant psion with staggering psychic projection."
      Factory = createMasterInquisitor }

    { Name = "Master Mesmer"
      Tier = Master
      Discipline = CombatMode.Arcane
      Description = "Agility Primary (1.0 : 0.75 : 0.75): master phantasmist weaving intricate mirror labyrinths."
      Factory = createMasterMesmer }

    { Name = "Master Abjurer"
      Tier = Master
      Discipline = CombatMode.Arcane
      Description = "Discipline Primary (1.0 : 0.75 : 0.75): grand abjurer placing destabilizing ground glyphs and barriers."
      Factory = createMasterAbjurer }

    // =========================================================================
    // GrandMaster Tier (Level 200, 6 Preparations, Max Stats 840+)
    // =========================================================================

    { Name = "Grandmaster Berserker"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Power Primary (1.0 : 0.75 : 0.75): kinetic slayer wielding wild momentum and cleaves."
      Factory = createGrandMasterBerserker }

    { Name = "Grandmaster Inquisitor"
      Tier = GrandMaster
      Discipline = CombatMode.Arcane
      Description = "Power Primary (1.0 : 0.75 : 0.75): psychic dread arcanist unleashing Dread Warhorn and Synaptic Brand."
      Factory = createGrandMasterInquisitor }

    { Name = "Grandmaster Duelist"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Agility Primary (1.0 : 0.75 : 0.75): master fencer deploying Caltrop Pouches, seeking vital openings."
      Factory = createGrandMasterDuelist }

    { Name = "Grandmaster Mesmer"
      Tier = GrandMaster
      Discipline = CombatMode.Arcane
      Description = "Agility Primary (1.0 : 0.75 : 0.75): phantasmist weaving Mirror Mirages, decoy swaps, and Prismatic Flares."
      Factory = createGrandMasterMesmer }

    { Name = "Grandmaster Warden"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Discipline Primary (1.0 : 0.75 : 0.75): bastion knight planting Bastion Zone Control and polearms."
      Factory = createGrandMasterWarden }

    { Name = "Grandmaster Justicar"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Discipline Primary (legacy alias for Warden): knight warder planting Bastion Zone Control."
      Factory = createGrandMasterJusticar }

    { Name = "Grandmaster Abjurer"
      Tier = GrandMaster
      Discipline = CombatMode.Arcane
      Description = "Discipline Primary (1.0 : 0.75 : 0.75): runic warder commanding Heraldic Treatises and ground glyphs."
      Factory = createGrandMasterAbjurer }

    { Name = "Grandmaster Strategist"
      Tier = GrandMaster
      Discipline = CombatMode.Arcane
      Description = "Discipline Primary (legacy alias for Abjurer): tactical mastermind."
      Factory = createGrandMasterStrategist }

    // =========================================================================
    // Generic NPC Archetypes (Novice Tier, Level 1, Zero Preparations)
    // =========================================================================

    { Name = "Warrior"
      Tier = Novice
      Discipline = CombatMode.Physical
      Description = "Generic Power NPC: front-line brute with kinetic Force and fortitude (no preparations)."
      Factory = createNoviceWarrior }

    { Name = "Rogue"
      Tier = Novice
      Discipline = CombatMode.Physical
      Description = "Generic Finesse NPC: agile skirmisher relying on Finesse and speed (no preparations)."
      Factory = createNoviceRogue }

    { Name = "Soldier"
      Tier = Novice
      Discipline = CombatMode.Physical
      Description = "Generic Discipline NPC: rank-and-file guard maintaining formation and Prowess (no preparations)."
      Factory = createNoviceSoldier }

    { Name = "Mage"
      Tier = Novice
      Discipline = CombatMode.Arcane
      Description = "Generic Magic NPC: arcane practitioner channeling raw psychic energy (no preparations)."
      Factory = createNoviceMage }
  ]

  let findByName (name: string) : ArchetypeInfo option =
    let trimmed = name.Trim().ToLowerInvariant()
    allArchetypes
    |> List.tryFind (fun a -> a.Name.ToLowerInvariant() = trimmed)
    |> Option.orElseWith (fun () ->
      allArchetypes
      |> List.tryFind (fun a -> a.Name.ToLowerInvariant().Contains(trimmed)))

  let createCustom (name: string) (hp: int) (morale: int) (armor: int) (stats: (StatId * int) seq) : Combatant =
    let id = CombatantId.New()
    let statBlock = StatBlock.Create stats
    { Combatant.create id name hp morale statBlock with
        Armor = ArmorIntegrity.Create armor }
