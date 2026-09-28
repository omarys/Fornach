namespace Fornach.Cli

open System
open Fornach.Domain

module Archetypes =

  // =========================================================================
  // Novice Tier (Stats 35–50, HP/Morale 400–650)
  // =========================================================================

  let createIronRecruit () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 45; Fortitude, 40
        Finesse, 25; Reflex, 25
        Prowess, 30; Poise, 30
        Intellect, 15; Resolve, 20
        Acuity, 15; Intuition, 15
        Acumen, 15; Composure, 20
      ]
    { Combatant.create id "Iron Recruit" 600 400 stats with
        Armor = ArmorIntegrity.Create 25 }

  let createCourtScribe () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 15; Fortitude, 20
        Finesse, 20; Reflex, 20
        Prowess, 15; Poise, 20
        Intellect, 25; Resolve, 30
        Acuity, 45; Intuition, 40
        Acumen, 30; Composure, 35
      ]
    { Combatant.create id "Court Scribe" 400 650 stats with
        Armor = ArmorIntegrity.Create 10 }

  let createHedgeMage () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 15; Fortitude, 15
        Finesse, 20; Reflex, 20
        Prowess, 15; Poise, 20
        Intellect, 45; Resolve, 30
        Acuity, 25; Intuition, 25
        Acumen, 40; Composure, 35
      ]
    { Combatant.create id "Hedge Mage" 450 600 stats with
        Armor = ArmorIntegrity.Create 15 }

  // =========================================================================
  // =========================================================================
  // Veteran Tier (Level 40: Stats ~200, HP/Morale 2400–3600, Armor 55–85)
  // =========================================================================

  let createTheron () =
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
    { Combatant.create id "Theron (Iron Vanguard)" 3575 2400 stats with
        Armor = ArmorIntegrity.Create 84
        Stance = CombatStance.PowerStance }

  let createSilverFencer () =
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
    { Combatant.create id "Lyra (Silver Fencer)" 3575 2400 stats with
        Armor = ArmorIntegrity.Create 84
        Stance = CombatStance.AgilityStance }

  let createAurelius () =
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
    { Combatant.create id "Aurelius (Thought-Weaver)" 2400 3575 stats with
        Armor = ArmorIntegrity.Create 54
        Stance = CombatStance.AgilityStance }

  let createLadyVane () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Acumen, 201; Composure, 201
        Intellect, 123; Resolve, 123
        Force, 74; Fortitude, 74
        Finesse, 74; Reflex, 74
        Prowess, 74; Poise, 74
        Acuity, 74; Intuition, 74
      ]
    { Combatant.create id "Lady Vane (High Magistrate)" 2400 3575 stats with
        Armor = ArmorIntegrity.Create 54
        Stance = CombatStance.DisciplineStance }

  let createMirageWeaver () =
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
    { Combatant.create id "Seraphina (Mirage Weaver)" 2400 3575 stats with
        Armor = ArmorIntegrity.Create 54
        Stance = CombatStance.AgilityStance }

  let createRunicAbjurer () =
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
    { Combatant.create id "Kaelen (Runic Abjurer)" 2400 3575 stats with
        Armor = ArmorIntegrity.Create 54
        Stance = CombatStance.DisciplineStance }

  // =========================================================================
  // Master Tier (Level 100: Stats ~440, HP/Morale 5400–8100, Armor 114–174)
  // =========================================================================

  let createWarmaster () =
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
    { Combatant.create id "Valerius (Grand Warmaster)" 8075 5400 stats with
        Armor = ArmorIntegrity.Create 174
        Stance = CombatStance.PowerStance }

  let createArchDiviner () =
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
    { Combatant.create id "Ignis (Arch-Diviner)" 5400 8075 stats with
        Armor = ArmorIntegrity.Create 114
        Stance = CombatStance.PowerStance }

  let createChancellor () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Acumen, 441; Composure, 441
        Intellect, 273; Intuition, 273
        Force, 164; Fortitude, 164
        Finesse, 164; Reflex, 164
        Prowess, 164; Poise, 164
        Acuity, 164; Resolve, 164
      ]
    { Combatant.create id "Chancellor Malakor" 5400 8075 stats with
        Armor = ArmorIntegrity.Create 114
        Stance = CombatStance.DisciplineStance }

  // =========================================================================
  // GrandMaster Tier (Level 200, 6 Preparations, Max Stats 840+)
  // =========================================================================

  let createGrandMasterBerserker () = TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster
  let createGrandMasterInquisitor () = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
  let createGrandMasterDuelist () = TierFactory.createClassTier CharacterClass.Duelist CombatTier.GrandMaster
  let createGrandMasterMesmer () = TierFactory.createClassTier CharacterClass.Mesmer CombatTier.GrandMaster
  let createGrandMasterJusticar () = TierFactory.createClassTier CharacterClass.Justicar CombatTier.GrandMaster
  let createGrandMasterStrategist () = TierFactory.createClassTier CharacterClass.Strategist CombatTier.GrandMaster

  let createGrandmasterBerserker = createGrandMasterBerserker
  let createGrandmasterTitan = createGrandMasterBerserker
  let createGrandmasterInquisitor = createGrandMasterInquisitor
  let createGrandmasterDuelist = createGrandMasterDuelist
  let createGrandmasterMesmer = createGrandMasterMesmer
  let createGrandmasterJusticar = createGrandMasterJusticar
  let createGrandmasterStrategist = createGrandMasterStrategist

  // =========================================================================
  // Generic NPC Archetypes (Novice Tier, Level 1, Zero Preparations)
  // =========================================================================

  let createNoviceWarrior () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 45; Fortitude, 45
        Prowess, 25; Poise, 25
        Finesse, 15; Reflex, 15
        Intellect, 15; Resolve, 15
        Acuity, 15; Intuition, 15
        Acumen, 15; Composure, 15
      ]
    { Combatant.createWithClass id "Warrior" 650 450 stats CharacterClass.Warrior 1 with
        Armor = ArmorIntegrity.Create 25
        Stance = CombatStance.PowerStance }

  let createNoviceAssassin () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Finesse, 45; Reflex, 45
        Prowess, 25; Poise, 25
        Force, 15; Fortitude, 15
        Intellect, 15; Resolve, 15
        Acuity, 15; Intuition, 15
        Acumen, 15; Composure, 15
      ]
    { Combatant.createWithClass id "Assassin" 650 450 stats CharacterClass.Assassin 1 with
        Armor = ArmorIntegrity.Create 25
        Stance = CombatStance.AgilityStance }

  let createNoviceSoldier () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Prowess, 45; Poise, 45
        Fortitude, 25; Force, 25
        Finesse, 15; Reflex, 15
        Intellect, 15; Resolve, 15
        Acuity, 15; Intuition, 15
        Acumen, 15; Composure, 15
      ]
    { Combatant.createWithClass id "Soldier" 650 450 stats CharacterClass.Soldier 1 with
        Armor = ArmorIntegrity.Create 25
        Stance = CombatStance.DisciplineStance }

  let createNoviceMage () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Intellect, 45; Resolve, 45
        Acumen, 25; Composure, 25
        Force, 15; Fortitude, 15
        Finesse, 15; Reflex, 15
        Prowess, 15; Poise, 15
        Acuity, 15; Intuition, 15
      ]
    { Combatant.createWithClass id "Mage" 450 650 stats CharacterClass.Mage 1 with
        Armor = ArmorIntegrity.Create 15
        Stance = CombatStance.PowerStance }

  // =========================================================================
  // Roster Registry
  // =========================================================================

  let allArchetypes : ArchetypeInfo list = [
    { Name = "Iron Recruit"
      Tier = Novice
      Discipline = CombatMode.Physical
      Description = "Novice soldier with baseline kinetic cleave and shield work."
      Factory = createIronRecruit }

    { Name = "Court Scribe"
      Tier = Novice
      Discipline = CombatMode.Social
      Description = "Adept at rhetorical fencing, spotting telltale contradictions."
      Factory = createCourtScribe }

    { Name = "Hedge Mage"
      Tier = Novice
      Discipline = CombatMode.Arcane
      Description = "Apprentice channeler utilizing direct mental static and basic wards."
      Factory = createHedgeMage }

    { Name = "Iron Vanguard"
      Tier = Veteran
      Discipline = CombatMode.Physical
      Description = "Seasoned martial warrior commanding heavy Force and stance pressure."
      Factory = createTheron }

    { Name = "Silver Fencer"
      Tier = Veteran
      Discipline = CombatMode.Physical
      Description = "Agile fencer executing probing Finesse strikes, seeking critical vital openings."
      Factory = createSilverFencer }

    { Name = "Thought-Weaver"
      Tier = Veteran
      Discipline = CombatMode.Arcane
      Description = "Psionic mystic shredding mental defenses via raw Intellect and Acuity."
      Factory = createAurelius }

    { Name = "Mirage Weaver"
      Tier = Veteran
      Discipline = CombatMode.Arcane
      Description = "Guile illusionist conjuring decoy mirror swarms and disorienting glamours."
      Factory = createMirageWeaver }

    { Name = "Runic Abjurer"
      Tier = Veteran
      Discipline = CombatMode.Arcane
      Description = "Discipline abjurer commanding defensive wards and posture-shattering shockwaves."
      Factory = createRunicAbjurer }

    { Name = "High Magistrate"
      Tier = Veteran
      Discipline = CombatMode.Social
      Description = "Aristocratic orator dismantling composure with procedural leverage."
      Factory = createLadyVane }

    { Name = "Grand Warmaster"
      Tier = Master
      Discipline = CombatMode.Physical
      Description = "Uncapped martial behemoth capable of crushing even expert shields in one blow."
      Factory = createWarmaster }

    { Name = "Arch-Diviner"
      Tier = Master
      Discipline = CombatMode.Arcane
      Description = "Ascendant psion with staggering psychic projection and cataclysmic surges."
      Factory = createArchDiviner }

    { Name = "Chancellor Malakor"
      Tier = Master
      Discipline = CombatMode.Social
      Description = "Formidable court titan who commands unyielding authority and lethal scrutiny."
      Factory = createChancellor }

    // =========================================================================
    // GrandMaster Tier (Level 200, 6 Preparations, Max Stats 840+)
    // =========================================================================

    { Name = "Grandmaster Berserker"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Power/Physical GrandMaster: kinetic juggernaut wielding Shockwave Slam and Cleaves."
      Factory = createGrandMasterBerserker }

    { Name = "Grandmaster Inquisitor"
      Tier = GrandMaster
      Discipline = CombatMode.Arcane
      Description = "Power/Mental GrandMaster: psychic dread arcanist unleashing Dread Warhorn and Synaptic Brand."
      Factory = createGrandMasterInquisitor }

    { Name = "Grandmaster Duelist"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Agility/Physical GrandMaster: vital skirmisher deploying Caltrop Pouches and Concealed Blades."
      Factory = createGrandMasterDuelist }

    { Name = "Grandmaster Mesmer"
      Tier = GrandMaster
      Discipline = CombatMode.Arcane
      Description = "Agility/Mental GrandMaster: phantasmist weaving Mirror Mirages and deadly NeuroToxins."
      Factory = createGrandMasterMesmer }

    { Name = "Grandmaster Justicar"
      Tier = GrandMaster
      Discipline = CombatMode.Physical
      Description = "Discipline/Physical GrandMaster: knight warder planting Bastion Zone Control and Parrying Bucklers."
      Factory = createGrandMasterJusticar }

    { Name = "Grandmaster Strategist"
      Tier = GrandMaster
      Discipline = CombatMode.Social
      Description = "Discipline/Mental GrandMaster: dialectician commanding Heraldic Treatises and Socratic Dossiers."
      Factory = createGrandMasterStrategist }

    // =========================================================================
    // Generic NPC Archetypes (Novice Tier, Level 1, Zero Preparations)
    // =========================================================================

    { Name = "Warrior"
      Tier = Novice
      Discipline = CombatMode.Physical
      Description = "Generic Power NPC: front-line brute with kinetic Force and fortitude (no preparations)."
      Factory = createNoviceWarrior }

    { Name = "Assassin"
      Tier = Novice
      Discipline = CombatMode.Physical
      Description = "Generic Finesse NPC: agile skirmisher relying on Finesse and speed (no preparations)."
      Factory = createNoviceAssassin }

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
    |> List.tryFind (fun a ->
      a.Name.ToLowerInvariant() = trimmed
      || a.Name.ToLowerInvariant().Contains(trimmed))

  let createCustom (name: string) (hp: int) (morale: int) (armor: int) (stats: (StatId * int) seq) : Combatant =
    let id = CombatantId.New()
    let statBlock = StatBlock.Create stats
    { Combatant.create id name hp morale statBlock with
        Armor = ArmorIntegrity.Create armor }
