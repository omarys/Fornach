namespace Fornach.Cli

open System
open System.Text
open System.Threading
open Spectre.Console
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine
open Fornach.Story

module TowerDisplay =

  /// Outcome of a multi-enemy (group) engagement
  [<RequireQualifiedAccess>]
  type GroupDuelOutcome =
    | Victorious
    | Defeated
    | Fled

  /// Result of a multi-enemy (group) engagement: the resolved outcome plus the updated
  /// player and enemy records so the caller can write them back into the run state
  type GroupDuelResult =
    { Outcome: GroupDuelOutcome
      Player: Combatant
      Enemies: TowerEnemy list }

  let private themeColor (theme: FloorTheme) : Color =
    match theme with
    | QuarryPlazas -> Theme.ColorComment
    | PineCloisters -> Theme.ColorGreen
    | BasaltCalderas -> Theme.ColorRed
    | TempestTerraces -> Theme.ColorCyan
    | SunkenBoulevards -> Theme.ColorPurple
    | ElysianSanctuaries -> Theme.ColorYellow
    | CelestialSpires -> Theme.ColorPink

  let private formatTileVisible (tile: TowerTile) (theme: FloorTheme) : string =
    match tile with
    | StairwayPortal door ->
      match door with
      | Open -> sprintf "[bold %s]▲[/]" Theme.Green
      | LockedByKey _ -> sprintf "[bold %s]⮝[/]" Theme.Orange
      | LockedByQuest _ -> sprintf "[bold %s]⮝[/]" Theme.Purple
    | Archway -> sprintf "[bold %s]∩[/]" Theme.Foreground
    | Hazard hazard ->
      match hazard with
      | LavaRift -> sprintf "[bold %s]≈[/]" Theme.Red
      | AcidSlag -> sprintf "[bold %s]≈[/]" Theme.Green
      | DeepCurrent -> sprintf "[bold %s]≋[/]" Theme.Cyan
      | CalmingSpores -> sprintf "[bold %s]❀[/]" Theme.Pink
    | Pillar -> sprintf "[bold %s]%c[/]" theme.ColorHex theme.PillarGlyph
    | Floor _ -> sprintf "[%s]%c[/]" theme.ColorHex theme.FloorGlyph
    | Chasm ->
      let ch = theme.ChasmGlyph
      if ch = ' ' then " " else sprintf "[%s]%c[/]" Theme.CurrentLine ch

  let private formatTileExplored (tile: TowerTile) (theme: FloorTheme) : string =
    match tile with
    | StairwayPortal _ -> sprintf "[%s]▲[/]" Theme.Comment
    | Archway -> sprintf "[%s]∩[/]" Theme.Comment
    | Hazard _ -> sprintf "[%s]~[/]" Theme.Comment
    | Pillar -> sprintf "[%s]%c[/]" Theme.Comment theme.PillarGlyph
    | Floor _ -> sprintf "[%s]%c[/]" Theme.CurrentLine theme.FloorGlyph
    | Chasm -> " "

  /// Structured inspection outcome for examining tiles and hazards within field of view
  type InspectionDetail = {
    Coordinate: Point
    Distance: int
    VisibilityText: string
    TileGlyph: string
    TileName: string
    TileDescription: string
    HazardWarning: string option
    EntitySummary: string option
  }

  /// Inspects a specific coordinate on the current floor, detailing visibility, terrain, hazards, and occupants
  let inspectTile (state: TowerRunState) (pt: Point) : InspectionDetail =
    let dist = Math.Max(Math.Abs(pt.X - state.PlayerPosition.X), Math.Abs(pt.Y - state.PlayerPosition.Y))
    let isVisible = Set.contains pt state.CurrentFloor.Visible
    let isExplored = Set.contains pt state.CurrentFloor.Explored

    let visibilityText =
      if isVisible then sprintf "[bold %s]󰄬 Within Active Vision[/]" Theme.Green
      elif isExplored then sprintf "[italic %s]󰒓 Explored Memory (Out of Direct Sight)[/]" Theme.Comment
      else sprintf "[%s]󰋑 Shrouded in Darkness[/]" Theme.Comment

    let tileOpt = Map.tryFind pt state.CurrentFloor.Tiles
    let entityOpt = Map.tryFind pt state.CurrentFloor.Entities

    let tileGlyph, tileName, tileDesc, hazardWarning =
      if not isExplored && not isVisible then
        "?", "Unexplored Darkness", "This chamber quadrant has not been surveyed yet.", None
      else
        match tileOpt with
        | Some (Hazard hazard) ->
          match hazard with
          | AcidSlag ->
            sprintf "[bold %s]≈[/]" Theme.Green,
            "Corrosive Acid Slag",
            "Pools of toxic acidic runoff seep from ancient mining veins.",
            Some (sprintf "[bold %s]CAUTION:[/] Corrosive acid dissolves [bold %s]-15 Armor durability[/] upon stepping here!" Theme.Red Theme.Yellow)
          | LavaRift ->
            sprintf "[bold %s]≈[/]" Theme.Red,
            "Molten Lava Rift",
            "Superheated magma bubbling through fractured basalt bedrock.",
            Some (sprintf "[bold %s]DANGER:[/] Intense heat scorches player for [bold %s]-15 direct Health[/]!" Theme.Red Theme.Red)
          | DeepCurrent ->
            sprintf "[bold %s]≋[/]" Theme.Cyan,
            "Deep Floodwater Current",
            "Swift, murky water with treacherous underwater undertows.",
            Some (sprintf "[bold %s]HAZARD:[/] Heavy current exerts drag, inflicting [bold %s]+15 Exhaustion[/]!" Theme.Yellow Theme.Purple)
          | CalmingSpores ->
            sprintf "[bold %s]❀[/]" Theme.Pink,
            "Calming Spore Blossom",
            "A serene patch of fragrant, luminous psychotropic blossoms.",
            Some (sprintf "[bold %s]BENEFICIAL:[/] Inhaling these peaceful spores [bold %s]resets Recklessness to 0[/]!" Theme.Green Theme.Cyan)
        | Some (Floor surface) ->
          let glyph = string state.CurrentFloor.Theme.FloorGlyph
          match surface with
          | PavedStone -> glyph, "Paved Granite Stone", "Firm architectural flagstones forming expansive pavilions (Walkable).", None
          | ForestMoss -> glyph, "Verdant Forest Moss", "Soft, damp carpet of ancient pine moss (Walkable).", None
          | BasaltRock -> glyph, "Basalt Rock Flagstone", "Dark volcanic stone causeway, smooth and heated (Walkable).", None
          | ShallowWater -> glyph, "Shallow Floodwater", "Calm ankle-deep water reflecting the cavern ceiling (Walkable).", None
          | LilyPetals -> glyph, "Lily Petal Lawn", "Dense fragrant white lily blossoms covering the terrace (Walkable).", None
          | StarlitGlass -> glyph, "Starlit Glass Causeway", "Smooth, translucent crystalline surface piercing the void (Walkable).", None
        | Some Archway ->
          "∩", "Colonnade Archway", "Open monumental architectural portal connecting courtyards (Walkable).", None
        | Some Pillar ->
          string state.CurrentFloor.Theme.PillarGlyph, "Architectural Monolith / Pillar", "Colossal load-bearing stone pillar (Impassable; blocks movement and line of sight).", None
        | Some Chasm ->
          let ch = string state.CurrentFloor.Theme.ChasmGlyph
          (if ch = " " then " " else ch), "Abyssal Chasm / Void", "Endless drop into the depths (Impassable; allows vision and ranged line of sight).", None
        | Some (StairwayPortal door) ->
          match door with
          | Open ->
            "▲", "Ascension Stairway",
            sprintf "Spiraling stone staircase leading upward to Floor %d (Step here to ascend)." (state.CurrentFloor.FloorNumber + 1),
            None
          | LockedByKey(_, keyName, hint) ->
            "⮝", "Sealed Ascension Door",
            sprintf "Heavy iron vault portal sealed tight. Requires: [[%s]]. Hint: %s" keyName hint,
            Some (sprintf "[bold %s]LOCKED:[/] Collect key to unlock." Theme.Orange)
          | LockedByQuest(_, questTitle, req) ->
            "⮝", "Barred Ascension Portal",
            sprintf "Ascension barred by architectural trial: [[%s]]. Requirement: %s" questTitle req,
            Some (sprintf "[bold %s]TRIAL ACTIVE:[/] Complete trial to pass." Theme.Purple)
        | None ->
          " ", "Chamber Void", "Unbounded space outside chamber walls.", None

    let entitySummary =
      if not isVisible then
        if isExplored then
          match entityOpt with
          | Some (EntityChest c) ->
            if c.IsOpen then
              Some (sprintf "[%s]⌹ Opened Vault Chest[/] [grey](Empty — remembered location)[/]" Theme.Comment)
            else
              Some (sprintf "[bold %s]⌹ VAULT CHEST[/] [grey](Unopened — remembered location)[/]" Theme.Yellow)
          | Some (EntityShrine s) ->
            if s.IsUsed then
              Some (sprintf "[%s]† Depleted Shrine: %s[/] [grey](Power exhausted — remembered location)[/]" Theme.Comment s.Name)
            else
              Some (sprintf "[bold %s]† RUNIC SHRINE: %s[/] [grey](%s — remembered location)[/]" Theme.Green s.Name s.BlessingDescription)
          | Some (EntityEncounter enc) ->
            match enc with
            | SacrificialAltar a ->
              if a.IsUsed then Some (sprintf "[%s]♨ Depleted Altar: %s[/] [grey](Remembered location)[/]" Theme.Comment a.Name)
              else Some (sprintf "[bold %s]♨ SACRIFICIAL ALTAR: %s[/] [grey](Remembered location)[/]" Theme.Purple a.Name)
            | WanderingTrader m ->
              Some (sprintf "[bold %s]󱁠 SPECTRAL MERCHANT: %s[/] [grey](Remembered location)[/]" Theme.Cyan m.Name)
            | TreasureVault v ->
              if v.IsOpen then Some (sprintf "[%s]⌹ Plundered Vault: %s[/] [grey](Remembered location)[/]" Theme.Comment v.Name)
              else Some (sprintf "[bold %s]⌹ SEALED VAULT: %s[/] [grey](Remembered location)[/]" Theme.Yellow v.Name)
            | MechanicalTrapGauntlet t ->
              if t.IsDisarmed || t.IsTriggered then Some (sprintf "[%s]✕ Neutralized Trap: %s[/] [grey](Remembered location)[/]" Theme.Comment t.Name)
              else Some (sprintf "[bold %s]✕ MECHANICAL TRAP: %s[/] [grey](Remembered location)[/]" Theme.Red t.Name)
            | MemoryEchoFragment e ->
              if e.IsCommuned then Some (sprintf "[%s]✧ Communed Memory: %s[/] [grey](Remembered location)[/]" Theme.Comment e.Title)
              else Some (sprintf "[bold %s]✧ DORMANT MEMORY ECHO: %s[/] [grey](Remembered location)[/]" Theme.Green e.Title)
            | AmbushLair a ->
              if a.IsTriggered then Some (sprintf "[%s]! Cleared Ambush Site[/] [grey](Remembered location)[/]" Theme.Comment)
              else Some (sprintf "[bold %s]! SUSPICIOUS COLONNADE[/] [grey](Remembered location)[/]" Theme.Red)
          | _ -> None
        else None
      elif pt = state.PlayerPosition then
        let p: Combatant = state.Player
        Some (sprintf "[bold %s]@ YOU[/] [grey]| %s (Lv.%d %s) | HP: %d/%d | Morale: %d/%d[/]"
          Theme.Yellow p.Name p.Level p.Class.Name p.Health.Current p.Health.Maximum p.Morale.Current p.Morale.Maximum)
      else
        match entityOpt with
        | Some (EntityEnemy e) ->
          if e.IsDefeated then
            Some (sprintf "[%s]%% Defeated Guardian: %s[/] [grey](Neutralized)[/]" Theme.Comment e.Name)
          else
            let c: Combatant = e.Combatant
            let stanceOrForm =
              if c.Plane = Mental then
                match c.ComplexForm with
                | Some f -> f.Name
                | None -> "Unthreaded"
              else
                sprintf "%A" c.Stance
            let familyPrefix =
              match c.MonsterFamily with
              | Some f -> sprintf "[bold %s][[%s]][/] " Theme.Purple f.Name
              | None -> ""
            let traitsSuffix =
              if c.MonsterTraits.IsEmpty then ""
              else
                let traitNames = c.MonsterTraits |> List.map (fun t -> t.Name) |> String.concat ", "
                sprintf " | Traits: [%s]%s[/]" Theme.Yellow traitNames
            let assess = TacticalAssessment.assess state.Player c
            let vulnSnippet = match assess.VulnerabilitySummary with Some v -> sprintf " | Opening: %s" (Markup.Escape v) | None -> ""
            let readSnippet = sprintf "\n  󰓥 [bold %s]Tactical Read (%s):[/] %s%s\n  [italic %s]💡 %s[/]"
                                Theme.Yellow (Markup.Escape assess.Headline) (Markup.Escape assess.PrimarySummary) vulnSnippet Theme.Comment (Markup.Escape assess.StrategicAdvice)
            Some (sprintf "[bold %s]! HOSTILE GUARDIAN: %s%s[/] [grey]| Lv.%d %s | HP: %d/%d | Morale: %d/%d | %s: %s%s[/]%s"
              Theme.Red familyPrefix e.Name c.Level c.Class.Name c.Health.Current c.Health.Maximum c.Morale.Current c.Morale.Maximum (if c.Plane = Mental then "Form" else "Stance") stanceOrForm traitsSuffix readSnippet)
        | Some (EntityNpc n) ->
          let trialText = match n.Quest with Some q -> sprintf " [bold %s](Trial Offered: %s)[/]" Theme.Yellow q.Title | None -> ""
          Some (sprintf "[bold %s]? INHABITANT: %s[/] [italic %s](%s)[/]%s" Theme.Cyan n.Name Theme.Comment n.Role trialText)
        | Some (EntityChest c) ->
          if c.IsOpen then
            Some (sprintf "[%s]⌹ Opened Vault Chest[/] [grey](Empty)[/]" Theme.Comment)
          else
            Some (sprintf "[bold %s]⌹ VAULT CHEST[/] [grey](Unopened — walk into tile to open)[/]" Theme.Yellow)
        | Some (EntityShrine s) ->
          if s.IsUsed then
            Some (sprintf "[%s]† Depleted Shrine: %s[/] [grey](Power exhausted)[/]" Theme.Comment s.Name)
          else
            Some (sprintf "[bold %s]† RUNIC SHRINE: %s[/] [grey](%s — walk into tile to commune)[/]" Theme.Green s.Name s.BlessingDescription)
        | Some (EntityEncounter enc) ->
          match enc with
          | SacrificialAltar a ->
            if a.IsUsed then
              Some (sprintf "[%s]♨ Spent Sacrificial Altar: %s[/] [grey](Pact sealed — power exhausted)[/]" Theme.Comment a.Name)
            else
              Some (sprintf "[bold %s]♨ SACRIFICIAL ALTAR: %s[/] [grey]| Cost: [bold %s]%s[/] -> Reward: [bold %s]%s[/] (Walk into tile to commune)[/]"
                Theme.Purple a.Name Theme.Red a.Cost.Description Theme.Green a.Reward.Description)
          | WanderingTrader m ->
            Some (sprintf "[bold %s]󱁠 SPECTRAL MERCHANT: %s (%s)[/] [grey]| %d wares available (Walk into tile to trade)[/]"
              Theme.Cyan m.Name m.Title m.Wares.Length)
          | TreasureVault v ->
            if v.IsOpen then
              Some (sprintf "[%s]⌹ Plundered Vault: %s[/] [grey](Emptied)[/]" Theme.Comment v.Name)
            else
              let puzzleText =
                match v.Puzzle with
                | KeyholeLock (_, name, _) -> sprintf "Key Required: %s" name
                | StatCheck (stat, req, _) -> sprintf "Test: %d %A" req stat
                | MemoryCipher (riddle, _) -> sprintf "Cipher: %s" riddle
              Some (sprintf "[bold %s]⌹ SEALED TREASURE VAULT: %s[/] [grey]| %s | %d Souls + %d Relic(s)[/]"
                Theme.Yellow v.Name puzzleText v.BonusSouls v.Relics.Length)
          | MechanicalTrapGauntlet t ->
            if t.IsDisarmed then
              Some (sprintf "[%s]✕ Disarmed Trap: %s[/] [grey](Mechanism neutralized)[/]" Theme.Comment t.Name)
            elif t.IsTriggered then
              Some (sprintf "[%s]✕ Sprung Trap: %s[/] [grey](Mechanism discharged)[/]" Theme.Comment t.Name)
            else
              Some (sprintf "[bold %s]✕ CONCEALED MECHANICAL TRAP: %s[/] [grey]| Disarm Check: %d %A | Step cautiously![/]"
                Theme.Red t.Name t.DisarmThreshold t.DisarmStat)
          | MemoryEchoFragment e ->
            if e.IsCommuned then
              Some (sprintf "[%s]✧ Awakened Memory: %s[/] [grey](Communed)[/]" Theme.Comment e.Title)
            else
              Some (sprintf "[bold %s]✧ DORMANT MEMORY ECHO: %s[/] [grey]| \"%s\" (+%d Morale upon contact)[/]"
                Theme.Green e.Title e.SensoryDetail e.MoraleRecovery)
          | AmbushLair a ->
            if a.IsTriggered then
              Some (sprintf "[%s]! Cleared Ambush Site[/] [grey](All foes vanquished or dispersed)[/]" Theme.Comment)
            else
              Some (sprintf "[bold %s]󰈸 LURKING AMBUSH LAIR: %s[/] [grey]| Threat: Lv.%d %s | Status: Ready to spring![/]"
                Theme.Red a.Name a.Pack.Leader.Level a.Pack.Leader.Name)
        | None -> None

    { Coordinate = pt
      Distance = dist
      VisibilityText = visibilityText
      TileGlyph = tileGlyph
      TileName = tileName
      TileDescription = tileDesc
      HazardWarning = hazardWarning
      EntitySummary = entitySummary }

  /// Renders detailed tactical analysis and hazard warnings for the tile under the inspection reticle
  let renderInspectionPanel (state: TowerRunState) (cursorPt: Point) : Panel =
    let detail = inspectTile state cursorPt
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore

    // Row 1: Coordinates, Distance, Visibility status
    let statusLine =
      sprintf "[bold %s]󰍹 INSPECTION RETICLE[/] [grey]| Position: (%d, %d) | Distance: %d step%s |[/] %s"
        Theme.Yellow detail.Coordinate.X detail.Coordinate.Y detail.Distance (if detail.Distance = 1 then "" else "s") detail.VisibilityText
    grid.AddRow(Markup(statusLine)) |> ignore

    // Row 2: Tile type & description
    let tileLine = sprintf "[bold %s]Terrain:[/] %s [bold %s]%s[/] ── [italic %s]%s[/]" Theme.Cyan detail.TileGlyph Theme.Foreground detail.TileName Theme.Comment detail.TileDescription
    grid.AddRow(Markup(tileLine)) |> ignore

    // Row 3: Hazard warning if any
    match detail.HazardWarning with
    | Some warning ->
      grid.AddRow(Markup(sprintf "[bold %s]⚠ Environmental Warning:[/] %s" Theme.Red warning)) |> ignore
    | None -> ()

    // Row 4: Entity summary if any
    match detail.EntitySummary with
    | Some ent ->
      grid.AddRow(Markup(sprintf "[bold %s]󰒋 Occupant:[/] %s" Theme.Yellow ent)) |> ignore
    | None -> ()

    // Row 5: Hint bar
    grid.AddRow(Markup(sprintf "[italic %s]󰌌 Pan Reticle: Arrows / Vim (h/j/k/l/y/u/b/n)  •  Exit Inspect: Esc / x / Enter  •  ? / F1: Legend & Manual[/]" Theme.Comment)) |> ignore

    Panel(grid)
      .Header(sprintf "[bold %s]󰍹 TILE & HAZARD INSPECTOR[/]" Theme.Yellow)
      .Border(BoxBorder.Square)
      .BorderStyle(Theme.StyleYellow)
      .Expand()

  /// Horizontal split for the crawl view: the map viewport takes its natural width (capped by
  /// the floor) and the HUD takes the remainder. Both are returned so the caller can pin the
  /// grid columns — otherwise the HUD's longest row sizes its column and squeezes the map.
  let layoutWidths (termW: int) (mapWidth: int) : int * int =
    let maxView = Math.Max(45, mapWidth)
    let viewW = Math.Clamp(termW - 62, 45, maxView)
    let hudW = Math.Max(58, termW - viewW - 4)
    viewW, hudW

  /// Renders the ASCII map viewport centered on the player position (or inspection cursor if active)
  let renderViewportWithCursor (state: TowerRunState) (cursor: Point option) (viewW: int) (viewH: int) : Panel =
    let theme = state.CurrentFloor.Theme
    let halfW = viewW / 2
    let halfH = viewH / 2

    // Center viewport on inspection cursor if active, otherwise on player
    let centerPt = cursor |> Option.defaultValue state.PlayerPosition
    let startX = Math.Clamp(centerPt.X - halfW, 0, Math.Max(0, state.CurrentFloor.Width - viewW))
    let startY = Math.Clamp(centerPt.Y - halfH, 0, Math.Max(0, state.CurrentFloor.Height - viewH))
    let endX = Math.Min(state.CurrentFloor.Width - 1, startX + viewW - 1)
    let endY = Math.Min(state.CurrentFloor.Height - 1, startY + viewH - 1)

    let sb = StringBuilder()

    for y in startY .. endY do
      for x in startX .. endX do
        let pt = { X = x; Y = y }
        let isCursor = cursor = Some pt

        if isCursor then
          // Render highlighted inspection reticle over the cell glyph
          let cellGlyph =
            if pt = state.PlayerPosition then "@"
            elif Set.contains pt state.CurrentFloor.Visible then
              match Map.tryFind pt state.CurrentFloor.Entities with
              | Some (EntityEnemy e) -> if e.IsDefeated then "%" else "!"
              | Some (EntityNpc _) -> "?"
              | Some (EntityChest _) -> "⌹"
              | Some (EntityShrine _) -> "†"
              | Some (EntityEncounter enc) ->
                match enc with
                | SacrificialAltar _ -> "♨"
                | WanderingTrader _ -> "$"
                | TreasureVault _ -> "⌹"
                | MechanicalTrapGauntlet _ -> "✕"
                | MemoryEchoFragment _ -> "✦"
                | AmbushLair _ -> "!"
              | None ->
                match Map.tryFind pt state.CurrentFloor.Tiles with
                | Some (Hazard AcidSlag) -> "≈"
                | Some (Hazard LavaRift) -> "≈"
                | Some (Hazard DeepCurrent) -> "≋"
                | Some (Hazard CalmingSpores) -> "❀"
                | Some (Floor _) -> string theme.FloorGlyph
                | Some Archway -> "∩"
                | Some Pillar -> string theme.PillarGlyph
                | Some (StairwayPortal _) -> "▲"
                | Some Chasm -> string theme.ChasmGlyph
                | None -> " "
            elif Set.contains pt state.CurrentFloor.Explored then
              match Map.tryFind pt state.CurrentFloor.Entities with
              | Some (EntityChest c) when not c.IsOpen -> "⌹"
              | Some (EntityShrine s) when not s.IsUsed -> "†"
              | Some (EntityEncounter enc) ->
                match enc with
                | SacrificialAltar a when not a.IsUsed -> "♨"
                | WanderingTrader _ -> "$"
                | TreasureVault v when not v.IsOpen -> "⌹"
                | MemoryEchoFragment e when not e.IsCommuned -> "✦"
                | _ -> " "
              | _ ->
                match Map.tryFind pt state.CurrentFloor.Tiles with
                | Some (Floor _) -> string theme.FloorGlyph
                | Some Pillar -> string theme.PillarGlyph
                | Some Archway -> "∩"
                | Some (StairwayPortal _) -> "▲"
                | Some (Hazard _) -> "~"
                | _ -> " "
            else " "
          sb.Append(sprintf "[bold black on #f1fa8c]%s[/]" (if cellGlyph = " " then "•" else cellGlyph)) |> ignore
        elif pt = state.PlayerPosition then
          sb.Append(sprintf "[bold %s on %s]@[/]" Theme.Yellow Theme.Red) |> ignore
        elif Set.contains pt state.CurrentFloor.Visible then
          // In active FOV
          match Map.tryFind pt state.CurrentFloor.Entities with
          | Some (EntityEnemy e) ->
            if e.IsDefeated then
              sb.Append(sprintf "[%s]%%[/]" Theme.Comment) |> ignore
            else
              sb.Append(sprintf "[bold %s]![/]" Theme.Red) |> ignore
          | Some (EntityNpc _) ->
            sb.Append(sprintf "[bold %s]?[/]" Theme.Cyan) |> ignore
          | Some (EntityChest c) ->
            if c.IsOpen then
              sb.Append(sprintf "[%s]⌹[/]" Theme.Comment) |> ignore
            else
              sb.Append(sprintf "[bold %s]⌹[/]" Theme.Yellow) |> ignore
          | Some (EntityShrine s) ->
            if s.IsUsed then
              sb.Append(sprintf "[%s]†[/]" Theme.Comment) |> ignore
            else
              sb.Append(sprintf "[bold %s]†[/]" Theme.Green) |> ignore
          | Some (EntityEncounter enc) ->
            match enc with
            | SacrificialAltar a ->
              if a.IsUsed then sb.Append(sprintf "[%s]♨[/]" Theme.Comment) |> ignore
              else sb.Append(sprintf "[bold %s]♨[/]" Theme.Purple) |> ignore
            | WanderingTrader _ ->
              sb.Append(sprintf "[bold %s]$[/]" Theme.Cyan) |> ignore
            | TreasureVault v ->
              if v.IsOpen then sb.Append(sprintf "[%s]⌹[/]" Theme.Comment) |> ignore
              else sb.Append(sprintf "[bold %s]⌹[/]" Theme.Yellow) |> ignore
            | MechanicalTrapGauntlet t ->
              if t.IsDisarmed || t.IsTriggered then sb.Append(sprintf "[%s]✕[/]" Theme.Comment) |> ignore
              else sb.Append(sprintf "[bold %s]✕[/]" Theme.Red) |> ignore
            | MemoryEchoFragment e ->
              if e.IsCommuned then sb.Append(sprintf "[%s]✧[/]" Theme.Comment) |> ignore
              else sb.Append(sprintf "[bold %s]✦[/]" Theme.Green) |> ignore
            | AmbushLair a ->
              if a.IsTriggered then sb.Append(sprintf "[%s]%%[/]" Theme.Comment) |> ignore
              else sb.Append(sprintf "[bold %s]![/]" Theme.Red) |> ignore
          | None ->
            match Map.tryFind pt state.CurrentFloor.Tiles with
            | Some tile -> sb.Append(formatTileVisible tile theme) |> ignore
            | None -> sb.Append(' ') |> ignore
        elif Set.contains pt state.CurrentFloor.Explored then
          // Explored in memory
          match Map.tryFind pt state.CurrentFloor.Entities with
          | Some (EntityChest c) when not c.IsOpen ->
            sb.Append(sprintf "[%s]⌹[/]" Theme.Comment) |> ignore
          | Some (EntityShrine s) when not s.IsUsed ->
            sb.Append(sprintf "[%s]†[/]" Theme.Comment) |> ignore
          | Some (EntityEncounter enc) ->
            match enc with
            | SacrificialAltar a when not a.IsUsed -> sb.Append(sprintf "[%s]♨[/]" Theme.Comment) |> ignore
            | WanderingTrader _ -> sb.Append(sprintf "[%s]$[/]" Theme.Comment) |> ignore
            | TreasureVault v when not v.IsOpen -> sb.Append(sprintf "[%s]⌹[/]" Theme.Comment) |> ignore
            | MemoryEchoFragment e when not e.IsCommuned -> sb.Append(sprintf "[%s]✦[/]" Theme.Comment) |> ignore
            | _ ->
              match Map.tryFind pt state.CurrentFloor.Tiles with
              | Some tile -> sb.Append(formatTileExplored tile theme) |> ignore
              | None -> sb.Append(' ') |> ignore
          | _ ->
            match Map.tryFind pt state.CurrentFloor.Tiles with
            | Some tile -> sb.Append(formatTileExplored tile theme) |> ignore
            | None -> sb.Append(' ') |> ignore
        else
          sb.Append(' ') |> ignore

      if y < endY then
        sb.AppendLine() |> ignore

    let headerText =
      match cursor with
      | Some cPt ->
        sprintf "[bold %s]󰍹 INSPECT MODE[/] [grey]| Reticle: (%d, %d) | Player: (%d, %d)[/]"
          Theme.Yellow cPt.X cPt.Y state.PlayerPosition.X state.PlayerPosition.Y
      | None ->
        sprintf "[bold %s]󰒋 %s[/] [grey]| 󰍹 Pos: (%d, %d)[/]"
          theme.ColorHex
          theme.Name
          state.PlayerPosition.X
          state.PlayerPosition.Y

    Panel(Markup(sb.ToString()))
      .Header(headerText)
      .Border(BoxBorder.Heavy)
      .BorderStyle(Style(foreground = Nullable (if cursor.IsSome then Theme.ColorYellow else themeColor theme)))
      .Expand()

  /// Backward-compatible viewport renderer centered on player position
  let renderViewport (state: TowerRunState) (viewW: int) (viewH: int) : Panel =
    renderViewportWithCursor state None viewW viewH

  /// Renders the side HUD panel detailing player vitals, ascension status, and inventory.
  /// `debug` adds the [DEBUG] marker used by the in-game F12 overlay.
  let renderHudWithDebug (state: TowerRunState) (debug: bool) : Panel =
    let grid = Grid()
    grid.AddColumn(GridColumn().NoWrap()) |> ignore

    let p = state.Player

    // 0. Run identity (debug only): the seed is reproducible via --seed N,
    // so the debug HUD surfaces it alongside the [DEBUG] marker.
    if debug then
      grid.AddRow(Markup(sprintf "[bold %s]Seed:[/] [bold white]%d[/]  [bold red]󰒲 [[DEBUG]][/]" Theme.Comment state.Seed)) |> ignore

    // 1. Vitals
    grid.AddRow(Markup(sprintf "[bold %s]─── 󰓥 TACTICAL VITALS ───[/]" Theme.Yellow)) |> ignore
    grid.AddRow(Markup(Display.renderBar "󰋑 Health" p.Health.Current p.Health.Maximum Theme.Red)) |> ignore
    grid.AddRow(Markup(Display.renderBar "󰧑 Morale" p.Morale.Current p.Morale.Maximum Theme.Cyan)) |> ignore
    let armorText =
      if p.Armor.IsShredded then
        sprintf "%-22s [bold blink %s]SHREDDED (0%% soak) ⚠[/]" " Armor Integrity" Theme.Red
      else
        sprintf "%-22s [%s]%d / %d[/] [%s](%d%% soak)[/]" " Armor Integrity" Theme.Comment p.Armor.Current p.Armor.Max Theme.Yellow (int (p.Armor.AbsorptionRatio * 100.0))
    grid.AddRow(Markup(armorText)) |> ignore

    // Prominent low health warning banner
    if p.Health.Current <= p.Health.Maximum / 4 then
      grid.AddRow(Markup("[bold blink red]⚠ CRITICAL HEALTH: Rest or visit Shrine before combat![/]")) |> ignore
    elif p.Health.Current <= p.Health.Maximum / 2 then
      grid.AddRow(Markup("[bold yellow]⚠ WOUNDED: Health below 50%. Triage or rest recommended.[/]")) |> ignore

    // Stance, Complex Form, and Weapon Integrity
    if p.Plane = Mental then
      let formColor, formName =
        match p.ComplexForm with
        | Some ComplexForm.ResonanceSpike -> Theme.Pink, "Resonance Spike (Power)"
        | Some ComplexForm.PhantasmalDiffusion -> Theme.Green, "Phantasmal Diffusion (Agility)"
        | Some ComplexForm.AegisLattice -> Theme.Cyan, "Aegis Lattice (Discipline)"
        | None -> Theme.Comment, "Unthreaded"
      grid.AddRow(Markup(sprintf "%-22s [bold %s]%s[/]" "🧵 Complex Form" formColor formName)) |> ignore
    else
      let stanceColor =
        match p.Stance with
        | CombatStance.PowerStance -> Theme.Red
        | CombatStance.AgilityStance -> Theme.Green
        | CombatStance.DisciplineStance -> Theme.Purple
      grid.AddRow(Markup(sprintf "%-22s [bold %s]%A[/]" "󰓥 Combat Stance" stanceColor p.Stance)) |> ignore

      let weaponCondColor, weaponCondDesc =
        match p.WeaponCondition with
        | WeaponCondition.Pristine -> Theme.Green, "Pristine (100% dmg)"
        | WeaponCondition.Notched -> Theme.Yellow, "Notched (-10% dmg)"
        | WeaponCondition.Damaged -> Theme.Orange, "Damaged (-25% dmg) ⚠"
        | WeaponCondition.Broken -> Theme.Red, "Broken (-50% dmg) ☠"
      grid.AddRow(Markup(sprintf "%-22s [bold %s]%s[/]" "󰚌 Weapon Integrity" weaponCondColor weaponCondDesc)) |> ignore

    // Emotional Meters
    grid.AddRow(Markup(Display.renderMeter "󰈸 Recklessness" p.Meters.Recklessness Theme.Red)) |> ignore
    grid.AddRow(Markup(Display.renderMeter "󰓎 Overwhelm" p.Meters.Overwhelm Theme.Yellow)) |> ignore
    grid.AddRow(Markup(Display.renderMeter "󰒓 Exhaustion" p.Meters.Exhaustion Theme.Purple)) |> ignore

    grid.AddRow(Markup(sprintf "[bold %s]─── 󰒋 ASCENSION STATUS ───[/]" Theme.Cyan)) |> ignore
    let doorState =
      match Map.tryFind state.CurrentFloor.StairwayLocation state.CurrentFloor.Tiles with
      | Some (StairwayPortal ds) -> ds
      | _ -> DoorState.Open

    match doorState with
    | DoorState.Open ->
      grid.AddRow(Markup(sprintf "󰁝 Portal: [bold %s]UNLOCKED (Floor %d Stairs Ready)[/]" Theme.Green (state.CurrentFloor.FloorNumber + 1))) |> ignore
    | DoorState.LockedByKey(_, keyName, hint) ->
      grid.AddRow(Markup(sprintf "󰌆 Portal: [bold %s]SEALED (Requires: %s)[/]" Theme.Orange keyName)) |> ignore
      grid.AddRow(Markup(sprintf "          [italic %s]%s[/]" Theme.Comment hint)) |> ignore
    | DoorState.LockedByQuest(_, questTitle, req) ->
      grid.AddRow(Markup(sprintf "󱁕 Portal: [bold %s]BARRED (Trial: %s)[/]" Theme.Purple questTitle)) |> ignore
      grid.AddRow(Markup(sprintf "          [italic %s]%s[/]" Theme.Comment req)) |> ignore

    // Keys & Relics
    let keysDisplay =
      if state.CollectedKeys.IsEmpty then
        sprintf "[%s]None[/]" Theme.Comment
      else
        state.CollectedKeys
        |> Seq.map (sprintf "[bold %s]󰌆 %s[/]" Theme.Yellow)
        |> String.concat ", "
    grid.AddRow(Markup(sprintf "Vault Keys: %s" keysDisplay)) |> ignore

    let relicsDisplay =
      if state.InventoryItems.IsEmpty then
        sprintf "[%s]None[/]" Theme.Comment
      else
        state.InventoryItems
        |> List.map (fun it -> sprintf "[bold %s]󰆧 %s[/]" Theme.Pink it.Name)
        |> String.concat ", "
    grid.AddRow(Markup(sprintf "Relics: %s" relicsDisplay)) |> ignore

    // Souls & Alchemical Trophies
    let trophiesDisplay =
      if state.Trophies.IsEmpty then
        sprintf "[%s]None[/]" Theme.Comment
      else
        state.Trophies
        |> List.map (fun (t, cnt) -> sprintf "[bold %s]%s (x%d)[/]" Theme.Cyan t.Name cnt)
        |> String.concat ", "
    grid.AddRow(Markup(sprintf "󰮯 Souls: [bold gold1]%d[/]  |  💎 Trophies: %s" state.Souls trophiesDisplay)) |> ignore

    // Active Quests
    if not state.CurrentFloor.ActiveQuests.IsEmpty then
      grid.AddRow(Markup(sprintf "[bold %s]─── 󱁕 ACTIVE TRIALS ───[/]" Theme.Purple)) |> ignore
      for q in state.CurrentFloor.ActiveQuests do
        let status =
          if q.IsCompleted then sprintf "[bold %s][[COMPLETED]][/]" Theme.Green
          else sprintf "[bold %s][[IN PROGRESS]][/]" Theme.Yellow
        grid.AddRow(Markup(sprintf "• %s: %s" q.Title status)) |> ignore

    // Legend
    grid.AddRow(Markup(sprintf "[bold %s]─── 󰋜 ARCHITECTURAL LEGEND ───[/]" Theme.Comment)) |> ignore
    grid.AddRow(Markup(sprintf "[bold %s]@[/] You  [bold %s]![/] Enemy  [bold %s]?[/] NPC  [bold %s]⌹[/] Chest/Vault  [bold %s]†[/] Shrine  [bold #bd93f9]♨[/] Altar  [bold #8be9fd]$[/] Trader  [bold #50fa7b]✦[/] Echo  [bold #ff5555]✕[/] Trap"
      Theme.Yellow Theme.Red Theme.Cyan Theme.Yellow Theme.Green)) |> ignore
    grid.AddRow(Markup(sprintf "[%s]󰌌 Keys: Arrows/Vim: Move | x/; : Inspect | C: Character | Space: Wait | ? / F1: Symbol & Glyph Legend | Q: Quit%s[/]" Theme.Comment (if debug then " | F12: Debug Menu" else ""))) |> ignore

    Panel(grid)
      .Header(sprintf "[bold %s]󰍹 CHAMBER OBSERVATIONS[/]" Theme.Yellow)
      .Border(BoxBorder.Rounded)
      .BorderStyle(Theme.StyleCurrentLine)
      .Expand()

  /// Non-debug HUD used by callers that don't run the debug overlay (and by tests).
  let renderHud (state: TowerRunState) : Panel = renderHudWithDebug state false

  /// Restores the player's survival resources to full: Health/Morale to max, Armor to its
  /// created maximum, weapon to Pristine. God mode re-applies this every play-loop iteration
  /// so nothing can deplete or degrade the player.
  let godModeRestore (player: Combatant) : Combatant =
    { player with
        Health = Pool.Create player.Health.Maximum
        Morale = Pool.Create player.Morale.Maximum
        Armor = ArmorIntegrity.Create player.Armor.Max
        WeaponCondition = WeaponCondition.Pristine }

  /// Trauma-loop rewind applied on player defeat. Restores the player fully and clears every
  /// combat debuff (bleed, cripple, wards, clones, combo, collapse), then re-seats every
  /// non-boss mob on its original spawn tile with pristine vitals. Regenerating the floor from
  /// the run seed is deterministic, so the mob layout comes back exactly as it started.
  let traumaReset (state: TowerRunState) : TowerRunState =
    let player =
      { state.Player with
          Health = Pool.Create state.Player.Health.Maximum
          Morale = Pool.Create state.Player.Morale.Maximum
          Armor = ArmorIntegrity.Create state.Player.Armor.Max
          WeaponCondition = WeaponCondition.Pristine
          Meters = StatusMeters.Zero
          BleedStacks = 0
          LimbDebuff = 0
          ArcaneWard = 0
          MirrorClones = 0
          ComboTracker = ConsecutiveComboTracker.Zero
          Collapse = CollapseState.Stable }
    let pristine = TowerGenerator.generateFloorWithMode state.Seed state.CurrentFloor.FloorNumber state.IsStoryMode
    let pristineMobs =
      pristine.Entities
      |> Map.toList
      |> List.filter (fun (_, ent) ->
        match ent with
        | EntityEnemy e -> not (e.Id.StartsWith("boss_"))
        | _ -> false)
    // Drop the current non-boss mobs (moved and wounded), keep bosses/chests/NPCs, then re-seat
    // the pristine mobs so nothing stays aggroed or damaged across the rewind.
    let withoutMobs =
      state.CurrentFloor.Entities
      |> Map.filter (fun _ ent ->
        match ent with
        | EntityEnemy e -> e.Id.StartsWith("boss_")
        | _ -> true)
    let entities = pristineMobs |> List.fold (fun acc (pt, ent) -> Map.add pt ent acc) withoutMobs
    { state with
        Player = player
        PlayerPosition = state.CurrentFloor.SpawnLocation
        CurrentFloor = { state.CurrentFloor with Entities = entities } }

  /// Renders recent messages in a chronicle panel
  let renderMessageLog (state: TowerRunState) (maxLines: int) : Panel =
    let recent =
      if state.MessageLog.IsEmpty then
        [ "The cold, ancient stones of the Infinite Tower resonate with memory." ]
      else
        state.MessageLog |> List.truncate maxLines

    let logText =
      recent
      |> List.map (fun msg -> sprintf "[%s]󰁔[/] [bold %s]%s[/]" Theme.Pink Theme.Foreground (Markup.Escape msg))
      |> String.concat "\n"

    Panel(Markup(logText))
      .Header(sprintf "[bold %s]󰈙 Chronicle Log[/]" Theme.Foreground)
      .Border(BoxBorder.Square)
      .BorderStyle(Theme.StyleComment)
      .Expand()

  /// Displays an interactive modal dialog when speaking to an NPC
  let showNpcDialog (npc: TowerNpc) =
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore

    grid.AddRow(Markup(sprintf "[bold %s]%s[/] [italic %s](%s)[/]" Theme.Cyan npc.Name Theme.Comment npc.Role)) |> ignore
    grid.AddRow(Rule().RuleStyle(Theme.StyleComment)) |> ignore

    for line in npc.Dialogue do
      grid.AddRow(Markup(sprintf "[%s]\"%s\"[/]\n" Theme.Foreground line)) |> ignore

    match npc.Quest with
    | Some q ->
      grid.AddRow(Rule(sprintf "[bold %s]TRIAL OFFERED[/]" Theme.Yellow).RuleStyle(Theme.StyleYellow)) |> ignore
      grid.AddRow(Markup(sprintf "[bold %s]%s[/]" Theme.Yellow q.Title)) |> ignore
      grid.AddRow(Markup(sprintf "[%s]%s[/]" Theme.Foreground q.Description)) |> ignore
      grid.AddRow(Markup(sprintf "[bold %s]Reward:[/] [italic %s]%s[/]" Theme.Green Theme.Foreground q.RewardDescription)) |> ignore
    | None -> ()

    grid.AddRow(Markup(sprintf "\n[%s]Press any key to close dialogue...[/]" Theme.Comment)) |> ignore

    let panel =
      Panel(grid)
        .Border(BoxBorder.Heavy)
        .BorderStyle(Style(foreground = Nullable Theme.ColorCyan))

    AnsiConsole.Clear()
    AnsiConsole.Write(panel)
    Console.ReadKey(true) |> ignore

  /// Displays an evocative narrative modal when discovering a roadside memory fragment
  let showMemoryEchoDialog (echo: MemoryEchoData) =
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore

    grid.AddRow(Markup(sprintf "[bold %s]✧ AWAKENED REPRESSED MEMORY: %s[/]" Theme.Pink echo.Title)) |> ignore
    grid.AddRow(Rule().RuleStyle(Theme.StylePurple)) |> ignore

    grid.AddRow(Markup(sprintf "[italic %s]\"%s\"[/]\n" Theme.Cyan echo.SensoryDetail)) |> ignore

    for line in echo.MemoryTranscript do
      grid.AddRow(Markup(sprintf "[%s]%s[/]" Theme.Foreground line)) |> ignore

    grid.AddRow(Rule().RuleStyle(Theme.StyleComment)) |> ignore
    grid.AddRow(Markup(sprintf "[bold %s]󰄬 Cognitive clarity restored: +%d Morale[/]" Theme.Green echo.MoraleRecovery)) |> ignore
    grid.AddRow(Markup(sprintf "\n[%s]Press any key to awaken and continue...[/]" Theme.Comment)) |> ignore

    let panel =
      Panel(grid)
        .Border(BoxBorder.Heavy)
        .BorderStyle(Style(foreground = Nullable Theme.ColorPurple))

    AnsiConsole.Clear()
    AnsiConsole.Write(panel)
    Console.ReadKey(true) |> ignore

  /// Shows the comprehensive symbol, glyph, and control manual
  let showHelpManual () =
    Display.showSymbolAndGlyphLegend ()

  /// Renders the full character sheet: mastery tier, XP progress, and the 12-attribute matrix.
  let renderCharacterSheet (state: TowerRunState) : Panel =
    let p = state.Player
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore
    grid.AddColumn(GridColumn().NoWrap()) |> ignore

    let tier = ProgressionScale.levelToTier p.Level

    grid.AddRow(Markup(sprintf "[bold %s]%s[/]  [grey](%s • Level %d)[/]" Theme.Yellow p.Name p.Class.Name p.Level)) |> ignore
    grid.AddRow(Markup(sprintf "Mastery Tier: [bold %s]%s[/]    Plane: [bold %s]%s[/]" Theme.Cyan (tier.ToString()) Theme.Purple (p.Plane.ToString()))) |> ignore
    grid.AddRow(Markup(sprintf "Experience: [bold %s]%d / %d[/] toward Level %d" Theme.Green p.Progression.CurrentXP p.Progression.ExperienceToNext (p.Level + 1))) |> ignore
    grid.AddRow(Markup(sprintf "[grey]Attributes are stance-adjusted: +10%% matching vector, -10%% the plane's other vectors.[/]")) |> ignore
    grid.AddRow(Rule().RuleStyle(Theme.StyleComment)) |> ignore

    grid.AddRow(Markup(sprintf "[bold %s]─── 󰓥 VITALS ───[/]" Theme.Yellow)) |> ignore
    grid.AddRow(Markup(Display.renderBar "󰋑 Health" p.Health.Current p.Health.Maximum Theme.Red)) |> ignore
    grid.AddRow(Markup(Display.renderBar "󰧑 Morale" p.Morale.Current p.Morale.Maximum Theme.Cyan)) |> ignore
    grid.AddRow(Markup(sprintf "%-22s [%s]%d / %d[/] [%s](%d%% soak)[/]" " Armor Integrity" Theme.Comment p.Armor.Current p.Armor.Max Theme.Yellow (int (p.Armor.AbsorptionRatio * 100.0)))) |> ignore

    let renderPlane (plane: Plane) (planeColor: string) =
      grid.AddRow(Markup(sprintf "[bold %s]─── %s PLANE ───[/]" planeColor (plane.ToString().ToUpperInvariant()))) |> ignore

      for vector in [ Vector.Power; Vector.Agility; Vector.Discipline ] do
        let cells =
          Attributes.all
          |> List.filter (fun stat ->
            let d = Attributes.descriptorOf stat
            d.Plane = plane && d.Vector = vector)
          |> List.map (fun stat ->
            let d = Attributes.descriptorOf stat
            let tag = if d.Orientation = Offense then "OFF" else "DEF"
            sprintf "[%s]%s[/] [bold white]%d[/] [grey]%s[/]" planeColor d.CanonicalName (p.GetCombatStat stat) tag)
          |> String.concat "      "

        grid.AddRow(Markup(sprintf "[grey]%s[/]  %s" (vector.ToString()) cells)) |> ignore

    renderPlane Physical Theme.Red
    renderPlane Mental Theme.Purple

    grid.AddRow(Rule().RuleStyle(Theme.StyleComment)) |> ignore

    if p.Plane = Mental then
      let formName = p.ComplexForm |> Option.map (fun f -> f.Name) |> Option.defaultValue "Unthreaded"
      grid.AddRow(Markup(sprintf "Complex Form: [bold %s]%s[/]" Theme.Pink formName)) |> ignore
    else
      grid.AddRow(Markup(sprintf "Combat Stance: [bold %s]%A[/]" Theme.Green p.Stance)) |> ignore

    if not p.Preparations.IsEmpty then
      grid.AddRow(Markup(sprintf "[bold %s]─── TACTICAL PREPARATIONS ───[/]" Theme.Cyan)) |> ignore

      for slot in p.Preparations do
        grid.AddRow(Markup(sprintf "• [bold white]%A[/]  [grey]%d / %d uses[/]" slot.Type slot.RemainingUses slot.MaxUses)) |> ignore

    Panel(grid)
      .Header(sprintf "[bold %s]󰒋 CHARACTER SHEET[/]" Theme.Cyan)
      .Border(BoxBorder.Rounded)
      .BorderStyle(Theme.StyleCurrentLine)
      .Expand()

  /// Opens the character sheet modal and waits for dismissal.
  let showCharacterSheet (state: TowerRunState) : unit =
    AnsiConsole.Clear()
    AnsiConsole.Write(renderCharacterSheet state)
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(sprintf "[%s]Press any key to return to the chamber...[/]" Theme.Comment)
    Console.ReadKey(true) |> ignore

  /// Displays a celebratory banner when the player has gained a level.
  let showLevelUpBanner (state: TowerRunState) (previousLevel: int) : unit =
    let p = state.Player

    if p.Level > previousLevel then
      let previousTier = ProgressionScale.levelToTier previousLevel
      let currentTier = ProgressionScale.levelToTier p.Level

      let rankLine =
        if currentTier <> previousTier then
          sprintf "\n[bold gold1]★ RANK BREAKTHROUGH — %s ★[/]" (currentTier.ToString())
        else
          ""

      let panel =
        Panel(Markup(sprintf "[bold green]⬆ LEVEL UP![/]  [bold white]Level %d → %d[/]%s\n[italic grey]All attributes, Health, Morale, and Armor scale to your new mastery.[/]"
          previousLevel p.Level rankLine))
          .Header(sprintf "[bold %s]󰒋 ASCENSION[/]" Theme.Green)
          .Border(BoxBorder.Double)
          .BorderStyle(Style(foreground = Nullable Color.Green))

      AnsiConsole.Clear()
      AnsiConsole.Write(Align.Center(panel))
      AnsiConsole.WriteLine()
      AnsiConsole.MarkupLine(sprintf "[%s]Press any key to continue...[/]" Theme.Comment)
      Console.ReadKey(true) |> ignore

  /// Main interactive turn loop for Tower dungeon crawling and Story Mode stage exploration
  let runTowerCrawlWithMode
    (initialPlayer: Combatant)
    (startFloor: int)
    (onCombatDuel: int -> Combatant -> Combatant -> (CombatOutcome * Combatant))
    (onGroupCombat: int -> Combatant -> TowerEnemy list -> bool -> GroupDuelResult)
    (onDefeatAnimation: unit -> unit)
    (debug: bool)
    (forcedSeed: int option)
    (isStory: bool) : unit =

    let rng = Random()
    let seed = defaultArg forcedSeed (rng.Next(10000, 99999))
    let mutable state = TowerSession.initSessionWithMode initialPlayer seed startFloor isStory
    let mutable sessionActive = true
    let mutable inspectCursor : Point option = None
    let mutable fleeGrace = 0
    let mutable godMode = false

    let livingEnemyCount () =
      state.CurrentFloor.Entities
      |> Map.toList
      |> List.sumBy (fun (_, ent) ->
        match ent with
        | EntityEnemy e when not e.IsDefeated -> 1
        | _ -> 0)

    RuntimeLog.section (sprintf "FLOOR %d" startFloor)
    RuntimeLog.write (
      sprintf "floor load: seed=%d floor=%d size=%dx%d theme=%s enemies=%d story=%b"
        seed state.CurrentFloor.FloorNumber state.CurrentFloor.Width state.CurrentFloor.Height
        state.CurrentFloor.Theme.Name (livingEnemyCount ()) isStory)

    // Resolves a multi-enemy engagement and applies its outcome to the run state.
    let handleGroup (floorNum: int) (engaged: TowerEnemy list) =
      if not engaged.IsEmpty then
        let canFlee = TowerSession.canEscape state
        // Armor/weapon wear during the fight is a debuff: snapshot, then restore when it ends.
        let playerConditionBefore = TowerSession.combatCondition state.Player
        let enemyConditionsBefore =
          engaged |> List.map (fun e -> e.Id, TowerSession.combatCondition e.Combatant)
        let result = onGroupCombat floorNum state.Player engaged canFlee
        let armorBefore, weaponBefore = playerConditionBefore
        state <- { state with Player = TowerSession.restoreCombatCondition armorBefore weaponBefore result.Player }
        // Persist every updated enemy with its pre-fight condition, then award each one the group put down.
        let restoredEnemies =
          result.Enemies
          |> List.map (fun e ->
            match enemyConditionsBefore |> List.tryFind (fun (id, _) -> id = e.Id) with
            | Some (_, (armor, weapon)) -> { e with Combatant = TowerSession.restoreCombatCondition armor weapon e.Combatant }
            | None -> e)
        for e in restoredEnemies do
          state <- TowerSession.updateEnemy e state
        for e in restoredEnemies do
          if e.IsDefeated then
            let levelBefore = state.Player.Level
            state <- TowerSession.resolveEnemyDefeat e.Id state
            showLevelUpBanner state levelBefore

        match result.Outcome with
        | GroupDuelOutcome.Victorious ->
          ()

        | GroupDuelOutcome.Defeated ->
          onDefeatAnimation()
          // Rewind: restore the player and put every non-boss mob back where it spawned, undamaged.
          state <- traumaReset state
          state <-
            { state with
                MessageLog = "Truck-kun strikes! Rewound through the trauma loop. You awaken at the entrance. All souls, trophies, and gear preserved!" :: state.MessageLog }
          state <- TowerSession.updateFov state
          AnsiConsole.MarkupLine(sprintf "\n[bold red]Trauma loop reset! Rewound to entrance. Health restored, progression preserved. Grind and prepare![/]")
          Thread.Sleep(1200)

        | GroupDuelOutcome.Fled ->
          // Break away to the free neighbouring tile furthest from the pack.
          let neighbours =
            Point.allNeighbors state.PlayerPosition
            |> List.filter (fun p ->
              let walkable =
                match Map.tryFind p state.CurrentFloor.Tiles with
                | Some t -> t.IsWalkable
                | None -> false
              let blocked =
                match Map.tryFind p state.CurrentFloor.Entities with
                | Some ent -> ent.BlocksMovement
                | None -> false
              walkable && not blocked)
          let enemyPoints =
            state.CurrentFloor.Entities
            |> Map.toList
            |> List.choose (fun (p, ent) -> match ent with EntityEnemy e when not e.IsDefeated -> Some p | _ -> None)
          match neighbours with
          | [] -> ()
          | _ ->
            let escapePt =
              neighbours
              |> List.maxBy (fun p ->
                enemyPoints |> List.map (fun ep -> Distance.chebyshev p ep) |> List.fold min Int32.MaxValue)
            state <- { state with PlayerPosition = escapePt }
            state <- TowerSession.updateFov state
            fleeGrace <- 1

    // Runs the enemy pursuit/engagement phase after a turn-consuming action.
    let runEnemyPhase () =
      if fleeGrace > 0 then
        fleeGrace <- fleeGrace - 1
        RuntimeLog.write (sprintf "enemy phase skipped (flee grace remaining %d)" fleeGrace)
      else
        RuntimeLog.write "enemy phase: begin"
        let s2, evs = TowerSession.stepEnemies state
        state <- s2
        for ev in evs do
          match ev with
          | TowerEvent.GroupCombatTriggered enemies ->
            RuntimeLog.write (sprintf "enemy phase: group combat triggered (%d foes)" enemies.Length)
            handleGroup state.CurrentFloor.FloorNumber enemies
          | _ -> ()
        RuntimeLog.write "enemy phase: end"

    // ── Debug overlay (--debug) ────────────────────────────────────────────
    // Every helper mutates the live run state; the F12 menu loops until Close.

    let debugLogEvent (ev: TowerEvent) =
      let s = sprintf "%A" ev
      RuntimeLog.write (sprintf "event: %s" (if s.Length > 200 then s.Substring(0, 200) else s))

    let debugTeleport () =
      let floor = AnsiConsole.Prompt(TextPrompt<int>("[bold yellow]Teleport to floor:[/]"))
      let regenerated = TowerGenerator.generateFloorWithMode seed floor isStory
      state <- { state with CurrentFloor = regenerated; PlayerPosition = regenerated.SpawnLocation }
      state <- TowerSession.updateFov state
      RuntimeLog.write (sprintf "debug: teleport -> floor %d spawn %A" floor regenerated.SpawnLocation)
      AnsiConsole.MarkupLine(sprintf "[bold green]Teleported to floor %d.[/]" floor)

    let debugSetLevel () =
      let level = AnsiConsole.Prompt(TextPrompt<int>("[bold yellow]Set level:[/]"))
      let leveled = TierFactory.rescaleToLevel state.Player level
      // rescaleToLevel rewrites Level/PrimaryStat; reattach the live profile
      // (XP, class, preparations) exactly like TowerSession.applyExperience.
      let player =
        { leveled with
            Progression =
              { state.Player.Progression with
                  Level = leveled.Progression.Level
                  PrimaryStat = leveled.Progression.PrimaryStat } }
      state <- { state with Player = player }
      RuntimeLog.write (sprintf "debug: set level %d" level)
      AnsiConsole.MarkupLine(sprintf "[bold green]Level set to %d.[/]" level)

    let debugRevealMap () =
      let allTiles = state.CurrentFloor.Tiles |> Map.toSeq |> Seq.map fst |> Set.ofSeq
      state <- { state with CurrentFloor = { state.CurrentFloor with Explored = allTiles } }
      RuntimeLog.write (sprintf "debug: revealed map (%d tiles)" allTiles.Count)
      AnsiConsole.MarkupLine(sprintf "[bold green]Revealed all %d tiles.[/]" allTiles.Count)

    let debugSpawnEnemy () =
      let freeTile =
        Point.allNeighbors state.PlayerPosition
        |> List.filter (fun p ->
          let walkable =
            match Map.tryFind p state.CurrentFloor.Tiles with
            | Some t -> t.IsWalkable
            | None -> false
          walkable && not (Map.containsKey p state.CurrentFloor.Entities))
        |> List.tryHead
      match freeTile with
      | None -> AnsiConsole.MarkupLine(sprintf "[bold %s]No free tile adjacent to the player.[/]" Theme.Red)
      | Some pt ->
        let template =
          match Bestiary.byBiome state.CurrentFloor.Theme with
          | [] -> Bestiary.slagHound
          | list -> list.Head
        let combatant = TierFactory.rescaleToLevel (Bestiary.createMonster template) state.Player.Level
        let enemy =
          { Id = sprintf "debug_enemy_%d_%d" state.CurrentFloor.FloorNumber (state.CurrentFloor.Entities.Count)
            Name = combatant.Name
            Combatant = combatant
            DropsKeyId = None
            IsDefeated = false }
        state <- { state with CurrentFloor = { state.CurrentFloor with Entities = Map.add pt (EntityEnemy enemy) state.CurrentFloor.Entities } }
        RuntimeLog.write (sprintf "debug: spawned %s (level %d) at %A" enemy.Name enemy.Combatant.Level pt)
        AnsiConsole.MarkupLine(sprintf "[bold green]Spawned %s at (%d, %d).[/]" enemy.Name pt.X pt.Y)

    let debugClearEnemies () =
      let before = livingEnemyCount ()
      state <-
        { state with
            CurrentFloor =
              { state.CurrentFloor with
                  Entities =
                    state.CurrentFloor.Entities
                    |> Map.filter (fun _ ent -> match ent with EntityEnemy _ -> false | _ -> true) } }
      RuntimeLog.write (sprintf "debug: cleared %d enemies" before)
      AnsiConsole.MarkupLine(sprintf "[bold green]Removed %d enemies from the floor.[/]" before)

    let debugForceEncounter () =
      let foes =
        (TowerSession.adjacentEnemies state) @ (TowerSession.enemiesAggroed state)
        |> List.distinctBy (fun e -> e.Id)
      if foes.IsEmpty then
        RuntimeLog.write "debug: force encounter -> no adjacent or aggroed foes"
        AnsiConsole.MarkupLine(sprintf "[bold %s]No adjacent or aggroed enemies to engage.[/]" Theme.Yellow)
      else
        RuntimeLog.write (sprintf "debug: force encounter with %d foes" foes.Length)
        handleGroup state.CurrentFloor.FloorNumber foes

    let rec debugMenu () =
      AnsiConsole.Clear()
      let choice =
        Display.promptWithVim(
          SelectionPrompt<string>()
            .Title(sprintf "[bold red]󰒲 DEBUG MENU[/] [%s]seed: %d  floor: %d[/]" Theme.Comment seed state.CurrentFloor.FloorNumber)
            .PageSize(10)
            .AddChoices([
              sprintf "God mode: %s" (if godMode then "[bold green]ON[/]" else "[bold red]OFF[/]")
              "Teleport to floor..."
              "Set level..."
              "Reveal whole map"
              "Spawn enemy adjacent"
              "Clear all enemies"
              "Force encounter with aggroed foes"
              "Close"
            ])
        )
      if choice.Contains("God mode") then
        godMode <- not godMode
        AnsiConsole.MarkupLine(sprintf "[bold yellow]God mode %s.[/]" (if godMode then "enabled" else "disabled"))
        debugMenu ()
      elif choice.Contains("Teleport") then debugTeleport (); debugMenu ()
      elif choice.Contains("Set level") then debugSetLevel (); debugMenu ()
      elif choice.Contains("Reveal") then debugRevealMap (); debugMenu ()
      elif choice.Contains("Spawn enemy") then debugSpawnEnemy (); debugMenu ()
      elif choice.Contains("Clear all enemies") then debugClearEnemies (); debugMenu ()
      elif choice.Contains("Force encounter") then debugForceEncounter (); debugMenu ()
      else ()

    while sessionActive do
      AnsiConsole.Clear()

      // God mode re-applies before each rendered frame so the player can never
      // be depleted or degraded by the previous turn's combat/pursuit.
      if debug && godMode then
        state <- { state with Player = godModeRestore state.Player }

      let termW = Math.Max(80, AnsiConsole.Profile.Width)
      let termH =
        if Console.IsOutputRedirected || Console.WindowHeight <= 0 then 30
        else Console.WindowHeight

      // Header rule
      let floorRule =
        match inspectCursor with
        | Some _ ->
          Rule(sprintf "[bold %s]󰍹 FORNACH: TILE & HAZARD INSPECTION MODE ── FLOOR %d: %s[/]"
            Theme.Yellow
            state.CurrentFloor.FloorNumber
            state.CurrentFloor.Theme.Name)
            .Centered()
            .RuleStyle(Theme.StyleYellow)
        | None ->
          if state.IsStoryMode && state.CurrentFloor.FloorNumber <= 7 then
            let stageName =
              match state.CurrentFloor.FloorNumber with
              | 1 -> "PROLOGUE: THE RAIN-SOAKED FOREST"
              | 2 -> "STAGE 1: GUILT & HESITATION"
              | 3 -> "STAGE 2: DENIAL"
              | 4 -> "STAGE 3: ANGER"
              | 5 -> "STAGE 4: BARGAINING"
              | 6 -> "STAGE 5: DEPRESSION"
              | _ -> "STAGE 6: ACCEPTANCE"
            Rule(sprintf "[bold %s]󰈙 FORNACH: %s ── %s[/]"
              state.CurrentFloor.Theme.ColorHex
              stageName
              state.CurrentFloor.Theme.Name)
              .Centered()
              .RuleStyle(Style(foreground = Nullable (themeColor state.CurrentFloor.Theme)))
          else
            Rule(sprintf "[bold %s]󰒋 FORNACH: THE INFINITE TOWER ── FLOOR %d: %s[/]"
              state.CurrentFloor.Theme.ColorHex
              state.CurrentFloor.FloorNumber
              state.CurrentFloor.Theme.Name)
              .Centered()
              .RuleStyle(Style(foreground = Nullable (themeColor state.CurrentFloor.Theme)))

      AnsiConsole.Write(floorRule)
      match inspectCursor with
      | Some _ ->
        AnsiConsole.MarkupLine(sprintf "[italic %s]Pan inspection reticle over chamber tiles to examine terrain, hazards, and occupants.[/]\n" Theme.Yellow)
      | None ->
        AnsiConsole.MarkupLine(sprintf "[italic %s]%s[/]\n" Theme.Comment state.CurrentFloor.Theme.Description)

      // Dynamic viewport calculation to fill the terminal window
      let viewW, hudW = layoutWidths termW state.CurrentFloor.Width

      // Vertical space: header (~4 rows) + log/inspector (4-8 rows) + borders/margins (~6 rows)
      let logLines = if termH >= 45 then 8 elif termH >= 35 then 6 else 4
      let availableH = termH - 4 - logLines - 6
      let viewH = Math.Clamp(availableH, 21, state.CurrentFloor.Height)

      // Layout: Left (Viewport), Right (HUD), Bottom (Log or Inspector)
      let viewportPanel = renderViewportWithCursor state inspectCursor viewW viewH
      let hudPanel = renderHudWithDebug state debug
      let bottomPanel =
        match inspectCursor with
        | Some curPt -> renderInspectionPanel state curPt
        | None -> renderMessageLog state logLines

      // Pin both columns: otherwise the HUD's longest row (inventory, keys) sizes its
      // column and squeezes the map viewport down to a sliver.
      let grid = Grid()
      grid.AddColumn(GridColumn().Width(viewW + 2)) |> ignore
      grid.AddColumn(GridColumn().Width(hudW)) |> ignore
      grid.AddRow(viewportPanel, hudPanel) |> ignore
      grid.Expand <- true

      AnsiConsole.Write(grid)
      AnsiConsole.Write(bottomPanel)

      // Read player command
      let key = Console.ReadKey(true)
      RuntimeLog.write (
        sprintf "input: floor=%d pos=%A facing=%A inspect=%b key=%A char=%A"
          state.CurrentFloor.FloorNumber state.PlayerPosition state.Facing (inspectCursor.IsSome) key.Key key.KeyChar)

      match inspectCursor with
      | Some curPt ->
        // In Inspect Mode: navigate reticle or exit
        let moveDirOpt =
          match key.Key with
          | ConsoleKey.UpArrow | ConsoleKey.W | ConsoleKey.K | ConsoleKey.NumPad8 -> Some Direction.North
          | ConsoleKey.DownArrow | ConsoleKey.S | ConsoleKey.J | ConsoleKey.NumPad2 -> Some Direction.South
          | ConsoleKey.LeftArrow | ConsoleKey.A | ConsoleKey.H | ConsoleKey.NumPad4 -> Some Direction.West
          | ConsoleKey.RightArrow | ConsoleKey.D | ConsoleKey.L | ConsoleKey.NumPad6 -> Some Direction.East
          | ConsoleKey.Y | ConsoleKey.NumPad7 -> Some Direction.NorthWest
          | ConsoleKey.U | ConsoleKey.NumPad9 -> Some Direction.NorthEast
          | ConsoleKey.B | ConsoleKey.NumPad1 -> Some Direction.SouthWest
          | ConsoleKey.N | ConsoleKey.NumPad3 -> Some Direction.SouthEast
          | _ -> None

        match moveDirOpt with
        | Some dir ->
          let nextPt = curPt + Direction.toDelta dir
          let clampedX = Math.Clamp(nextPt.X, 0, state.CurrentFloor.Width - 1)
          let clampedY = Math.Clamp(nextPt.Y, 0, state.CurrentFloor.Height - 1)
          inspectCursor <- Some { X = clampedX; Y = clampedY }
        | None ->
          match key.Key with
          | ConsoleKey.Escape | ConsoleKey.Enter | ConsoleKey.Spacebar ->
            inspectCursor <- None
          | ConsoleKey.F1 ->
            showHelpManual ()
          | ConsoleKey.F12 when debug ->
            debugMenu ()
          | _ when key.KeyChar = 'x' || key.KeyChar = 'X' || key.KeyChar = ';' ->
            inspectCursor <- None
          | _ when key.KeyChar = '?' ->
            showHelpManual ()
          | _ -> ()

      | None ->
        // Standard Locomotion Mode
        let dirOpt =
          match key.Key with
          | ConsoleKey.UpArrow | ConsoleKey.W | ConsoleKey.K | ConsoleKey.NumPad8 -> Some Direction.North
          | ConsoleKey.DownArrow | ConsoleKey.S | ConsoleKey.J | ConsoleKey.NumPad2 -> Some Direction.South
          | ConsoleKey.LeftArrow | ConsoleKey.A | ConsoleKey.H | ConsoleKey.NumPad4 -> Some Direction.West
          | ConsoleKey.RightArrow | ConsoleKey.D | ConsoleKey.L | ConsoleKey.NumPad6 -> Some Direction.East
          | ConsoleKey.Y | ConsoleKey.NumPad7 -> Some Direction.NorthWest
          | ConsoleKey.U | ConsoleKey.NumPad9 -> Some Direction.NorthEast
          | ConsoleKey.B | ConsoleKey.NumPad1 -> Some Direction.SouthWest
          | ConsoleKey.N | ConsoleKey.NumPad3 -> Some Direction.SouthEast
          | _ -> None

        match dirOpt with
        | Some dir ->
          let posBefore = state.PlayerPosition
          let nextState, events = TowerSession.stepPlayer dir state
          state <- nextState
          RuntimeLog.write (
            sprintf "action: move %A %A -> %A (%d event(s))"
              dir posBefore state.PlayerPosition events.Length)

          for ev in events do
            debugLogEvent ev
            match ev with
            | TowerEvent.CombatTriggered _ when (TowerSession.adjacentEnemies state).Length > 1 ->
              // More than one foe is on us: resolve the whole pack as a group engagement.
              handleGroup state.CurrentFloor.FloorNumber (TowerSession.adjacentEnemies state)

            | TowerEvent.CombatTriggered enemy ->
              AnsiConsole.WriteLine()
              let isBoss = enemy.Id.StartsWith("boss_") || enemy.Name.StartsWith("Aspect of")

              if isBoss then
                let scene = EnvironmentScenes.getSceneForEnemy enemy.Name
                AnsiConsole.Clear()
                AnsiConsole.Write(EnvironmentScenes.renderSceneHeader scene)
                AnsiConsole.WriteLine()
                AnsiConsole.Write(EnvironmentScenes.renderBossEncounterCard enemy.Combatant scene)
                AnsiConsole.WriteLine()

              let choice =
                Display.promptSelectionWithHelp
                  (sprintf "[bold red]󰈸 %s blocks your path: %s![/]"
                    (if isBoss then "TRAUMA MANIFESTATION" else "A formidable foe") enemy.Name)
                  [
                    "󰓥  Engage in Tactical Dueling Combat"
                    "⚡  Quick Resolve (Overcome with Standard Prowess)"
                    "🏃  Step Back / Disengage"
                  ]
                  None
                  (fun () -> ())

              RuntimeLog.write (sprintf "choice: %s (encounter %s)" (Markup.Remove choice) enemy.Name)

              if choice.Contains("Engage in Tactical") then
                let levelBefore = state.Player.Level
                // Armor/weapon wear is a combat debuff: snapshot, then restore when the fight ends.
                let armorBefore, weaponBefore = TowerSession.combatCondition state.Player
                let outcome, updatedPlayer = onCombatDuel state.CurrentFloor.FloorNumber state.Player enemy.Combatant
                if outcome = CombatOutcome.PlayerVictorious then
                  if isBoss then
                    // Overcoming the trauma aspect grants total catharsis:
                    let catharticPlayer =
                      { updatedPlayer with
                          Health = Pool.Create updatedPlayer.Health.Maximum
                          Morale = Pool.Create updatedPlayer.Morale.Maximum
                          Armor = ArmorIntegrity.Create updatedPlayer.Armor.Max
                          WeaponCondition = WeaponCondition.Pristine
                          BleedStacks = 0
                          LimbDebuff = 0
                          Meters = StatusMeters.Zero }
                    state <- { state with Player = catharticPlayer }

                    let memoryTitle, memoryDesc =
                      match state.CurrentFloor.FloorNumber with
                      | 1 -> "Shattered Windshield", "In the puddle at your feet, you see the reflection of a crumpled sedan, a shattered crosswalk signal, and a girl's hand slipping from your grasp."
                      | 2 -> "Broken Yellow Umbrella", "A crumpled yellow umbrella crushed beneath tire treads. The mist parts as the illusion shatters."
                      | 3 -> "Shouting in the Hallway", "Harsh, regretful words screamed just minutes before the fatal crossing. The burning rage cools into quiet ash."
                      | 4 -> "Hospital Heart Monitors", "The steady, frantic beeping of intensive care monitors. The desperate trades whispered in the dark."
                      | 5 -> "The Empty Bedroom", "The suffocating stillness of walking past an untouched bedroom. The weight of absence."
                      | _ -> "The Crosswalk Intersection", "The white lilies blur into headlights and rain. The collision was not your fault. Acceptance at last."
                    let memPanel =
                      Panel(Markup(sprintf "[bold gold1]★ TRAUMA MEMORY OVERCOME:[/] [bold white]%s[/]\n[italic grey]%s[/]\n[bold green]✦ Psychological breakthrough: Health, Morale, Armor, and Weapon restored to Pristine![/]" memoryTitle memoryDesc))
                        .Border(BoxBorder.Heavy)
                        .BorderStyle(Style(foreground = Nullable Color.Gold1))
                    AnsiConsole.Clear()
                    AnsiConsole.Write(memPanel)
                    AnsiConsole.WriteLine()
                    AnsiConsole.MarkupLine(sprintf "[bold %s]The Ascension Door unlocks! You may now step through the portal.[/]\n" Theme.Green)
                    AnsiConsole.MarkupLine(sprintf "[%s]Press any key to resume exploration...[/]" Theme.Comment)
                    Console.ReadKey(true) |> ignore
                  else
                    // No post-encounter healing: recover by moving (10%/step) or waiting (33%) instead.
                    // Armor/weapon debuffs clear now the fight is over; trap damage taken before it is kept.
                    state <- { state with Player = TowerSession.restoreCombatCondition armorBefore weaponBefore updatedPlayer }
                    AnsiConsole.MarkupLine(sprintf "\n[bold %s]Adversary vanquished![/]" Theme.Green)
                    Thread.Sleep(900)

                  state <- TowerSession.resolveEnemyDefeat enemy.Id state
                  showLevelUpBanner state levelBefore
                else
                  onDefeatAnimation()
                  // Rewind: restore the player and put every non-boss mob back where it spawned, undamaged.
                  state <- traumaReset state
                  state <-
                    { state with
                        MessageLog = "Truck-kun strikes! Rewound through the trauma loop. You awaken at the entrance. All souls, trophies, and gear preserved!" :: state.MessageLog }
                  state <- TowerSession.updateFov state
                  AnsiConsole.MarkupLine(sprintf "\n[bold red]Trauma loop reset! Rewound to entrance. Health restored, progression preserved. Grind and prepare![/]")
                  Thread.Sleep(1200)

              elif choice.Contains("Quick Resolve") then
                let levelBefore = state.Player.Level
                let strainedPlayer =
                  { state.Player with
                      Health = state.Player.Health.ApplyDelta -15
                      Morale = state.Player.Morale.ApplyDelta -10
                      Meters = StatusMeters.Zero }
                state <- { state with Player = strainedPlayer }
                state <- TowerSession.resolveEnemyDefeat enemy.Id state
                AnsiConsole.MarkupLine(sprintf "\n[bold %s]With steady resolve, you shatter the foe's stance![/]" Theme.Green)
                Thread.Sleep(700)
                showLevelUpBanner state levelBefore

              else
                // Disengage
                ()

            | TowerEvent.ChestOpened(chest, itemOpt, _) ->
              AnsiConsole.WriteLine()
              let rewardText =
                match itemOpt with
                | Some item -> sprintf "[bold %s]Relic Discovered:[/] %s\n[italic grey]%s[/]" Theme.Yellow item.Name item.Description
                | None -> "The chest mechanisms yield."
              AnsiConsole.MarkupLine(sprintf "\n[bold %s]⌹ %s[/]\n%s" Theme.Yellow chest.Description rewardText)
              Thread.Sleep(900)

            | TowerEvent.NpcInteracted(npc, _) ->
              showNpcDialog npc

            | TowerEvent.AltarEncountered(altar, pt) ->
              AnsiConsole.WriteLine()
              let choice =
                Display.promptSelectionWithHelp
                  (sprintf "[bold %s]♨ %s[/]\n[italic %s]%s[/]\n[bold %s]Cost: %s[/]  ──>  [bold %s]Reward: %s[/]"
                    Theme.Purple altar.Name Theme.Comment altar.Description Theme.Red altar.Cost.Description Theme.Green altar.Reward.Description)
                  [
                    "󰄬  Accept Sacrifice and Receive Boon"
                    "🏃  Step Back / Refuse Pact"
                  ]
                  None
                  (fun () -> ())
              RuntimeLog.write (sprintf "choice: %s (altar %s)" (Markup.Remove choice) altar.Name)
              if choice.Contains("Accept") then
                match TowerSession.applyAltarSacrifice pt state with
                | Ok updatedState ->
                  state <- updatedState
                  AnsiConsole.MarkupLine(sprintf "\n[bold %s]The altar drinks your sacrifice. The blessing takes hold![/]" Theme.Green)
                  Thread.Sleep(900)
                | Error msg ->
                  AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s[/]" Theme.Red msg)
                  Thread.Sleep(700)

            | TowerEvent.TraderEncountered(merchant, pt) ->
              AnsiConsole.WriteLine()
              let choices =
                merchant.Wares
                |> List.mapi (fun i w ->
                  let trophyPart =
                    match w.RequiredTrophy with
                    | Some (t, cnt) -> sprintf " + %d %s" cnt t.Name
                    | None -> ""
                  if w.IsPurchased then
                    sprintf "[grey]%d. [STRIKETHROUGH]%s[/] (Purchased)[/]" (i + 1) w.Item.Name
                  else
                    sprintf "󰆧  Buy %s ([bold gold1]%d Souls[/]%s)" w.Item.Name w.CostSouls trophyPart)
                |> fun list -> list @ [ "🏃  Leave Shop" ]

              let choice =
                Display.promptSelectionWithHelp
                  (sprintf "[bold %s]󱁠 %s, %s[/]\n[italic %s]\"%s\"[/]\n[bold gold1]Your Souls: %d[/]"
                    Theme.Cyan merchant.Name merchant.Title Theme.Comment (List.head merchant.Dialogue) state.Souls)
                  choices
                  None
                  (fun () -> ())
              RuntimeLog.write (sprintf "choice: %s (trader %s)" (Markup.Remove choice) merchant.Name)
              if not (choice.Contains("Leave")) then
                let selectedIdx =
                  merchant.Wares
                  |> List.tryFindIndex (fun w -> choice.Contains(w.Item.Name))
                match selectedIdx with
                | Some idx ->
                  match TowerSession.buyFromTrader pt idx state with
                  | Ok updatedState ->
                    state <- updatedState
                    AnsiConsole.MarkupLine(sprintf "\n[bold %s]Transaction complete! Item placed in inventory.[/]" Theme.Green)
                    Thread.Sleep(900)
                  | Error err ->
                    AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s[/]" Theme.Red err)
                    Thread.Sleep(900)
                | None -> ()

            | TowerEvent.VaultEncountered(vault, pt) ->
              AnsiConsole.WriteLine()
              let puzzleInfo =
                match vault.Puzzle with
                | KeyholeLock (_, name, hint) -> sprintf "Locked by key: [[%s]] (%s)" name hint
                | StatCheck (stat, req, desc) -> sprintf "Stat Requirement: %d %A (%s)" req stat desc
                | MemoryCipher (riddle, _) -> sprintf "Cipher: \"%s\"" riddle

              let choice =
                Display.promptSelectionWithHelp
                  (sprintf "[bold %s]⌹ %s[/]\n[italic %s]%s[/]\n[bold %s]%s[/]\nGuaranteed: [bold gold1]+%d Souls[/] + %d Relic(s)"
                    Theme.Yellow vault.Name Theme.Comment vault.Description Theme.Orange puzzleInfo vault.BonusSouls vault.Relics.Length)
                  [
                    "󰌆  Attempt to Unlock and Loot Vault"
                    "🏃  Step Back"
                  ]
                  None
                  (fun () -> ())
              RuntimeLog.write (sprintf "choice: %s (vault %s)" (Markup.Remove choice) vault.Name)
              if choice.Contains("Attempt") then
                match TowerSession.attemptOpenVault pt state with
                | Ok updatedState ->
                  state <- updatedState
                  AnsiConsole.MarkupLine(sprintf "\n[bold %s]The vault's ancient mechanisms yield! Spoils claimed![/]" Theme.Green)
                  Thread.Sleep(900)
                | Error err ->
                  AnsiConsole.MarkupLine(sprintf "\n[bold %s]Failed to open vault: %s[/]" Theme.Red err)
                  Thread.Sleep(1000)

            | TowerEvent.EchoDiscovered echo ->
              showMemoryEchoDialog echo

            | TowerEvent.AmbushTriggered(ambush, spawned) ->
              AnsiConsole.WriteLine()
              AnsiConsole.MarkupLine(sprintf "\n[bold red]⚠ AMBUSH SPRUNG: %s![/]" ambush.Name)
              AnsiConsole.MarkupLine(sprintf "[italic %s]%s[/]" Theme.Foreground ambush.TriggerDescription)
              AnsiConsole.MarkupLine(sprintf "[bold %s]%d monsters emerge from the surrounding colonnades![/]" Theme.Yellow spawned.Length)
              Thread.Sleep(1000)
              // The ambush pack is already adjacent: hand the encounter to the group combat path.
              handleGroup state.CurrentFloor.FloorNumber spawned

            | TowerEvent.TrapTriggered(_, msg) ->
              AnsiConsole.MarkupLine(sprintf "\n[bold red]⚠ %s[/]" msg)
              Thread.Sleep(800)

            | TowerEvent.TrapDisarmed(_, msg) ->
              AnsiConsole.MarkupLine(sprintf "\n[bold green]󰄬 %s[/]" msg)
              Thread.Sleep(700)

            | TowerEvent.ShrineActivated shrine ->
              AnsiConsole.WriteLine()
              let panel =
                Panel(Markup(sprintf "[bold %s]† SANCTUARY EMBRACE: %s[/]\n[italic white]%s[/]\n[bold %s]✦ Health & Morale fully restored! Armor repaired to maximum! Weapon restored to Pristine![/]"
                  Theme.Yellow shrine.Name shrine.BlessingDescription Theme.Green))
                  .Border(BoxBorder.Heavy)
                  .BorderStyle(Style(foreground = Nullable Theme.ColorYellow))
              AnsiConsole.Clear()
              AnsiConsole.Write(panel)
              AnsiConsole.WriteLine()
              AnsiConsole.MarkupLine(sprintf "[%s]Press any key to resume exploration...[/]" Theme.Comment)
              Console.ReadKey(true) |> ignore

            | TowerEvent.StairwayAscended nextFloorNum ->
              if state.IsStoryMode && nextFloorNum = 8 then
                EnvironmentScenes.playCrosswalkTowerTransition ()
                AnsiConsole.Clear()
                AnsiConsole.Write(
                  Rule(sprintf "[bold %s]★★★ THE INFINITE TOWER UNLOCKED ★★★[/]" Theme.Yellow)
                    .Centered()
                    .RuleStyle(Theme.StyleYellow)
                )
                AnsiConsole.MarkupLine(sprintf "\n[bold %s]You have conquered the 6 Grief Stages and freed yourself from the trauma loop.[/]" Theme.Green)
                AnsiConsole.MarkupLine(sprintf "[%s]The boundless, infinite floors of the Tower now stretch endlessly before you...[/]\n" Theme.Comment)
                AnsiConsole.MarkupLine(sprintf "[%s]Press any key to begin ascending the Infinite Tower...[/]" Theme.Yellow)
                Console.ReadKey(true) |> ignore
              elif state.IsStoryMode && nextFloorNum <= 7 then
                let nextScene = EnvironmentScenes.getSceneForEnemy (
                  match nextFloorNum with
                  | 2 -> "guilt_aspect"
                  | 3 -> "denial_aspect"
                  | 4 -> "anger_aspect"
                  | 5 -> "bargaining_aspect"
                  | 6 -> "depression_aspect"
                  | _ -> "acceptance_aspect")
                AnsiConsole.Clear()
                AnsiConsole.Write(EnvironmentScenes.renderSceneHeader nextScene)
                AnsiConsole.WriteLine()
                AnsiConsole.MarkupLine(sprintf "[bold %s]Ascended to %s! Stepping into the new stage...[/]" Theme.Yellow nextScene.LocationName)
                Thread.Sleep(1500)
              else
                AnsiConsole.Clear()
                AnsiConsole.Write(
                  Rule(sprintf "[bold %s]★★★ ASCENDED TO FLOOR %d: %s ★★★[/]" Theme.Yellow nextFloorNum state.CurrentFloor.Theme.Name)
                    .Centered()
                    .RuleStyle(Theme.StyleYellow)
                )
                AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s[/]" Theme.Foreground state.CurrentFloor.Theme.Description)
                AnsiConsole.MarkupLine(sprintf "[%s]Stepping into the vast new chamber...[/]" Theme.Comment)
                Thread.Sleep(1200)

            | _ -> ()

          // Out-of-combat recovery: 10% of max Health and Morale per step, only when no enemy is hunting you.
          if state.PlayerPosition <> posBefore && TowerSession.isSafe state then
            let heal = Math.Max(1, int (float state.Player.Health.Maximum * 0.10))
            let morale = Math.Max(1, int (float state.Player.Morale.Maximum * 0.10))
            state <-
              { state with
                  Player =
                    { state.Player with
                        Health = state.Player.Health.ApplyDelta heal
                        Morale = state.Player.Morale.ApplyDelta morale } }

          runEnemyPhase ()

        | None ->
          match key.Key with
          | ConsoleKey.Spacebar | ConsoleKey.OemPeriod | ConsoleKey.NumPad5 ->
            let safe = TowerSession.isSafe state
            let heal = if safe then Math.Max(1, int (float state.Player.Health.Maximum * 0.33)) else 0
            let morale = if safe then Math.Max(1, int (float state.Player.Morale.Maximum * 0.33)) else 0
            let armorRepair = Math.Max(1, int (float state.Player.Armor.Max * 0.25))
            let honedWeapon = WeaponCondition.repair state.Player.WeaponCondition
            let restedPlayer =
              { state.Player with
                  Health = state.Player.Health.ApplyDelta heal
                  Morale = state.Player.Morale.ApplyDelta morale
                  Meters = StatusMeters.Zero
                  Armor = { state.Player.Armor with Current = Math.Min(state.Player.Armor.Max, state.Player.Armor.Current + armorRepair) }
                  WeaponCondition = honedWeapon }
            let weaponMsg =
              if honedWeapon <> state.Player.WeaponCondition then
                sprintf ", weapon honed to %A" honedWeapon
              else ""
            let logMsg =
              if safe then
                sprintf "You rest, breathe, and tend your gear (+%d HP, +%d Morale, armor repaired%s, strain vented)." heal morale weaponMsg
              else
                sprintf "You tend your gear, but cannot recover while enemies are near (armor repaired%s, strain vented)." weaponMsg
            state <- { state with Player = restedPlayer; MessageLog = logMsg :: state.MessageLog }
            RuntimeLog.write (sprintf "action: rest (%s)" (if safe then "safe" else "contested"))
            runEnemyPhase ()

          | ConsoleKey.F1 ->
            showHelpManual ()

          | ConsoleKey.F12 when debug ->
            debugMenu ()

          | ConsoleKey.Q | ConsoleKey.Escape ->
            let confirm =
              AnsiConsole.Confirm(sprintf "[bold %s]Do you wish to retreat from the Tower and return to the main menu?[/]" Theme.Yellow, false)
            if confirm then
              sessionActive <- false

          | _ when key.KeyChar = 'c' || key.KeyChar = 'C' ->
            showCharacterSheet state

          | _ when key.KeyChar = 'x' || key.KeyChar = 'X' || key.KeyChar = ';' ->
            // Enter Inspect Mode centered on player position
            inspectCursor <- Some state.PlayerPosition

          | _ when key.KeyChar = '?' ->
            showHelpManual ()

          | _ -> ()

  /// Standard roguelike tower crawl entry point
  let runTowerCrawl
    (initialPlayer: Combatant)
    (startFloor: int)
    (onCombatDuel: int -> Combatant -> Combatant -> (CombatOutcome * Combatant))
    (onGroupCombat: int -> Combatant -> TowerEnemy list -> bool -> GroupDuelResult)
    (onDefeatAnimation: unit -> unit)
    (debug: bool)
    (forcedSeed: int option) : unit =
    runTowerCrawlWithMode initialPlayer startFloor onCombatDuel onGroupCombat onDefeatAnimation debug forcedSeed false

  /// Interactive story mode stage expedition entry point
  let runStoryCrawl
    (initialPlayer: Combatant)
    (startFloor: int)
    (onCombatDuel: int -> Combatant -> Combatant -> (CombatOutcome * Combatant))
    (onGroupCombat: int -> Combatant -> TowerEnemy list -> bool -> GroupDuelResult)
    (onDefeatAnimation: unit -> unit)
    (debug: bool)
    (forcedSeed: int option) : unit =
    runTowerCrawlWithMode initialPlayer startFloor onCombatDuel onGroupCombat onDefeatAnimation debug forcedSeed true
