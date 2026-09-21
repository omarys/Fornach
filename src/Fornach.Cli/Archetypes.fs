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
  // Adept Tier (Stats 100–150, HP/Morale 1400–2600)
  // =========================================================================

  let createTheron () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 135; Fortitude, 125
        Finesse, 70; Reflex, 80
        Prowess, 115; Poise, 110
        Intellect, 40; Resolve, 50
        Acuity, 40; Intuition, 45
        Acumen, 40; Composure, 50
      ]
    { Combatant.create id "Theron (Iron Vanguard)" 2600 1400 stats with
        Armor = ArmorIntegrity.Create 70
        Stance = CombatStance.PowerStance }

  let createSilverFencer () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 60; Fortitude, 70
        Finesse, 140; Reflex, 130
        Prowess, 95; Poise, 85
        Intellect, 45; Resolve, 50
        Acuity, 50; Intuition, 55
        Acumen, 40; Composure, 50
      ]
    { Combatant.create id "Lyra (Silver Fencer)" 1800 1600 stats with
        Armor = ArmorIntegrity.Create 45
        Stance = CombatStance.AgilityStance }

  let createAurelius () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 40; Fortitude, 45
        Finesse, 45; Reflex, 50
        Prowess, 40; Poise, 45
        Intellect, 140; Resolve, 110
        Acuity, 125; Intuition, 95
        Acumen, 105; Composure, 100
      ]
    { Combatant.create id "Aurelius (Thought-Weaver)" 1400 2600 stats with
        Armor = ArmorIntegrity.Create 30
        Stance = CombatStance.AgilityStance }

  let createLadyVane () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 45; Fortitude, 50
        Finesse, 50; Reflex, 50
        Prowess, 45; Poise, 55
        Intellect, 135; Resolve, 115
        Acuity, 90; Intuition, 95
        Acumen, 125; Composure, 110
      ]
    { Combatant.create id "Lady Vane (High Magistrate)" 1600 2400 stats with
        Armor = ArmorIntegrity.Create 40
        Stance = CombatStance.DisciplineStance }

  let createMirageWeaver () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 40; Fortitude, 45
        Finesse, 50; Reflex, 60
        Prowess, 45; Poise, 50
        Intellect, 85; Resolve, 90
        Acuity, 145; Intuition, 120
        Acumen, 85; Composure, 90
      ]
    { Combatant.create id "Seraphina (Mirage Weaver)" 1500 2500 stats with
        Armor = ArmorIntegrity.Create 35
        Stance = CombatStance.AgilityStance }

  let createRunicAbjurer () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 45; Fortitude, 55
        Finesse, 40; Reflex, 45
        Prowess, 50; Poise, 60
        Intellect, 90; Resolve, 105
        Acuity, 80; Intuition, 90
        Acumen, 145; Composure, 125
      ]
    { Combatant.create id "Kaelen (Runic Abjurer)" 1700 2300 stats with
        Armor = ArmorIntegrity.Create 45
        Stance = CombatStance.DisciplineStance }

  // =========================================================================
  // Master Tier (Stats 300–400, HP/Morale 4000–8000)
  // =========================================================================

  let createWarmaster () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 350; Fortitude, 320
        Finesse, 180; Reflex, 200
        Prowess, 300; Poise, 280
        Intellect, 90; Resolve, 120
        Acuity, 90; Intuition, 110
        Acumen, 90; Composure, 130
      ]
    { Combatant.create id "Valerius (Grand Warmaster)" 7500 4500 stats with
        Armor = ArmorIntegrity.Create 120 }

  let createArchDiviner () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 90; Fortitude, 100
        Finesse, 110; Reflex, 120
        Prowess, 90; Poise, 110
        Intellect, 380; Resolve, 310
        Acuity, 340; Intuition, 270
        Acumen, 300; Composure, 280
      ]
    { Combatant.create id "Ignis (Arch-Diviner)" 4200 8000 stats with
        Armor = ArmorIntegrity.Create 50 }

  let createChancellor () =
    let id = CombatantId.New()
    let stats =
      StatBlock.Create [
        Force, 95; Fortitude, 110
        Finesse, 110; Reflex, 115
        Prowess, 95; Poise, 120
        Intellect, 360; Resolve, 320
        Acuity, 280; Intuition, 270
        Acumen, 340; Composure, 300
      ]
    { Combatant.create id "Chancellor Malakor" 4800 7400 stats with
        Armor = ArmorIntegrity.Create 65 }

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
      Tier = Adept
      Discipline = CombatMode.Physical
      Description = "Seasoned martial warrior commanding heavy Force and stance pressure."
      Factory = createTheron }

    { Name = "Silver Fencer"
      Tier = Adept
      Discipline = CombatMode.Physical
      Description = "Agile fencer executing probing Finesse strikes, seeking critical vital openings."
      Factory = createSilverFencer }

    { Name = "Thought-Weaver"
      Tier = Adept
      Discipline = CombatMode.Arcane
      Description = "Psionic mystic shredding mental defenses via raw Intellect and Acuity."
      Factory = createAurelius }

    { Name = "Mirage Weaver"
      Tier = Adept
      Discipline = CombatMode.Arcane
      Description = "Guile illusionist conjuring decoy mirror swarms and disorienting glamours."
      Factory = createMirageWeaver }

    { Name = "Runic Abjurer"
      Tier = Adept
      Discipline = CombatMode.Arcane
      Description = "Discipline abjurer commanding defensive wards and posture-shattering shockwaves."
      Factory = createRunicAbjurer }

    { Name = "High Magistrate"
      Tier = Adept
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
