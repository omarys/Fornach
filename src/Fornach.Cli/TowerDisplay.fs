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

  /// Renders the ASCII map viewport centered on the player position
  let renderViewport (state: TowerRunState) (viewW: int) (viewH: int) : Panel =
    let theme = state.CurrentFloor.Theme
    let halfW = viewW / 2
    let halfH = viewH / 2

    let startX = Math.Clamp(state.PlayerPosition.X - halfW, 0, Math.Max(0, state.CurrentFloor.Width - viewW))
    let startY = Math.Clamp(state.PlayerPosition.Y - halfH, 0, Math.Max(0, state.CurrentFloor.Height - viewH))
    let endX = Math.Min(state.CurrentFloor.Width - 1, startX + viewW - 1)
    let endY = Math.Min(state.CurrentFloor.Height - 1, startY + viewH - 1)

    let sb = StringBuilder()

    for y in startY .. endY do
      for x in startX .. endX do
        let pt = { X = x; Y = y }
        if pt = state.PlayerPosition then
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
      sprintf "[bold %s]%s[/] [grey]| Pos: (%d, %d)[/]"
        theme.ColorHex
        theme.Name
        state.PlayerPosition.X
        state.PlayerPosition.Y

    Panel(Markup(sb.ToString()))
      .Header(headerText)
      .Border(BoxBorder.Heavy)
      .BorderStyle(Style(foreground = Nullable (themeColor theme)))

  /// Renders the side HUD panel detailing player vitals, ascension status, and inventory
  let renderHud (state: TowerRunState) : Panel =
    let grid = Grid()
    grid.AddColumn(GridColumn().NoWrap()) |> ignore

    let p = state.Player

    // 1. Vitals
    grid.AddRow(Markup(sprintf "[bold %s]─── TACTICAL VITALS ───[/]" Theme.Yellow)) |> ignore
    grid.AddRow(Markup(Display.renderBar "Health" p.Health.Current p.Health.Maximum Theme.Red)) |> ignore
    grid.AddRow(Markup(Display.renderBar "Morale" p.Morale.Current p.Morale.Maximum Theme.Cyan)) |> ignore
    let armorText = sprintf "%-18s [%s]%d / %d[/] [%s](%d%% soak)[/]" "Armor Integrity" Theme.Comment p.Armor.Current p.Armor.Max Theme.Yellow (int (p.Armor.AbsorptionRatio * 100.0))
    grid.AddRow(Markup(armorText)) |> ignore

    // Stance & Weapon
    let stanceColor =
      match p.Stance with
      | CombatStance.PowerStance -> Theme.Red
      | CombatStance.AgilityStance -> Theme.Green
      | CombatStance.DisciplineStance -> Theme.Purple
    grid.AddRow(Markup(sprintf "%-18s [bold %s]%A[/]" "Combat Stance" stanceColor p.Stance)) |> ignore

    // Emotional Meters
    grid.AddRow(Markup(Display.renderMeter "Recklessness" p.Meters.Recklessness Theme.Red)) |> ignore
    grid.AddRow(Markup(Display.renderMeter "Overwhelm" p.Meters.Overwhelm Theme.Yellow)) |> ignore
    grid.AddRow(Markup(Display.renderMeter "Exhaustion" p.Meters.Exhaustion Theme.Purple)) |> ignore

    grid.AddRow(Markup(sprintf "[bold %s]─── ASCENSION STATUS ───[/]" Theme.Cyan)) |> ignore
    let doorState =
      match Map.tryFind state.CurrentFloor.StairwayLocation state.CurrentFloor.Tiles with
      | Some (StairwayPortal ds) -> ds
      | _ -> DoorState.Open

    match doorState with
    | DoorState.Open ->
      grid.AddRow(Markup(sprintf "Portal: [bold %s]UNLOCKED (Stairway to Floor %d Ready)[/]" Theme.Green (state.CurrentFloor.FloorNumber + 1))) |> ignore
    | DoorState.LockedByKey(_, keyName, hint) ->
      grid.AddRow(Markup(sprintf "Portal: [bold %s]SEALED (Requires: %s)[/]" Theme.Orange keyName)) |> ignore
      grid.AddRow(Markup(sprintf "        [italic %s]%s[/]" Theme.Comment hint)) |> ignore
    | DoorState.LockedByQuest(_, questTitle, req) ->
      grid.AddRow(Markup(sprintf "Portal: [bold %s]BARRED (Trial: %s)[/]" Theme.Purple questTitle)) |> ignore
      grid.AddRow(Markup(sprintf "        [italic %s]%s[/]" Theme.Comment req)) |> ignore

    // Keys & Relics
    let keysDisplay =
      if state.CollectedKeys.IsEmpty then
        sprintf "[%s]None[/]" Theme.Comment
      else
        state.CollectedKeys
        |> Seq.map (sprintf "[bold %s]%s[/]" Theme.Yellow)
        |> String.concat ", "
    grid.AddRow(Markup(sprintf "Vault Keys: %s" keysDisplay)) |> ignore

    let relicsDisplay =
      if state.InventoryItems.IsEmpty then
        sprintf "[%s]None[/]" Theme.Comment
      else
        state.InventoryItems
        |> List.map (fun it -> sprintf "[bold %s]%s[/]" Theme.Pink it.Name)
        |> String.concat ", "
    grid.AddRow(Markup(sprintf "Relics: %s" relicsDisplay)) |> ignore

    // Active Quests
    if not state.CurrentFloor.ActiveQuests.IsEmpty then
      grid.AddRow(Markup(sprintf "[bold %s]─── ACTIVE TRIALS ───[/]" Theme.Purple)) |> ignore
      for q in state.CurrentFloor.ActiveQuests do
        let status =
          if q.IsCompleted then sprintf "[bold %s][COMPLETED][/]" Theme.Green
          else sprintf "[bold %s][IN PROGRESS][/]" Theme.Yellow
        grid.AddRow(Markup(sprintf "• %s: %s" q.Title status)) |> ignore

    // Legend
    grid.AddRow(Markup(sprintf "[bold %s]─── ARCHITECTURAL LEGEND ───[/]" Theme.Comment)) |> ignore
    grid.AddRow(Markup(sprintf "[bold %s]@[/] You  [bold %s]![/] Enemy  [bold %s]?[/] NPC  [bold %s]⌹[/] Chest  [bold %s]†[/] Shrine  [bold %s]▲[/] Stairs"
      Theme.Yellow Theme.Red Theme.Cyan Theme.Yellow Theme.Green Theme.Green)) |> ignore
    grid.AddRow(Markup(sprintf "[%s]Move: W/A/S/D or Arrows | Rest: Space | Help: H | Quit: Q[/]" Theme.Comment)) |> ignore

    Panel(grid)
      .Header(sprintf "[bold %s]CHAMBER OBSERVATIONS[/]" Theme.Yellow)
      .Border(BoxBorder.Rounded)
      .BorderStyle(Theme.StyleCurrentLine)

  /// Renders recent messages in a chronicle panel
  let renderMessageLog (state: TowerRunState) (maxLines: int) : Panel =
    let recent =
      if state.MessageLog.IsEmpty then
        [ "The cold, ancient stones of the Infinite Tower resonate with memory." ]
      else
        state.MessageLog |> List.truncate maxLines

    let logText =
      recent
      |> List.map (sprintf "[%s]›[/] [bold %s]%s[/]" Theme.Pink Theme.Foreground)
      |> String.concat "\n"

    Panel(Markup(logText))
      .Header(sprintf "[bold %s]Chronicle Log[/]" Theme.Foreground)
      .Border(BoxBorder.Square)
      .BorderStyle(Theme.StyleComment)

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

    grid.AddRow(Markup(sprintf "[bold %s]THE INFINITE ROGUELIKE TOWER: EXPEDITION MANUAL[/]" Theme.Yellow)) |> ignore
    grid.AddRow(Rule().RuleStyle(Theme.StylePurple)) |> ignore
    grid.AddRow(Markup(sprintf "[bold %s]Movement & Exploration:[/]" Theme.Cyan)) |> ignore
    grid.AddRow(Markup("  [bold white]K / W / UpArrow / Keypad 8[/]    : Move North")) |> ignore
    grid.AddRow(Markup("  [bold white]J / S / DownArrow / Keypad 2[/]  : Move South")) |> ignore
    grid.AddRow(Markup("  [bold white]H / A / LeftArrow / Keypad 4[/]  : Move West")) |> ignore
    grid.AddRow(Markup("  [bold white]L / D / RightArrow / Keypad 6[/] : Move East")) |> ignore
    grid.AddRow(Markup("  [bold white]Y / U / B / N (Keypad 7/9/1/3)[/] : Diagonal Movement (NW, NE, SW, SE)")) |> ignore
    grid.AddRow(Markup("  [bold white]Spacebar / Period (.)[/]         : Stand ground / Wait a turn")) |> ignore
    grid.AddRow(Markup("  [bold white]? / F1[/]                        : Open this manual")) |> ignore
    grid.AddRow(Markup("  [bold white]Q / Escape[/]                     : Retreat to Main Menu")) |> ignore
    grid.AddRow(Markup(sprintf "\n[bold %s]Architecture & Encounters:[/]" Theme.Green)) |> ignore
    grid.AddRow(Markup("  • [bold red]![/] Guardians   : Step into their space to initiate tactical combat.")) |> ignore
    grid.AddRow(Markup("  • [bold cyan]?[/] Inhabitants : Walk into them to commune, learn lore, or receive trials.")) |> ignore
    grid.AddRow(Markup("  • [bold gold1]⌹[/] Vault Chest : Walk into chests to recover keys and powerful relics.")) |> ignore
    grid.AddRow(Markup("  • [bold green]†[/] Shrines     : Walk into ancient monoliths to recover Morale and poise.")) |> ignore
    grid.AddRow(Markup("  • [bold green]▲[/] Ascension   : Leads upward to the next floor of the Infinite Tower.")) |> ignore
    grid.AddRow(Markup("  • [bold white]∩[/] Archways    : Monumental transitions between expansive plazas.")) |> ignore
    grid.AddRow(Markup("  • [bold grey]Chasms[/]         : Endless voids; vision pierces them, but movement is blocked.")) |> ignore
    grid.AddRow(Markup(sprintf "\n[%s]Press any key to resume expedition...[/]" Theme.Comment)) |> ignore

    let panel =
      Panel(grid)
        .Border(BoxBorder.Double)
        .BorderStyle(Theme.StylePurple)

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

    while sessionActive do
      AnsiConsole.Clear()

      // Header rule
      let floorRule =
        Rule(sprintf "[bold %s]FORNACH: THE INFINITE TOWER ── FLOOR %d: %s[/]"
          state.CurrentFloor.Theme.ColorHex
          state.CurrentFloor.FloorNumber
          state.CurrentFloor.Theme.Name)
          .Centered()
          .RuleStyle(Style(foreground = Nullable (themeColor state.CurrentFloor.Theme)))
      AnsiConsole.Write(floorRule)
      AnsiConsole.MarkupLine(sprintf "[italic %s]%s[/]\n" Theme.Comment state.CurrentFloor.Theme.Description)

      // Layout: Left (Viewport 45x21), Right (HUD)
      let viewportPanel = renderViewport state 45 21
      let hudPanel = renderHud state
      let logPanel = renderMessageLog state 4

      let grid = Grid()
      grid.AddColumn(GridColumn().NoWrap()) |> ignore
      grid.AddColumn(GridColumn().NoWrap()) |> ignore
      grid.AddRow(viewportPanel, hudPanel) |> ignore

      AnsiConsole.Write(grid)
      AnsiConsole.Write(logPanel)

      // Read player command
      let key = Console.ReadKey(true)

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
                  .Title(sprintf "[bold red]A formidable foe blocks your path: %s![/]" enemy.Name)
                  .AddChoices([
                    "⚔️  Engage in Tactical Dueling Combat"
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

        | _ when key.KeyChar = '?' ->
          showHelpManual ()

        | _ -> ()
