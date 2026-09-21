namespace Fornach.Cli

open System
open Spectre.Console
open Spectre.Console.Rendering
open Fornach.Domain
open Fornach.Engine

module Display =

  let renderBar (label: string) (current: int) (maxVal: int) (color: string) =
    let safeMax = Math.Max(1, maxVal)
    let safeCurr = Math.Clamp(current, 0, safeMax)
    let pct = Math.Clamp(int (Math.Round((float safeCurr / float safeMax) * 20.0)), 0, 20)
    let filled = String('█', pct)
    let empty = String('░', 20 - pct)
    sprintf "%-18s [%s]%s%s[/] %5d / %-5d" label color filled empty safeCurr safeMax

  let renderMeter (label: string) (m: Meter) (color: string) =
    let pct = Math.Clamp(int (Math.Round((float m.Value / 100.0) * 15.0)), 0, 15)
    let filled = String('█', pct)
    let empty = String('░', 15 - pct)
    let warnColor =
      if m.Value >= 75 then "bold red"
      elif m.Value >= 40 then "yellow"
      else color
    sprintf "%-18s [%s]%s%s[/] [bold]%3d%%[/]" label warnColor filled empty m.Value

  let createCombatantPanel (c: Combatant) (borderColor: Color) (headerColor: string) =
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore

    // Health & Morale pools
    grid.AddRow(Markup(renderBar "HP (Physical)" c.Health.Current c.Health.Maximum "red")) |> ignore
    grid.AddRow(Markup(renderBar "Morale (Mental)" c.Morale.Current c.Morale.Maximum "deepskyblue1")) |> ignore

    // Armor durability & Soak
    let armorPct = int (c.Armor.AbsorptionRatio * 100.0)
    let armorText =
      if c.Armor.IsShredded then
        "[bold red]SHREDDED (0% soak)[/]"
      else
        sprintf "[grey]%d / %d[/] [yellow](%d%% soak)[/]" c.Armor.Current c.Armor.Max armorPct
    grid.AddRow(Markup(sprintf "%-18s %s" "Armor Integrity" armorText)) |> ignore
    grid.AddRow(Rule()) |> ignore

    // Shared Entropy & Momentum
    grid.AddRow(Markup(renderMeter "Recklessness" c.Meters.Recklessness "orange1")) |> ignore
    let comboText = sprintf "[cyan]%d study stacks[/] | [magenta]%d combo[/]" c.StudyStacks c.ComboTracker.ConsecutiveHits
    grid.AddRow(Markup(sprintf "%-18s %s" "Tactical Stance" comboText)) |> ignore
    grid.AddRow(Rule()) |> ignore

    // Physical Status Meters
    grid.AddRow(Markup(renderMeter "Exhaustion" c.Meters.Exhaustion "gold1")) |> ignore
    grid.AddRow(Markup(renderMeter "Overwhelm" c.Meters.Overwhelm "mediumvioletred")) |> ignore
    grid.AddRow(Markup(renderMeter "Frustration" c.Meters.Frustration "darkorange")) |> ignore
    grid.AddRow(Rule()) |> ignore

    // Mental / Social Status Meters
    grid.AddRow(Markup(renderMeter "Cognitive Fatigue" c.Meters.CognitiveFatigue "slateblue1")) |> ignore
    grid.AddRow(Markup(renderMeter "Confusion" c.Meters.Confusion "purple3")) |> ignore
    grid.AddRow(Markup(renderMeter "Provoke" c.Meters.Provoke "firebrick1")) |> ignore

    // Collapse Indicator
    match c.Collapse with
    | CollapseState.Collapsed reason ->
      grid.AddRow(Rule("[bold red on white] COLLAPSE: " + sprintf "%A" reason + " [/]")) |> ignore
      grid.AddRow(Markup("[bold blink red]*** TARGET IS EXECUTE ELIGIBLE (75% Defense Drop) ***[/]")) |> ignore
    | CollapseState.Stable -> ()

    Panel(grid)
      .Header(sprintf "[bold %s] %s [/]" headerColor c.Name)
      .Border(BoxBorder.Double)
      .BorderColor(borderColor)

  let renderHUD (player: Combatant) (enemy: Combatant) (roundNumber: int) =
    let pnlPlayer = createCombatantPanel player Color.Green "green"
    let pnlEnemy = createCombatantPanel enemy Color.Red "red"
    let columns = Columns([| pnlPlayer :> IRenderable; pnlEnemy :> IRenderable |])
    AnsiConsole.Clear()
    AnsiConsole.Write(
      Rule(sprintf "[bold yellow]Fornach Duel Arena: Round %d[/]" roundNumber)
        .LeftJustified()
    )
    AnsiConsole.Write(columns :> IRenderable)
    AnsiConsole.WriteLine()

  let renderRollBreakdown (actionName: string) (actorName: string) (contestOpt: ContestResult option) =
    match contestOpt with
    | None -> ()
    | Some contest ->
      let atk = contest.Attacker
      let def = contest.Defender
      let tierMult = ActionResolver.computeTierMultiplier contest.NetHits

      let table = Table().Border(TableBorder.Rounded).BorderColor(Color.Cyan1)
      table.AddColumn(TableColumn("[bold white]Participant[/]")) |> ignore
      table.AddColumn(TableColumn("[bold white]Stat Value[/]")) |> ignore
      table.AddColumn(TableColumn("[bold white]Dice Rolled[/]")) |> ignore
      table.AddColumn(TableColumn("[bold white]Floor Hits[/]")) |> ignore
      table.AddColumn(TableColumn("[bold white]Rolled Hits[/]")) |> ignore
      table.AddColumn(TableColumn("[bold white]Total Hits[/]")) |> ignore

      let formatRolls (rolls: int list) =
        let maxDisplay = 12
        let rollStr =
          rolls
          |> Seq.truncate maxDisplay
          |> Seq.map (fun r -> if r >= 5 then sprintf "[bold green]%d[/]" r else sprintf "[grey]%d[/]" r)
          |> String.concat ", "
        if rolls.Length > maxDisplay then sprintf "[[%s, ... (+%d)]]" rollStr (rolls.Length - maxDisplay)
        else sprintf "[[%s]]" rollStr

      table.AddRow(
        Markup(sprintf "[bold green]Attacker: %s[/]" actorName),
        Markup(sprintf "[bold]%d[/]" atk.StatValue),
        Markup(formatRolls atk.RawRolls),
        Markup(sprintf "[cyan]+%d[/]" atk.FloorHits),
        Markup(sprintf "[green]%d[/]" atk.RolledHits),
        Markup(sprintf "[bold green]%d[/]" atk.TotalHits)
      ) |> ignore

      table.AddRow(
        Markup("[bold red]Defender[/]"),
        Markup(sprintf "[bold]%d[/]" def.StatValue),
        Markup(formatRolls def.RawRolls),
        Markup(sprintf "[cyan]+%d[/]" def.FloorHits),
        Markup(sprintf "[red]%d[/]" def.RolledHits),
        Markup(sprintf "[bold red]%d[/]" def.TotalHits)
      ) |> ignore

      let outcomeText =
        if contest.IsWhiff then
          "[bold red]WHIFF (Attack Deflected / Absorbed) - 0.00x Damage[/]"
        else
          let critTag = if contest.IsCritical then " [bold yellow]★ CRITICAL STRIKE ★[/]" else ""
          sprintf "[bold green]PENETRATING HIT: %+d Net Hits[/] -> [bold yellow]Tier Multiplier: %.2fx[/]%s" contest.NetHits tierMult critTag

      let panelContent =
        Rows([|
          table :> IRenderable
          Markup(sprintf "  [bold white]Action:[/] [bold underline yellow]%s[/]" actionName) :> IRenderable
          Markup(sprintf "  [bold white]Contest Resolution:[/] %s" outcomeText) :> IRenderable
        |])

      let summaryPanel =
        Panel(panelContent)
          .Header("[bold cyan] Tactical Contest Inspection [/]")
          .Border(BoxBorder.Rounded)
          .BorderColor(Color.Cyan1)

      AnsiConsole.Write(summaryPanel :> IRenderable)
      AnsiConsole.WriteLine()

  let logEvent (evt: CombatEvent) =
    match evt with
    | CombatEvent.DamageApplied d ->
      let color = if d.Plane = Physical then "red" else "deepskyblue1"
      let critText = if d.IsCritical then " [bold yellow]** CRITICAL STRIKE **[/]" else ""
      let armorText = if d.IsArmorCompromised then " [italic orange3](Armor Compromised)[/]" else ""
      AnsiConsole.MarkupLine(sprintf "  [bold %s]>[/] Dealt [bold %s]%d %A damage[/]%s%s" color color d.Amount d.Plane critText armorText)

    | CombatEvent.DisparityTriggered (_, _, outcome) ->
      match outcome with
      | CrushingBlow bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold gold1]>> DISPARITY:[/] Crushing Blow! Target suffered [bold]+%d Exhaustion[/] and is staggered!" bonus)
      | ArterialRupture bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold red]>> DISPARITY:[/] Arterial Rupture! Target suffered [bold]+%d Overwhelm / Hemorrhage[/]!" bonus)
      | DisarmOrLimbDisable ->
        AnsiConsole.MarkupLine("  [bold orange1]>> DISPARITY:[/] Disarm & Disable! Target's weapon arm posture compromised!")
      | CognitiveRupture bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold slateblue1]>> DISPARITY:[/] Cognitive Rupture! Target's psychic ward collapsed (+%d Fatigue)!" bonus)
      | DialecticalParalysis ->
        AnsiConsole.MarkupLine("  [bold purple]>> DISPARITY:[/] Dialectical Paralysis! Target is locked in logical contradiction!")
      | StrippedCredibility bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold firebrick1]>> DISPARITY:[/] Stripped Credibility! Target's pride shattered (+%d Provoke)!" bonus)

    | CombatEvent.PassiveProcTriggered (_, _, outcome) ->
      match outcome with
      | ArmorSundered (shred, rem) ->
        AnsiConsole.MarkupLine(sprintf "  [bold orange3]>> PASSIVE:[/] Armor Sundered! Shredded [bold]%d durability[/] (Remaining: %d)" shred rem)
      | FocusShattered spike ->
        AnsiConsole.MarkupLine(sprintf "  [bold purple3]>> PASSIVE:[/] Focus Shattered! +%d Confusion applied to target!" spike)
      | VitalOpeningTriggered (bonusDmg, isPhys) ->
        let plStr = if isPhys then "Physical" else "Mental"
        AnsiConsole.MarkupLine(sprintf "  [bold yellow]>> PASSIVE:[/] Vital Opening! Dealt [bold]+%d %s bonus damage[/]!" bonusDmg plStr)
      | StudyStackGenerated total ->
        AnsiConsole.MarkupLine(sprintf "  [bold cyan]>> PASSIVE:[/] Defensive Study! Total stacks: [bold]%d[/]" total)

    | CombatEvent.GambitDeclared (_, name, cost) ->
      AnsiConsole.MarkupLine(sprintf "  [bold yellow]⚡ GAMBIT DECLARED:[/] [bold underline]%s[/] (+%d Recklessness)" name cost)

    | CombatEvent.GambitPunished (_, reason) ->
      AnsiConsole.MarkupLine(sprintf "  [bold red]✖ GAMBIT PUNISHED:[/] %s" reason)

    | CombatEvent.FormStabilized (_, drained, gained) ->
      AnsiConsole.MarkupLine(sprintf "  [bold green]✓ FORM STABILIZED:[/] Drained [bold]%d Recklessness[/], gained [bold]%d Study Stacks[/]." drained gained)

    | CombatEvent.ComboReset (_, reason) ->
      AnsiConsole.MarkupLine(sprintf "  [grey]• COMBO RESET:[/] %s" reason)

    | CombatEvent.CollapseTriggered (_, reason) ->
      AnsiConsole.MarkupLine(sprintf "  [bold red on white] ⚠ THRESHOLD SNAP: Target suffered COLLAPSE (%A)! [/]" reason)

    | CombatEvent.Executed (_, _, plane) ->
      AnsiConsole.MarkupLine(sprintf "  [bold white on red] ☠ EXECUTION DELIVERED ([%A]): Lethal blow concluded combat. ☠ [/]" plane)

    | CombatEvent.EquipmentProcTriggered (name, _, desc) ->
      AnsiConsole.MarkupLine(sprintf "  [bold skyblue1]⚙ EQUIPMENT PROC:[/] [bold]%s[/] - %s" name desc)
