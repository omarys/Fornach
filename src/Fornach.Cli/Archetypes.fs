namespace Fornach.Cli

open System
open Fornach.Domain

module Archetypes =

  // =========================================================================
  // Veteran Tier (Level 40: Stats ~200, HP/Morale 2400–3600, Armor 55–85)
  // =========================================================================

  let createVeteranJuggernaut () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 201; Fortitude, 201
        Prowess, 123; Poise, 123
        Finesse, 74; Reflex, 74
        Intellect, 74; Resolve, 74
        Acuity, 74; Intuition, 74
        Acumen, 74; Composure, 74
      ]
    { Combatant.create id "Veteran Juggernaut" 3575 2400 stats with
        Class = CharacterClass.Juggernaut
        Armor = ArmorIntegrity.Create 84
        Stance = CombatStance.PowerStance }

  let createVeteranDuelist () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Finesse, 201; Reflex, 201
        Prowess, 123; Poise, 123
        Force, 74; Fortitude, 74
        Intellect, 74; Resolve, 74
        Acuity, 74; Intuition, 74
        Acumen, 74; Composure, 74
      ]
    { Combatant.create id "Veteran Duelist" 3575 2400 stats with
        Class = CharacterClass.Duelist
        Armor = ArmorIntegrity.Create 84
        Stance = CombatStance.AgilityStance }

  let createVeteranInquisitor () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Intellect, 201; Resolve, 201
        Acuity, 123; Intuition, 123
        Acumen, 74; Composure, 74
        Force, 74; Fortitude, 74
        Finesse, 74; Reflex, 74
        Prowess, 74; Poise, 74
      ]
    { Combatant.create id "Veteran Inquisitor" 2400 3575 stats with
        Class = CharacterClass.Inquisitor
        Armor = ArmorIntegrity.Create 54
        Stance = CombatStance.AgilityStance }

  let createVeteranMesmer () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Acuity, 201; Intuition, 201
        Intellect, 123; Resolve, 123
        Force, 74; Fortitude, 74
        Finesse, 74; Reflex, 74
        Prowess, 74; Poise, 74
        Acumen, 74; Composure, 74
      ]
    { Combatant.create id "Veteran Mesmer" 2400 3575 stats with
        Class = CharacterClass.Mesmer
        Armor = ArmorIntegrity.Create 54
        Stance = CombatStance.AgilityStance }

  let createVeteranAbjurer () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Acumen, 201; Composure, 201
        Prowess, 123; Poise, 123
        Force, 74; Fortitude, 74
        Finesse, 74; Reflex, 74
        Intellect, 74; Resolve, 74
        Acuity, 74; Intuition, 74
      ]
    { Combatant.create id "Veteran Abjurer" 2400 3575 stats with
        Class = CharacterClass.Abjurer
        Armor = ArmorIntegrity.Create 54
        Stance = CombatStance.DisciplineStance }

  // =========================================================================
  // Master Tier (Level 100: Stats ~440, HP/Morale 5400–8100, Armor 114–174)
  // =========================================================================

  let createMasterJuggernaut () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 441; Fortitude, 441
        Prowess, 273; Poise, 273
        Finesse, 164; Reflex, 164
        Intellect, 164; Resolve, 164
        Acuity, 164; Intuition, 164
        Acumen, 164; Composure, 164
      ]
    { Combatant.create id "Master Juggernaut" 8075 5400 stats with
        Class = CharacterClass.Juggernaut
        Armor = ArmorIntegrity.Create 174
        Stance = CombatStance.PowerStance }

  let createMasterInquisitor () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Intellect, 441; Resolve, 441
        Acuity, 273; Intuition, 273
        Acumen, 164; Composure, 164
        Force, 164; Fortitude, 164
        Finesse, 164; Reflex, 164
        Prowess, 164; Poise, 164
      ]
    { Combatant.create id "Master Inquisitor" 5400 8075 stats with
        Class = CharacterClass.Inquisitor
        Armor = ArmorIntegrity.Create 114
        Stance = CombatStance.PowerStance }

  // =========================================================================
  // GrandMaster Tier (Level 200, 6 Preparations, Max Stats 840+)
  // =========================================================================

  let createGrandMasterBerserker () = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster
  let createGrandMasterJuggernaut () = TierFactory.createClassTier CharacterClass.Juggernaut CombatTier.GrandMaster
  let createGrandMasterInquisitor () = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
  let createGrandMasterDuelist () = TierFactory.createClassTier CharacterClass.Duelist CombatTier.GrandMaster
  let createGrandMasterAssassin () = TierFactory.createClassTier CharacterClass.Assassin CombatTier.GrandMaster
  let createGrandMasterMesmer () = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster
  let createGrandMasterWarden () = TierFactory.createClassTier CharacterClass.Warden CombatTier.GrandMaster
  let createGrandMasterJusticar () = TierFactory.createClassTier CharacterClass.Justicar CombatTier.GrandMaster
  let createGrandMasterRanger () = TierFactory.createClassTier CharacterClass.Ranger CombatTier.GrandMaster
  let createGrandMasterAbjurer () = TierFactory.createClassTier CharacterClass.Abjurer CombatTier.GrandMaster
  let createGrandMasterStrategist = createGrandMasterAbjurer

  let createGrandmasterBerserker = createGrandMasterBerserker
  let createGrandmasterJuggernaut = createGrandMasterJuggernaut
  let createGrandmasterTitan = createGrandMasterBerserker
  let createGrandmasterInquisitor = createGrandMasterInquisitor
  let createGrandmasterDuelist = createGrandMasterDuelist
  let createGrandmasterAssassin = createGrandMasterAssassin
  let createGrandmasterMesmer = createGrandMasterMesmer
  let createGrandmasterWarden = createGrandMasterWarden
  let createGrandmasterJusticar = createGrandMasterJusticar
  let createGrandmasterRanger = createGrandMasterRanger
  let createGrandmasterAbjurer = createGrandMasterAbjurer
  let createGrandmasterStrategist = createGrandMasterAbjurer

  let createClassTier (cls: CharacterClass) (tier: CombatTier) () =
    TierFactory.createClassTier cls tier

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
    { Name = "Veteran Juggernaut"
      Tier = Veteran
      Discipline = CombatMode.Physical
      Description = "Seasoned martial warrior commanding heavy Force and stance pressure."
      Factory = createVeteranJuggernaut }

    { Name = "Veteran Duelist"
      Tier = Veteran
      Discipline = CombatMode.Physical
      Description = "Agile fencer executing probing Finesse strikes, seeking critical vital openings."
      Factory = createVeteranDuelist }

    { Name = "Veteran Inquisitor"
      Tier = Veteran
      Discipline = CombatMode.Arcane
      Description = "Psionic mystic shredding mental defenses via raw Intellect and Acuity."
      Factory = createVeteranInquisitor }

    { Name = "Veteran Mesmer"
      Tier = Veteran
      Discipline = CombatMode.Arcane
      Description = "Guile illusionist conjuring decoy mirror swarms and disorienting glamours."
      Factory = createVeteranMesmer }

    { Name = "Veteran Abjurer"
      Tier = Veteran
      Discipline = CombatMode.Arcane
      Description = "Discipline abjurer commanding defensive wards and posture-shattering shockwaves."
      Factory = createVeteranAbjurer }

    { Name = "Veteran Warden"
      Tier = Veteran
      Discipline = CombatMode.Physical
      Description = "Discipline/Power/Agility bastion knight commanding Bastion Zone Control."
      Factory = createClassTier CharacterClass.Warden CombatTier.Veteran }

    { Name = "Veteran Ranger"
      Tier = Veteran
      Discipline = CombatMode.Physical
      Description = "Discipline/Agility/Power wild sentinel weaving reactive intercepts and skirmishing."
      Factory = createClassTier CharacterClass.Ranger CombatTier.Veteran }

    { Name = "Master Juggernaut"
      Tier = Master
      Discipline = CombatMode.Physical
      Description = "Uncapped martial behemoth capable of crushing even expert shields in one blow."
      Factory = createMasterJuggernaut }

    { Name = "Master Inquisitor"
      Tier = Master
      Discipline = CombatMode.Arcane
      Description = "Ascendant psion with staggering psychic projection and cataclysmic surges."
      Factory = createMasterInquisitor }

    { Name = "Master Warden"
      Tier = Master
      Discipline = CombatMode.Physical
      Description = "Discipline/Power/Agility bastion master with impenetrable zone control."
      Factory = createClassTier CharacterClass.Warden CombatTier.Master }

    { Name = "Master Ranger"
      Tier = Master
      Discipline = CombatMode.Physical
      Description = "Discipline/Agility/Power master scout with fluid reactive defense."
      Factory = createClassTier CharacterClass.Ranger CombatTier.Master }

    { Name = "Master Abjurer"
      Tier = Master
      Discipline = CombatMode.Arcane
      Description = "Discipline/Mental grand abjurer placing destabilizing wards and composure barriers."
      Factory = createClassTier CharacterClass.Abjurer CombatTier.Master }

    // =========================================================================
    // GrandMaster Tier (Level 200, 6 Preparations, Max Stats 840+)
    // =========================================================================

    { Name = "Grandmaster Berserker"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Power/Agility/Discipline (1.0 : 0.75 : 0.50): kinetic slayer wielding wild momentum and cleaves."
      Factory = createGrandMasterBerserker }

    { Name = "Grandmaster Juggernaut"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Power/Discipline/Agility (1.0 : 0.75 : 0.50): iron colossus with heavy armor and sundering impacts."
      Factory = createGrandMasterJuggernaut }

    { Name = "Grandmaster Inquisitor"
      Tier = GrandMaster
      Discipline = CombatMode.Arcane
      Description = "Power/Mental GrandMaster: psychic dread arcanist unleashing Dread Warhorn and Synaptic Brand."
      Factory = createGrandMasterInquisitor }

    { Name = "Grandmaster Duelist"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Agility/Discipline/Power (1.0 : 0.75 : 0.50): technical fencer deploying Caltrop Pouches (2 turns) and parries."
      Factory = createGrandMasterDuelist }

    { Name = "Grandmaster Assassin"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Agility/Power/Discipline (1.0 : 0.75 : 0.50): lethal shadow striker deploying Concealed Blades and burst."
      Factory = createGrandMasterAssassin }

    { Name = "Grandmaster Mesmer"
      Tier = GrandMaster
      Discipline = CombatMode.Arcane
      Description = "Agility/Mental GrandMaster: phantasmist weaving Mirror Mirages, decoy swaps, and Prismatic Flares."
      Factory = createGrandMasterMesmer }

    { Name = "Grandmaster Warden"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Discipline/Power/Agility (1.0 : 0.75 : 0.50): bastion knight planting Bastion Zone Control and polearms."
      Factory = createGrandMasterWarden }

    { Name = "Grandmaster Justicar"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Discipline/Physical GrandMaster (legacy alias for Warden): knight warder planting Bastion Zone Control."
      Factory = createGrandMasterJusticar }

    { Name = "Grandmaster Ranger"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Discipline/Agility/Power (1.0 : 0.75 : 0.50): wild sentinel weaving reactive intercepts and skirmishing."
      Factory = createGrandMasterRanger }

    { Name = "Grandmaster Abjurer"
      Tier = GrandMaster
      Discipline = CombatMode.Arcane
      Description = "Discipline/Mental GrandMaster: runic warder commanding Heraldic Treatises and destabilizing ground glyphs."
      Factory = createGrandMasterAbjurer }

    { Name = "Grandmaster Strategist"
      Tier = GrandMaster
      Discipline = CombatMode.Arcane
      Description = "Discipline/Mental GrandMaster (legacy alias for Abjurer): tactical mastermind."
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
