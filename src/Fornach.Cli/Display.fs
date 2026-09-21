namespace Fornach.Cli

open System
open Spectre.Console
open Spectre.Console.Rendering
open Fornach.Domain
open Fornach.Engine

module Display =

  let renderBar (label: string) (current: int) (maxVal: int) (colorHex: string) =
    let safeMax = Math.Max(1, maxVal)
    let safeCurr = Math.Clamp(current, 0, safeMax)
    let pct = Math.Clamp(int (Math.Round((float safeCurr / float safeMax) * 20.0)), 0, 20)
    let filled = String('█', pct)
    let empty = String('░', 20 - pct)
    sprintf "%-18s [%s]%s[/][%s]%s[/] [bold %s]%5d[/] [%s]/[/] [%s]%-5d[/]"
      label colorHex filled Theme.CurrentLine empty Theme.Foreground safeCurr Theme.Comment Theme.Comment safeMax

  let renderMeter (label: string) (m: Meter) (colorHex: string) =
    let pct = Math.Clamp(int (Math.Round((float m.Value / 100.0) * 15.0)), 0, 15)
    let filled = String('█', pct)
    let empty = String('░', 15 - pct)
    let warnColor =
      if m.Value >= 75 then Theme.Red
      elif m.Value >= 40 then Theme.Yellow
      else colorHex
    sprintf "%-18s [%s]%s[/][%s]%s[/] [bold %s]%3d%%[/]"
      label warnColor filled Theme.CurrentLine empty Theme.Foreground m.Value

  let createCombatantPanel (c: Combatant) (borderColor: Color) (headerColor: string) =
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore

    // Active Tactical Stance & Weapon Condition
    let stanceColor =
      match c.Stance with
      | CombatStance.PowerStance -> Theme.Red
      | CombatStance.AgilityStance -> Theme.Green
      | CombatStance.DisciplineStance -> Theme.Purple
    let stanceName =
      match c.Stance with
      | CombatStance.PowerStance -> "Power Stance (Force)"
      | CombatStance.AgilityStance -> "Agility Stance (Finesse)"
      | CombatStance.DisciplineStance -> "Discipline Stance (Prowess)"
    grid.AddRow(Markup(sprintf "%-18s [bold %s]%s[/]" "Active Stance" stanceColor stanceName)) |> ignore

    let weaponCondColor, weaponCondDesc =
      match c.WeaponCondition with
      | WeaponCondition.Pristine -> Theme.Green, "Pristine (100% eff)"
      | WeaponCondition.Notched -> Theme.Yellow, "Notched (-10% dmg)"
      | WeaponCondition.Damaged -> Theme.Orange, "Damaged (-25% dmg)"
      | WeaponCondition.Broken -> Theme.Red, "Broken (-50% dmg)"
    grid.AddRow(Markup(sprintf "%-18s [bold %s]%s[/]" "Weapon Integrity" weaponCondColor weaponCondDesc)) |> ignore

    // Health & Morale pools
    grid.AddRow(Markup(renderBar "HP (Physical)" c.Health.Current c.Health.Maximum Theme.Red)) |> ignore
    grid.AddRow(Markup(renderBar "Morale (Mental)" c.Morale.Current c.Morale.Maximum Theme.Cyan)) |> ignore

    // Armor durability & Soak
    let armorPct = int (c.Armor.AbsorptionRatio * 100.0)
    let armorText =
      if c.Armor.IsShredded then
        sprintf "[bold %s]SHREDDED (0%% soak)[/]" Theme.Red
      else
        sprintf "[%s]%d / %d[/] [%s](%d%% soak)[/]" Theme.Comment c.Armor.Current c.Armor.Max Theme.Yellow armorPct
    grid.AddRow(Markup(sprintf "%-18s %s" "Armor Integrity" armorText)) |> ignore

    if c.BleedStacks > 0 || c.LimbDebuff > 0 then
      let bleedText = if c.BleedStacks > 0 then sprintf "[bold %s]%d Bleed Stacks[/] " Theme.Red c.BleedStacks else ""
      let limbText = if c.LimbDebuff > 0 then sprintf "[%s]-%d Reflex (Crippled)[/]" Theme.Orange c.LimbDebuff else ""
      grid.AddRow(Markup(sprintf "%-18s %s%s" "Debuffs" bleedText limbText)) |> ignore

    grid.AddRow(Rule().RuleStyle(Theme.StyleCurrentLine)) |> ignore

    // Shared Entropy & Momentum
    grid.AddRow(Markup(renderMeter "Recklessness" c.Meters.Recklessness Theme.Orange)) |> ignore
    let comboText = sprintf "[%s]%d study stacks[/] | [%s]%d combo[/]" Theme.Purple c.StudyStacks Theme.Pink c.ComboTracker.ConsecutiveHits
    grid.AddRow(Markup(sprintf "%-18s %s" "Tactical Stance" comboText)) |> ignore
    grid.AddRow(Rule().RuleStyle(Theme.StyleCurrentLine)) |> ignore

    // Physical Status Meters
    grid.AddRow(Markup(renderMeter "Exhaustion" c.Meters.Exhaustion Theme.Yellow)) |> ignore
    grid.AddRow(Markup(renderMeter "Overwhelm" c.Meters.Overwhelm Theme.Pink)) |> ignore
    grid.AddRow(Markup(renderMeter "Frustration" c.Meters.Frustration Theme.Orange)) |> ignore
    grid.AddRow(Rule().RuleStyle(Theme.StyleCurrentLine)) |> ignore

    // Mental / Social Status Meters
    grid.AddRow(Markup(renderMeter "Cognitive Fatigue" c.Meters.CognitiveFatigue Theme.Purple)) |> ignore
    grid.AddRow(Markup(renderMeter "Confusion" c.Meters.Confusion Theme.Comment)) |> ignore
    grid.AddRow(Markup(renderMeter "Provoke" c.Meters.Provoke Theme.Red)) |> ignore

    // Collapse Indicator
    match c.Collapse with
    | CollapseState.Collapsed reason ->
      grid.AddRow(Rule(sprintf "[bold %s on %s] COLLAPSE: %A [/]" Theme.Foreground Theme.Red reason).RuleStyle(Theme.StyleRed)) |> ignore
      grid.AddRow(Markup(sprintf "[bold blink %s]*** TARGET IS EXECUTE ELIGIBLE (75%% Defense Drop) ***[/]" Theme.Red)) |> ignore
    | CollapseState.Stable -> ()

    Panel(grid)
      .Header(sprintf "[bold %s] %s [/]" headerColor c.Name)
      .Border(BoxBorder.Rounded)
      .BorderColor(borderColor)

  let renderHUD (player: Combatant) (enemy: Combatant) (roundNumber: int) =
    let pnlPlayer = createCombatantPanel player Theme.ColorGreen Theme.Green
    let pnlEnemy = createCombatantPanel enemy Theme.ColorPink Theme.Pink
    let columns = Columns([| pnlPlayer :> IRenderable; pnlEnemy :> IRenderable |])
    AnsiConsole.Clear()
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]Fornach Duel Arena: Round %d[/]" Theme.Yellow roundNumber)
        .LeftJustified()
        .RuleStyle(Theme.StylePurple)
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

      let table = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
      table.AddColumn(TableColumn(sprintf "[bold %s]Participant[/]" Theme.Foreground)) |> ignore
      table.AddColumn(TableColumn(sprintf "[bold %s]Stat Value[/]" Theme.Foreground)) |> ignore
      table.AddColumn(TableColumn(sprintf "[bold %s]Dice Rolled[/]" Theme.Foreground)) |> ignore
      table.AddColumn(TableColumn(sprintf "[bold %s]Floor Hits[/]" Theme.Foreground)) |> ignore
      table.AddColumn(TableColumn(sprintf "[bold %s]Rolled Hits[/]" Theme.Foreground)) |> ignore
      table.AddColumn(TableColumn(sprintf "[bold %s]Total Hits[/]" Theme.Foreground)) |> ignore

      let formatRolls (rolls: int list) =
        let maxDisplay = 12
        let rollStr =
          rolls
          |> Seq.truncate maxDisplay
          |> Seq.map (fun r -> if r >= 5 then sprintf "[bold %s]%d[/]" Theme.Green r else sprintf "[%s]%d[/]" Theme.Comment r)
          |> String.concat ", "
        if rolls.Length > maxDisplay then sprintf "[[%s, ... (+%d)]]" rollStr (rolls.Length - maxDisplay)
        else sprintf "[[%s]]" rollStr

      table.AddRow(
        Markup(sprintf "[bold %s]Attacker: %s[/]" Theme.Green actorName),
        Markup(sprintf "[bold %s]%d[/]" Theme.Foreground atk.StatValue),
        Markup(formatRolls atk.RawRolls),
        Markup(sprintf "[%s]+%d[/]" Theme.Cyan atk.FloorHits),
        Markup(sprintf "[%s]%d[/]" Theme.Green atk.RolledHits),
        Markup(sprintf "[bold %s]%d[/]" Theme.Green atk.TotalHits)
      ) |> ignore

      table.AddRow(
        Markup(sprintf "[bold %s]Defender[/]" Theme.Pink),
        Markup(sprintf "[bold %s]%d[/]" Theme.Foreground def.StatValue),
        Markup(formatRolls def.RawRolls),
        Markup(sprintf "[%s]+%d[/]" Theme.Cyan def.FloorHits),
        Markup(sprintf "[%s]%d[/]" Theme.Pink def.RolledHits),
        Markup(sprintf "[bold %s]%d[/]" Theme.Pink def.TotalHits)
      ) |> ignore

      let outcomeText =
        if contest.IsWhiff then
          sprintf "[bold %s]WHIFF (Attack Deflected / Absorbed) - 0.00x Damage[/]" Theme.Red
        else
          let critTag = if contest.IsCritical then sprintf " [bold %s]★ CRITICAL STRIKE ★[/]" Theme.Yellow else ""
          sprintf "[bold %s]PENETRATING HIT: %+d Net Hits[/] -> [bold %s]Tier Multiplier: %.2fx[/]%s"
            Theme.Green contest.NetHits Theme.Yellow tierMult critTag

      let rowsList = ResizeArray<IRenderable>()
      rowsList.Add(table)
      rowsList.Add(Markup(sprintf "  [bold %s]Action:[/] [bold underline %s]%s[/]" Theme.Foreground Theme.Yellow actionName))
      if contest.EncirclementPenalty > 0 then
        rowsList.Add(Markup(sprintf "  [bold %s]Encirclement Flank Penalty:[/] [bold %s]-%d Defense Hits[/]" Theme.Orange Theme.Red contest.EncirclementPenalty))
      rowsList.Add(Markup(sprintf "  [bold %s]Contest Resolution:[/] %s" Theme.Foreground outcomeText))

      let panelContent = Rows(rowsList |> Seq.toArray)

      let summaryPanel =
        Panel(panelContent)
          .Header(sprintf "[bold %s] Tactical Contest Inspection [/]" Theme.Purple)
          .Border(BoxBorder.Rounded)
          .BorderColor(Theme.ColorCurrentLine)

      AnsiConsole.Write(summaryPanel :> IRenderable)
      AnsiConsole.WriteLine()

  let logEvent (evt: CombatEvent) =
    match evt with
    | CombatEvent.DamageApplied d ->
      let color = if d.Plane = Physical then Theme.Red else Theme.Cyan
      let critText = if d.IsCritical then sprintf " [bold %s]** CRITICAL STRIKE **[/]" Theme.Yellow else ""
      let armorText = if d.IsArmorCompromised then sprintf " [italic %s](Armor Compromised)[/]" Theme.Orange else ""
      AnsiConsole.MarkupLine(sprintf "  [bold %s]>[/] Dealt [bold %s]%d %A damage[/]%s%s" color color d.Amount d.Plane critText armorText)

    | CombatEvent.DisparityTriggered (_, _, outcome) ->
      match outcome with
      | CrushingBlow bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]>> DISPARITY:[/] Crushing Blow! Target suffered [bold %s]+%d Exhaustion[/] and is staggered!" Theme.Yellow Theme.Yellow bonus)
      | ArterialRupture bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]>> DISPARITY:[/] Arterial Rupture! Target suffered [bold %s]+%d Overwhelm / Hemorrhage[/]!" Theme.Pink Theme.Pink bonus)
      | DisarmOrLimbDisable ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]>> DISPARITY:[/] Disarm & Disable! Target's weapon arm posture compromised!" Theme.Orange)
      | CognitiveRupture bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]>> DISPARITY:[/] Cognitive Rupture! Target's psychic ward collapsed (+%d Fatigue)!" Theme.Purple bonus)
      | DialecticalParalysis ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]>> DISPARITY:[/] Dialectical Paralysis! Target is locked in logical contradiction!" Theme.Purple)
      | StrippedCredibility bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]>> DISPARITY:[/] Stripped Credibility! Target's pride shattered (+%d Provoke)!" Theme.Red bonus)

    | CombatEvent.PassiveProcTriggered (_, _, outcome) ->
      match outcome with
      | ArmorSundered (shred, rem) ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]>> PASSIVE:[/] Armor Sundered! Shredded [bold %s]%d durability[/] (Remaining: %d)" Theme.Orange Theme.Orange shred rem)
      | FocusShattered spike ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]>> PASSIVE:[/] Focus Shattered! +%d Confusion applied to target!" Theme.Comment spike)
      | VitalOpeningTriggered (bonusDmg, isPhys) ->
        let plStr = if isPhys then "Physical" else "Mental"
        AnsiConsole.MarkupLine(sprintf "  [bold %s]>> PASSIVE:[/] Vital Opening! Dealt [bold %s]+%d %s bonus damage[/]!" Theme.Yellow Theme.Yellow bonusDmg plStr)
      | StudyStackGenerated total ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]>> PASSIVE:[/] Defensive Study! Total stacks: [bold %s]%d[/]" Theme.Purple Theme.Purple total)

    | CombatEvent.GambitDeclared (_, name, cost) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ GAMBIT DECLARED:[/] [bold underline %s]%s[/] (+%d Recklessness)" Theme.Orange Theme.Orange name cost)

    | CombatEvent.GambitPunished (_, reason) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]✖ GAMBIT PUNISHED:[/] %s" Theme.Red reason)

    | CombatEvent.FormStabilized (_, drained, gained) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]✓ FORM STABILIZED:[/] Drained [bold %s]%d Recklessness[/], gained [bold %s]%d Study Stacks[/]." Theme.Green Theme.Green drained Theme.Purple gained)

    | CombatEvent.ComboReset (_, reason) ->
      AnsiConsole.MarkupLine(sprintf "  [%s]• COMBO RESET:[/] %s" Theme.Comment reason)

    | CombatEvent.CollapseTriggered (_, reason) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s on %s] ⚠ THRESHOLD SNAP: Target suffered COLLAPSE (%A)! [/]" Theme.Foreground Theme.Red reason)

    | CombatEvent.Executed (_, _, plane) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s on %s] ☠ EXECUTION DELIVERED ([[%A]]): Lethal blow concluded combat. ☠ [/]" Theme.Foreground Theme.Red plane)

    | CombatEvent.EquipmentProcTriggered (name, _, desc) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚙ EQUIPMENT PROC:[/] [bold %s]%s[/] - %s" Theme.Cyan Theme.Foreground name desc)

    | CombatEvent.WeaponDegraded (_, newCond) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚙ WEAPON DEGRADED:[/] Weapon integrity dropped to [bold %s]%A[/]!" Theme.Red Theme.Red newCond)

    | CombatEvent.StanceShifted (_, oldS, newS) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]↺ STANCE SHIFTED:[/] Shifted from %A to [bold underline %s]%A[/]." Theme.Purple oldS Theme.Purple newS)

    | CombatEvent.RiposteExecuted (_, _, dmg) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚔ RIPOSTE:[/] Stance predicted! Reactive counter dealt [bold %s]%d damage[/]!" Theme.Purple Theme.Red dmg)

    | CombatEvent.DisarmExecuted (_, _, reason) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ DISARM:[/] %s" Theme.Orange reason)

    | CombatEvent.BleedTicked (_, dmg, rem) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🩸 BLEED TICK:[/] Took [bold %s]%d somatic bleed damage[/] (%d stacks remaining)." Theme.Red Theme.Red dmg rem)

    | CombatEvent.BleedApplied (_, added, total) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🩸 BLEED INFLICTED:[/] +%d bleed stacks applied (Total: [bold %s]%d[/])." Theme.Red added Theme.Red total)

    | CombatEvent.LimbDisabled (_, pen) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🩹 LIMB CRIPPLED:[/] Tendon severed! Target suffered [bold %s]-%d Reflex[/]." Theme.Orange Theme.Orange pen)

    | CombatEvent.DisciplineGambitExecuted (_, name, spent) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🎯 DISCIPLINE GAMBIT:[/] [bold underline %s]%s[/] (Expended %d Study Stacks, 0 Recklessness)." Theme.Purple Theme.Purple name spent)

    | CombatEvent.EncirclementPenalized (_, priorDefenses, penalty) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🛡️ ENCIRCLED:[/] Flanked by attack #%d! Disparity penalty: [bold %s]-%d Defense Hits[/]!" Theme.Orange (priorDefenses + 1) Theme.Red penalty)

    | CombatEvent.AttackOfOpportunityTriggered (_, _, vectorName, dmg, disrupted) ->
      let statusStr = if disrupted then sprintf " [bold %s](Flank Intercepted & Attack Defused!)[/]" Theme.Green else ""
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ ATTACK OF OPPORTUNITY (%s):[/] Flank attempt punished! Dealt [bold %s]%d reactive damage[/]!%s" Theme.Cyan vectorName Theme.Red dmg statusStr)

    | CombatEvent.CleaveExecuted (_, _, dmg, reck) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚔ CLEAVE:[/] Wide sweeping strike cleaved adjacent target for [bold %s]%d damage[/]! (Disparity Recklessness: [bold %s]+%d[/])" Theme.Red Theme.Red dmg Theme.Orange reck)

    | CombatEvent.StrikeChained (_, _, step, dmg) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ CHAIN FLOW #%d:[/] Disciplined cadence flowed into adjacent target for [bold %s]%d damage[/]! (0 Recklessness)" Theme.Purple step Theme.Red dmg)

