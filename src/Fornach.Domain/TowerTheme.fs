namespace Fornach.Domain

open System
open Fornach.Spatial

/// Floor biomes recycling and expanding upon the 5 Grief environments and beyond
type FloorTheme =
  | QuarryPlazas
  | PineCloisters
  | BasaltCalderas
  | TempestTerraces
  | SunkenBoulevards
  | ElysianSanctuaries
  | CelestialSpires

  member this.Name : string =
    match this with
    | QuarryPlazas -> "The Obsidian Quarry Plazas"
    | PineCloisters -> "The Shrouded Pine Cloisters"
    | BasaltCalderas -> "The Basalt Caldera Causeways"
    | TempestTerraces -> "The Tempest Promontory Terraces"
    | SunkenBoulevards -> "The Sunken Metropolis Boulevards"
    | ElysianSanctuaries -> "The Elysian Meadow Sanctuaries"
    | CelestialSpires -> "The Celestial Spire Bridges"

  member this.Description : string =
    match this with
    | QuarryPlazas -> "Wide stone pavilions, steam-vent walkways, and iron gantry plazas in the dark rain."
    | PineCloisters -> "Expansive moonlit glades flanked by colossal ancient pines, mossy arches, and shifting mist."
    | BasaltCalderas -> "Broad obsidian causeways suspended over rivers of magma, glowing with crackling heat."
    | TempestTerraces -> "Sweeping stone terraces buffeted by gale-force spray above a roaring, boundless ocean."
    | SunkenBoulevards -> "Grand flooded marble avenues and drowned colonnades reflecting silent, calm floodwaters."
    | ElysianSanctuaries -> "Endless white lily sanctuaries bathed in gentle golden sunlight and fragrant breezes."
    | CelestialSpires -> "Starlit glass causeways piercing through the void, suspended beneath cosmic constellations."

  member this.ColorHex : string =
    match this with
    | QuarryPlazas -> "#6272a4" // Slate / Comment
    | PineCloisters -> "#50fa7b" // Green
    | BasaltCalderas -> "#ff5555" // Red
    | TempestTerraces -> "#8be9fd" // Cyan
    | SunkenBoulevards -> "#bd93f9" // Purple
    | ElysianSanctuaries -> "#f1fa8c" // Yellow / Gold
    | CelestialSpires -> "#ff79c6" // Pink / Cosmic

  member this.FloorGlyph : char =
    match this with
    | QuarryPlazas -> '.'
    | PineCloisters -> '"'
    | BasaltCalderas -> ','
    | TempestTerraces -> '~'
    | SunkenBoulevards -> '·'
    | ElysianSanctuaries -> '❀'
    | CelestialSpires -> '✧'

  member this.PillarGlyph : char =
    match this with
    | QuarryPlazas -> '∏'
    | PineCloisters -> '♠'
    | BasaltCalderas -> '▲'
    | TempestTerraces -> '☗'
    | SunkenBoulevards -> '∩'
    | ElysianSanctuaries -> '⛩'
    | CelestialSpires -> '✦'

  member this.ChasmGlyph : char =
    match this with
    | BasaltCalderas -> '≈' // Magma lake
    | TempestTerraces -> '≋' // Sea abyss
    | SunkenBoulevards -> '░' // Deep murky flood
    | _ -> ' ' // Open void

/// Surface terrain texture for floor tiles
type SurfaceType =
  | PavedStone
  | ForestMoss
  | BasaltRock
  | ShallowWater
  | LilyPetals
  | StarlitGlass

/// Environmental hazard types
type HazardType =
  | LavaRift
  | AcidSlag
  | DeepCurrent
  | CalmingSpores

/// The state of the Ascension Door leading to the next floor
type DoorState =
  | Open
  | LockedByKey of keyId: string * keyName: string * hint: string
  | LockedByQuest of questId: string * questTitle: string * requirement: string
