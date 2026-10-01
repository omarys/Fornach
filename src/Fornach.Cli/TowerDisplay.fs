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
            sprintf "Heavy iron vault portal sealed tight. Requires: [%s]. Hint: %s" keyName hint,
            Some (sprintf "[bold %s]LOCKED:[/] Collect key to unlock." Theme.Orange)
          | LockedByQuest(_, questTitle, req) ->
            "⮝", "Barred Ascension Portal",
            sprintf "Ascension barred by architectural trial: [%s]. Requirement: %s" questTitle req,
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
            Some (sprintf "[bold %s]! HOSTILE GUARDIAN: %s[/] [grey]| Lv.%d %s | HP: %d/%d | Morale: %d/%d | %s: %s[/]"
              Theme.Red e.Name c.Level c.Class.Name c.Health.Current c.Health.Maximum c.Morale.Current c.Morale.Maximum (if c.Plane = Mental then "Form" else "Stance") stanceOrForm)
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
    grid.AddRow(Markup(sprintf "[italic %s]󰌌 Pan Reticle: Arrows / Vim (h/j/k/l/y/u/b/n)  •  Exit Inspect: Esc / x / Enter / Space[/]" Theme.Comment)) |> ignore

    Panel(grid)
      .Header(sprintf "[bold %s]󰍹 TILE & HAZARD INSPECTOR[/]" Theme.Yellow)
      .Border(BoxBorder.Square)
      .BorderStyle(Theme.StyleYellow)
      .Expand()

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

  /// Renders the side HUD panel detailing player vitals, ascension status, and inventory
  let renderHud (state: TowerRunState) : Panel =
    let grid = Grid()
    grid.AddColumn(GridColumn().NoWrap()) |> ignore

    let p = state.Player

    // 1. Vitals
    grid.AddRow(Markup(sprintf "[bold %s]─── 󰓥 TACTICAL VITALS ───[/]" Theme.Yellow)) |> ignore
    grid.AddRow(Markup(Display.renderBar "󰋑 Health" p.Health.Current p.Health.Maximum Theme.Red)) |> ignore
    grid.AddRow(Markup(Display.renderBar "󰧑 Morale" p.Morale.Current p.Morale.Maximum Theme.Cyan)) |> ignore
    let armorText = sprintf "%-22s [%s]%d / %d[/] [%s](%d%% soak)[/]" " Armor Integrity" Theme.Comment p.Armor.Current p.Armor.Max Theme.Yellow (int (p.Armor.AbsorptionRatio * 100.0))
    grid.AddRow(Markup(armorText)) |> ignore

    // Stance or Complex Form
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

    // Active Quests
    if not state.CurrentFloor.ActiveQuests.IsEmpty then
      grid.AddRow(Markup(sprintf "[bold %s]─── 󱁕 ACTIVE TRIALS ───[/]" Theme.Purple)) |> ignore
      for q in state.CurrentFloor.ActiveQuests do
        let status =
          if q.IsCompleted then sprintf "[bold %s][COMPLETED][/]" Theme.Green
          else sprintf "[bold %s][IN PROGRESS][/]" Theme.Yellow
        grid.AddRow(Markup(sprintf "• %s: %s" q.Title status)) |> ignore

    // Legend
    grid.AddRow(Markup(sprintf "[bold %s]─── 󰋜 ARCHITECTURAL LEGEND ───[/]" Theme.Comment)) |> ignore
    grid.AddRow(Markup(sprintf "[bold %s]@[/] You  [bold %s]![/] Enemy  [bold %s]?[/] NPC  [bold %s]⌹[/] Chest  [bold %s]†[/] Shrine  [bold %s]▲[/] Stairs  [bold #50fa7b]≈[/] Hazard"
      Theme.Yellow Theme.Red Theme.Cyan Theme.Yellow Theme.Green Theme.Green)) |> ignore
    grid.AddRow(Markup(sprintf "[%s]󰌌 Keys: Arrows/Vim: Move | x/; : Inspect Tiles | Space: Wait | ?: Help | Q: Quit[/]" Theme.Comment)) |> ignore

    Panel(grid)
      .Header(sprintf "[bold %s]󰍹 CHAMBER OBSERVATIONS[/]" Theme.Yellow)
      .Border(BoxBorder.Rounded)
      .BorderStyle(Theme.StyleCurrentLine)
      .Expand()

  /// Renders recent messages in a chronicle panel
  let renderMessageLog (state: TowerRunState) (maxLines: int) : Panel =
    let recent =
      if state.MessageLog.IsEmpty then
        [ "The cold, ancient stones of the Infinite Tower resonate with memory." ]
      else
        state.MessageLog |> List.truncate maxLines

    let logText =
      recent
      |> List.map (sprintf "[%s]󰁔[/] [bold %s]%s[/]" Theme.Pink Theme.Foreground)
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

  /// Shows the keyboard control manual
  let showHelpManual () =
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore

    grid.AddRow(Markup(sprintf "[bold %s]󰒋 THE INFINITE ROGUELIKE TOWER: EXPEDITION MANUAL[/]" Theme.Yellow)) |> ignore
    grid.AddRow(Rule().RuleStyle(Theme.StylePurple)) |> ignore
    grid.AddRow(Markup(sprintf "[bold %s]󰌌 Movement & Exploration:[/]" Theme.Cyan)) |> ignore
    grid.AddRow(Markup("  [bold white]K / W / UpArrow / Keypad 8[/]    : Move North")) |> ignore
    grid.AddRow(Markup("  [bold white]J / S / DownArrow / Keypad 2[/]  : Move South")) |> ignore
    grid.AddRow(Markup("  [bold white]H / A / LeftArrow / Keypad 4[/]  : Move West")) |> ignore
    grid.AddRow(Markup("  [bold white]L / D / RightArrow / Keypad 6[/] : Move East")) |> ignore
    grid.AddRow(Markup("  [bold white]Y / U / B / N (Keypad 7/9/1/3)[/] : Diagonal Movement (NW, NE, SW, SE)")) |> ignore
    grid.AddRow(Markup("  [bold white]X / Semicolon (;)[/]            : Inspect / Look mode (examine tiles & hazards in vision)")) |> ignore
    grid.AddRow(Markup("  [bold white]Spacebar / Period (.)[/]         : Stand ground / Wait a turn")) |> ignore
    grid.AddRow(Markup("  [bold white]? / F1[/]                        : Open this manual")) |> ignore
    grid.AddRow(Markup("  [bold white]Q / Escape[/]                     : Retreat to Main Menu")) |> ignore
    grid.AddRow(Markup(sprintf "\n[bold %s]󰞁 Architecture & Encounters:[/]" Theme.Green)) |> ignore
    grid.AddRow(Markup("  • [bold red]![/] Guardians   : Step into their space to initiate tactical combat.")) |> ignore
    grid.AddRow(Markup("  • [bold cyan]?[/] Inhabitants : Walk into them to commune, learn lore, or receive trials.")) |> ignore
    grid.AddRow(Markup("  • [bold gold1]⌹[/] Vault Chest : Walk into chests to recover keys and powerful relics.")) |> ignore
    grid.AddRow(Markup("  • [bold green]†[/] Shrines     : Walk into ancient monoliths to recover Morale and poise.")) |> ignore
    grid.AddRow(Markup("  • [bold green]▲[/] Ascension   : Leads upward to the next floor of the Infinite Tower.")) |> ignore
    grid.AddRow(Markup("  • [bold white]∩[/] Archways    : Monumental transitions between expansive plazas.")) |> ignore
    grid.AddRow(Markup("  • [bold grey]Chasms[/]         : Endless voids; vision pierces them, but movement is blocked.")) |> ignore
    grid.AddRow(Markup(sprintf "\n[bold %s]⚠ Environmental Hazards:[/]" Theme.Red)) |> ignore
    grid.AddRow(Markup("  • [bold #50fa7b]≈[/] Acid Slag   : Corrosive runoff; dissolves -15 Armor durability upon entry!")) |> ignore
    grid.AddRow(Markup("  • [bold red]≈[/] Lava Rift   : Molten fissures; burns player for -15 direct Health!")) |> ignore
    grid.AddRow(Markup("  • [bold cyan]≋[/] Deep Water  : Murky currents; exhausts player with +15 Exhaustion!")) |> ignore
    grid.AddRow(Markup("  • [bold pink]❀[/] Spores     : Serene blossoms; resets accumulated Recklessness to 0!")) |> ignore
    grid.AddRow(Markup(sprintf "\n[%s]Press any key to resume expedition...[/]" Theme.Comment)) |> ignore

    let panel =
      Panel(grid)
        .Border(BoxBorder.Double)
        .BorderStyle(Theme.StylePurple)
        .Expand()

    AnsiConsole.Clear()
    AnsiConsole.Write(panel)
    Console.ReadKey(true) |> ignore

  /// Main interactive turn loop for Tower dungeon crawling
  let runTowerCrawl
    (initialPlayer: Combatant)
    (startFloor: int)
    (onCombatDuel: Combatant -> Combatant -> (CombatOutcome * Combatant))
    (onDefeatAnimation: unit -> unit) : unit =

    let rng = Random()
    let seed = rng.Next(10000, 99999)
    let mutable state = TowerSession.initSession initialPlayer seed startFloor
    let mutable sessionActive = true
    let mutable inspectCursor : Point option = None

    while sessionActive do
      AnsiConsole.Clear()

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
      let hudW = 58
      let availableW = termW - hudW - 4
      let viewW = Math.Clamp(availableW, 45, state.CurrentFloor.Width)

      // Vertical space: header (~4 rows) + log/inspector (4-8 rows) + borders/margins (~6 rows)
      let logLines = if termH >= 45 then 8 elif termH >= 35 then 6 else 4
      let availableH = termH - 4 - logLines - 6
      let viewH = Math.Clamp(availableH, 21, state.CurrentFloor.Height)

      // Layout: Left (Viewport), Right (HUD), Bottom (Log or Inspector)
      let viewportPanel = renderViewportWithCursor state inspectCursor viewW viewH
      let hudPanel = renderHud state
      let bottomPanel =
        match inspectCursor with
        | Some curPt -> renderInspectionPanel state curPt
        | None -> renderMessageLog state logLines

      let grid = Grid()
      grid.AddColumn(GridColumn()) |> ignore
      grid.AddColumn(GridColumn().NoWrap()) |> ignore
      grid.AddRow(viewportPanel, hudPanel) |> ignore
      grid.Expand <- true

      AnsiConsole.Write(grid)
      AnsiConsole.Write(bottomPanel)

      // Read player command
      let key = Console.ReadKey(true)

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
          let nextState, events = TowerSession.stepPlayer dir state
          state <- nextState

          for ev in events do
            match ev with
            | TowerEvent.CombatTriggered enemy ->
              AnsiConsole.WriteLine()
              let choice =
                Display.promptWithVim(
                  SelectionPrompt<string>()
                    .Title(sprintf "[bold red]󰈸 A formidable foe blocks your path: %s![/]" enemy.Name)
                    .AddChoices([
                      "󰓥  Engage in Tactical Dueling Combat"
                      "⚡  Quick Resolve (Overcome with Standard Prowess)"
                      "🏃  Step Back / Disengage"
                    ])
                )

              if choice.Contains("Engage in Tactical") then
                let outcome, updatedPlayer = onCombatDuel state.Player enemy.Combatant
                if outcome = CombatOutcome.PlayerVictorious then
                  state <- { state with Player = updatedPlayer }
                  state <- TowerSession.resolveEnemyDefeat enemy.Id state
                  AnsiConsole.MarkupLine(sprintf "\n[bold %s]Guardian vanquished! You reclaim control of the floor.[/]" Theme.Green)
                  Thread.Sleep(900)
                else
                  onDefeatAnimation()
                  // Player defeat: reset at floor spawn with restored vitals
                  let restoredPlayer =
                    { state.Player with
                        Health = state.Player.Health.ApplyDelta 100
                        Morale = state.Player.Morale.ApplyDelta 100 }
                  state <-
                    { state with
                        Player = restoredPlayer
                        PlayerPosition = state.CurrentFloor.SpawnLocation
                        MessageLog = "Rewound through the misty intersection. You awaken at the chamber entrance." :: state.MessageLog }
                  state <- TowerSession.updateFov state

              elif choice.Contains("Quick Resolve") then
                let strainedPlayer =
                  { state.Player with
                      Health = state.Player.Health.ApplyDelta -15
                      Morale = state.Player.Morale.ApplyDelta -10 }
                state <- { state with Player = strainedPlayer }
                state <- TowerSession.resolveEnemyDefeat enemy.Id state
                AnsiConsole.MarkupLine(sprintf "\n[bold %s]With steady resolve, you shatter the guardian's stance![/]" Theme.Green)
                Thread.Sleep(700)

              else
                // Disengage
                ()

            | TowerEvent.NpcInteracted(npc, _) ->
              showNpcDialog npc

            | TowerEvent.StairwayAscended nextFloorNum ->
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

        | None ->
          match key.Key with
          | ConsoleKey.Spacebar | ConsoleKey.OemPeriod | ConsoleKey.NumPad5 ->
            let logMsg = "You steady your stance and observe the ambient flow of the chamber."
            state <- { state with MessageLog = logMsg :: state.MessageLog }

          | ConsoleKey.F1 ->
            showHelpManual ()

          | ConsoleKey.Q | ConsoleKey.Escape ->
            let confirm =
              AnsiConsole.Confirm(sprintf "[bold %s]Do you wish to retreat from the Tower and return to the main menu?[/]" Theme.Yellow, false)
            if confirm then
              sessionActive <- false

          | _ when key.KeyChar = 'x' || key.KeyChar = 'X' || key.KeyChar = ';' ->
            // Enter Inspect Mode centered on player position
            inspectCursor <- Some state.PlayerPosition

          | _ when key.KeyChar = '?' ->
            showHelpManual ()

          | _ -> ()
