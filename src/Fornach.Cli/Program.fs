namespace Fornach.Cli

open System
open Spectre.Console
open Fornach.Domain
open Fornach.Engine

module Program =

  let private printBanner () =
    AnsiConsole.Write(
      FigletText("FORNACH")
        .Centered()
        .Color(Theme.ColorPurple)
    )
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]Tactical Multi-Plane Combat Engine & Balance Workbench[/]" Theme.Yellow)
        .Centered()
        .RuleStyle(Theme.StyleCurrentLine)
    )
    AnsiConsole.WriteLine()

  let private promptSelectArchetype (title: string) : ArchetypeInfo =
    let prompt =
      SelectionPrompt<ArchetypeInfo>()
        .Title(title)
        .PageSize(10)
        .UseConverter(fun a ->
          let tierColor =
            match a.Tier with
            | Novice -> Theme.Comment
            | Adept -> Theme.Cyan
            | Master -> Theme.Yellow
          sprintf "[bold %s][[%A]][/] [bold %s]%-22s[/] ([%s]%A[/]) - [%s]%s[/]"
            tierColor a.Tier Theme.Foreground a.Name Theme.Pink a.Discipline Theme.Comment a.Description)

    prompt.AddChoices(Archetypes.allArchetypes) |> ignore
    AnsiConsole.Prompt(prompt)

  let private parseActionChoice (choice: string) : ActionIntent =
    if choice.Contains("EXECUTE FINISHER (Physical") then
      ExecuteStrike Physical
    elif choice.Contains("EXECUTE FINISHER (Mental") then
      ExecuteStrike Mental
    elif choice.Contains("Force Strike: Standard Cleave") then
      StandardAttack (ForceStrike false)
    elif choice.Contains("Force Strike: Wild Blow") then
      StandardAttack (ForceStrike true)
    elif choice.Contains("Finesse Cadence: Rapid Probing") then
      StandardAttack (FinesseCadence false)
    elif choice.Contains("Finesse Cadence: Relentless Blitz") then
      StandardAttack (FinesseCadence true)
    elif choice.Contains("Prowess Strike: Stance Pressure") then
      StandardAttack (ProwessStrike false)
    elif choice.Contains("Prowess Strike: Invitational Bait") then
      StandardAttack (ProwessStrike true)
    elif choice.Contains("Authority Decree: Imperious Command") then
      StandardAttack (AuthorityDecree false)
    elif choice.Contains("Authority Decree: Overwhelming Demand") then
      StandardAttack (AuthorityDecree true)
    elif choice.Contains("Guile Deception: Rhetorical Misdirection") then
      StandardAttack (GuileDeception false)
    elif choice.Contains("Guile Deception: Confidence Trap") then
      StandardAttack (GuileDeception true)
    elif choice.Contains("Acumen Interrogation: Procedural Pressure") then
      StandardAttack (AcumenInterrogation false)
    elif choice.Contains("Acumen Interrogation: Socratic Checkmate") then
      StandardAttack (AcumenInterrogation true)
    elif choice.Contains("Arcane Cataclysm: Elemental Blast") then
      StandardAttack (ArcaneCataclysm false)
    elif choice.Contains("Arcane Cataclysm: Overchannel") then
      StandardAttack (ArcaneCataclysm true)
    elif choice.Contains("Synaptic Glamour: Neural Static") then
      StandardAttack (SynapticGlamour false)
    elif choice.Contains("Synaptic Glamour: Mind Fracture") then
      StandardAttack (SynapticGlamour true)
    elif choice.Contains("Mirror Illusion: Phantasmal Decoys") then
      StandardAttack (MirrorIllusion false)
    elif choice.Contains("Mirror Illusion: Decoy Swarm") then
      StandardAttack (MirrorIllusion true)
    elif choice.Contains("Runic Ward Trap: Abjuration Glyph") then
      StandardAttack (RunicWardTrap false)
    elif choice.Contains("Runic Ward Trap: Anomalous Glyph") then
      StandardAttack (RunicWardTrap true)
    elif choice.Contains("Disorienting Shockwave: Balance Disruption") then
      StandardAttack (DisorientingShockwave false)
    elif choice.Contains("Disorienting Shockwave: Staggering Pulse") then
      StandardAttack (DisorientingShockwave true)
    elif choice.Contains("Shift Stance: Power Stance") then
      ShiftStance CombatStance.PowerStance
    elif choice.Contains("Shift Stance: Agility Stance") then
      ShiftStance CombatStance.AgilityStance
    elif choice.Contains("Shift Stance: Discipline Stance") then
      ShiftStance CombatStance.DisciplineStance
    elif choice.Contains("Calculated Flaw Strike") then
      StandardAttack (CalculatedFlawStrike 3)
    elif choice.Contains("Masterful Disarm") then
      StandardAttack (MasterfulDisarm 3)
    elif choice.Contains("Steady Form") then
      RecoveryAction SteadyForm
    elif choice.Contains("Center Mind") then
      RecoveryAction CenterMind
    else
      RecoveryAction SteadyForm

  let private buildActionChoices (player: Combatant) (enemy: Combatant) : string list =
    [
      if enemy.IsExecuteEligible then
        sprintf "☠️  [bold blink %s]EXECUTE FINISHER (Physical Strike)[/]" Theme.Red
        sprintf "☠️  [bold blink %s]EXECUTE FINISHER (Mental Strike)[/]" Theme.Red

      // Physical Martial Strikes
      sprintf "⚔️  [%s]Force Strike: Standard Cleave[/] (Power - Cleave vs. Fortitude)" Theme.Red
      sprintf "⚡ [bold %s]Force Strike: Wild Blow[/] (Power Gambit: +30 Recklessness, 1.5x Dmg)" Theme.Red
      sprintf "⚔️  [%s]Finesse Cadence: Rapid Probing[/] (Agility - Probing Cadence vs. Reflex)" Theme.Green
      sprintf "⚡ [bold %s]Finesse Cadence: Relentless Blitz[/] (Agility Gambit: +25 Recklessness)" Theme.Green
      sprintf "⚔️  [%s]Prowess Strike: Stance Pressure[/] (Discipline - Study Stacks vs. Poise)" Theme.Purple
      sprintf "⚡ [bold %s]Prowess Strike: Invitational Bait[/] (Discipline Gambit: +35 Recklessness)" Theme.Purple

      // Dedicated Discipline Gambits (cost Study Stacks with 0 Recklessness!)
      if player.StudyStacks >= 2 then
        sprintf "🎯 [bold %s]Calculated Flaw Strike[/] (Discipline Gambit: Spend Study Stacks for Vital Opening, 0 Recklessness)" Theme.Purple
      if player.StudyStacks >= 3 then
        sprintf "⚔️  [bold %s]Masterful Disarm[/] (Discipline Gambit: Spend Study Stacks to Degrade Opponent Weapon, 0 Recklessness)" Theme.Purple

      // Tactical Stance Shifts
      if player.Stance <> CombatStance.PowerStance then
        sprintf "↺ [bold %s]Shift Stance: Power Stance[/] (Sweeping Cleaves & Sunder Armor/Weapon)" Theme.Red
      if player.Stance <> CombatStance.AgilityStance then
        sprintf "↺ [bold %s]Shift Stance: Agility Stance[/] (Probing Cadence & Overwhelm Crits; -20%% AoO vs Flanks)" Theme.Green
      if player.Stance <> CombatStance.DisciplineStance then
        sprintf "↺ [bold %s]Shift Stance: Discipline Stance[/] (Chained Strikes, Study Stacks & Unpenalized AoO)" Theme.Purple

      // Social / Rhetorical Techniques
      sprintf "🗣️  [%s]Authority Decree: Imperious Command[/] (Presence vs. Will)" Theme.Yellow
      sprintf "⚡ [bold %s]Authority Decree: Overwhelming Demand[/] (Social Gambit: +25 Recklessness)" Theme.Yellow
      sprintf "🗣️  [%s]Guile Deception: Rhetorical Misdirection[/] (Guile vs. Insight)" Theme.Orange
      sprintf "⚡ [bold %s]Guile Deception: Confidence Trap[/] (Social Gambit: +20 Recklessness)" Theme.Orange
      sprintf "🗣️  [%s]Acumen Interrogation: Procedural Pressure[/] (Leverage vs. Composure)" Theme.Orange
      sprintf "⚡ [bold %s]Acumen Interrogation: Socratic Checkmate[/] (Social Gambit: +30 Recklessness)" Theme.Orange

      // Arcane Spellcraft (Universal Casting scaled by Mental Vector Proficiency)
      let powProf = int (Math.Round(player.GetArcaneProficiency Power * 100.0))
      let agiProf = int (Math.Round(player.GetArcaneProficiency Agility * 100.0))
      let disProf = int (Math.Round(player.GetArcaneProficiency Discipline * 100.0))

      let strainTag (prof: int) =
        if prof < 85 then sprintf " [%s](%d%% Prof - Off-School Strain)[/]" Theme.Comment prof
        else sprintf " [bold %s](%d%% Prof - Specialization)[/]" Theme.Green prof

      sprintf "✨ [%s]Arcane Cataclysm: Elemental Blast[/] (Power - Intellect vs. Resolve)%s" Theme.Pink (strainTag powProf)
      sprintf "⚡ [bold %s]Arcane Cataclysm: Overchannel[/] (Power Gambit: +35 Recklessness, Splash)%s" Theme.Pink (strainTag powProf)
      sprintf "✨ [%s]Synaptic Glamour: Neural Static[/] (Agility - Acuity vs. Intuition)%s" Theme.Purple (strainTag agiProf)
      sprintf "⚡ [bold %s]Synaptic Glamour: Mind Fracture[/] (Agility Gambit: +25 Recklessness)%s" Theme.Purple (strainTag agiProf)
      sprintf "🪞 [%s]Mirror Illusion: Phantasmal Decoys[/] (Agility - Weave Mirror Clones)%s" Theme.Purple (strainTag agiProf)
      sprintf "⚡ [bold %s]Mirror Illusion: Decoy Swarm[/] (Agility Gambit: +25 Recklessness, Extra Clones)%s" Theme.Purple (strainTag agiProf)
      sprintf "🛡️  [%s]Runic Ward Trap: Abjuration Glyph[/] (Discipline - Acumen vs. Composure, Ward)%s" Theme.Cyan (strainTag disProf)
      sprintf "⚡ [bold %s]Runic Ward Trap: Anomalous Glyph[/] (Discipline Gambit: +30 Recklessness, Heavy Ward)%s" Theme.Cyan (strainTag disProf)
      sprintf "🌀 [%s]Disorienting Shockwave: Balance Disruption[/] (Discipline - Break Posture & Tempo)%s" Theme.Orange (strainTag disProf)
      sprintf "⚡ [bold %s]Disorienting Shockwave: Staggering Pulse[/] (Discipline Gambit: +25 Recklessness, Swarm Pulse)%s" Theme.Orange (strainTag disProf)

      // Defensive Resets
      sprintf "🛡️  [%s]Steady Form[/] (Physical Reset: Drain Recklessness via Poise, build Study)" Theme.Green
      sprintf "🧠 [%s]Center Mind[/] (Mental Reset: Drain Recklessness, clear Confusion, restore Arcane Ward)" Theme.Cyan
    ]

  let private runInteractiveDuel (playerArch: ArchetypeInfo) (enemyArch: ArchetypeInfo) =
    let rng = Random()
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    let mutable player = playerArch.Factory ()
    let mutable enemy = enemyArch.Factory ()
    let mutable round = 1
    let mutable combatOver = false

    while not combatOver do
      Display.renderHUD player enemy round

      // 1. Choose Player Action
      let choices = buildActionChoices player enemy
      let choice =
        AnsiConsole.Prompt(
          SelectionPrompt<string>()
            .Title(sprintf "[bold %s]Round %d - Select Tactical Action for %s:[/]" Theme.Yellow round player.Name)
            .PageSize(10)
            .AddChoices(choices)
        )

      let playerIntent = parseActionChoice choice

      AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s executes %s...[/]" Theme.Green player.Name choice)
      let playerResult = ActionResolver.resolve roller playerIntent player enemy
      player <- playerResult.Actor
      enemy <- playerResult.Target

      Display.renderRollBreakdown choice player.Name playerResult.Contest
      playerResult.Events |> List.iter Display.logEvent

      let playerExecutedEnemy =
        playerResult.Events
        |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)

      if playerExecutedEnemy || enemy.Health.IsDepleted || enemy.Morale.IsDepleted then
        combatOver <- true
        AnsiConsole.WriteLine()
        AnsiConsole.Write(
          Rule(sprintf "[bold %s]★★★ VICTORY: %s HAS PREVAILED OVER %s! ★★★[/]" Theme.Green player.Name enemy.Name)
            .Centered()
            .RuleStyle(Theme.StyleGreen)
        )
      else
        // 2. Enemy AI Turn
        AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s evaluates the field and responds...[/]" Theme.Pink enemy.Name)
        let enemyIntent = AI.chooseIntent enemy player
        let enemyResult = ActionResolver.resolve roller enemyIntent enemy player
        enemy <- enemyResult.Actor
        player <- enemyResult.Target

        let intentDesc = sprintf "%A" enemyIntent
        Display.renderRollBreakdown intentDesc enemy.Name enemyResult.Contest
        enemyResult.Events |> List.iter Display.logEvent

        let enemyExecutedPlayer =
          enemyResult.Events
          |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)

        if enemyExecutedPlayer || player.Health.IsDepleted || player.Morale.IsDepleted then
          combatOver <- true
          AnsiConsole.WriteLine()
          AnsiConsole.Write(
            Rule(sprintf "[bold %s]☠☠☠ DEFEAT: %s HAS FALLEN TO %s! ☠☠☠[/]" Theme.Red player.Name enemy.Name)
              .Centered()
              .RuleStyle(Theme.StyleRed)
          )

      if not combatOver then
        AnsiConsole.WriteLine()
        AnsiConsole.Markup(sprintf "[%s]Press any key to proceed to the next round...[/]" Theme.Comment)
        Console.ReadKey(true) |> ignore
        round <- round + 1

    AnsiConsole.WriteLine()
    AnsiConsole.Markup(sprintf "[bold %s]Combat concluded. Press any key to return to menu...[/]" Theme.Yellow)
    Console.ReadKey(true) |> ignore

  let private runBalanceSimulator (archA: ArchetypeInfo) (archB: ArchetypeInfo) =
    let iterations =
      AnsiConsole.Prompt(
        SelectionPrompt<int>()
          .Title(sprintf "[bold %s]Select number of simulation iterations:[/]" Theme.Yellow)
          .AddChoices([ 50; 100; 250; 500; 1000 ])
      )

    let summary = Simulation.runBatch archA archB iterations
    Simulation.renderDashboard summary (archA.Factory()) (archB.Factory())

    AnsiConsole.Markup(sprintf "[bold %s]Simulation complete. Press any key to return to menu...[/]" Theme.Yellow)
    Console.ReadKey(true) |> ignore

  let private runGroupBalanceSimulator (soloArch: ArchetypeInfo) (mobArch: ArchetypeInfo) =
    let mobCount =
      AnsiConsole.Prompt(
        SelectionPrompt<int>()
          .Title(sprintf "[bold %s]Select Swarm Size (Number of Opponents fighting simultaneously):[/]" Theme.Yellow)
          .AddChoices([ 2; 3; 4; 5; 6; 8 ])
      )

    let iterations =
      AnsiConsole.Prompt(
        SelectionPrompt<int>()
          .Title(sprintf "[bold %s]Select number of simulation iterations:[/]" Theme.Yellow)
          .AddChoices([ 50; 100; 250; 500; 1000 ])
      )

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations
    Simulation.renderGroupDashboard summary (soloArch.Factory()) (mobArch.Factory())

    AnsiConsole.Markup(sprintf "[bold %s]Simulation complete. Press any key to return to menu...[/]" Theme.Yellow)
    Console.ReadKey(true) |> ignore

  let private showRoster () =
    let table = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
    table.AddColumn(TableColumn(sprintf "[bold %s]Tier[/]" Theme.Foreground)) |> ignore
    table.AddColumn(TableColumn(sprintf "[bold %s]Name[/]" Theme.Foreground)) |> ignore
    table.AddColumn(TableColumn(sprintf "[bold %s]Discipline[/]" Theme.Foreground)) |> ignore
    table.AddColumn(TableColumn(sprintf "[bold %s]HP / Morale[/]" Theme.Foreground)) |> ignore
    table.AddColumn(TableColumn(sprintf "[bold %s]Armor (Soak)[/]" Theme.Foreground)) |> ignore
    table.AddColumn(TableColumn(sprintf "[bold %s]Key Attributes[/]" Theme.Foreground)) |> ignore

    for arch in Archetypes.allArchetypes do
      let sample = arch.Factory()
      let tierColor =
        match arch.Tier with
        | Novice -> Theme.Comment
        | Adept -> Theme.Cyan
        | Master -> Theme.Yellow

      let soakPct = int (sample.Armor.AbsorptionRatio * 100.0)
      let keyStats =
        match arch.Discipline with
        | CombatMode.Physical ->
          sprintf "Force: %d, Fort: %d, Prow: %d, Poise: %d"
            (sample.GetStat Force) (sample.GetStat Fortitude) (sample.GetStat Prowess) (sample.GetStat Poise)
        | CombatMode.Social ->
          sprintf "Presence: %d, Will: %d, Leverage: %d"
            (sample.GetStat Intellect) (sample.GetStat Resolve) (sample.GetStat Acumen)
        | CombatMode.Arcane ->
          sprintf "Intellect: %d, Acuity: %d, Resolve: %d"
            (sample.GetStat Intellect) (sample.GetStat Acuity) (sample.GetStat Resolve)

      table.AddRow(
        Markup(sprintf "[bold %s]%A[/]" tierColor arch.Tier),
        Markup(sprintf "[bold %s]%s[/]" Theme.Foreground arch.Name),
        Markup(sprintf "[%s]%A[/]" Theme.Pink arch.Discipline),
        Markup(sprintf "[%s]%d[/] / [%s]%d[/]" Theme.Red sample.Health.Maximum Theme.Cyan sample.Morale.Maximum),
        Markup(sprintf "[%s]%d (%d%%)[/]" Theme.Yellow sample.Armor.Max soakPct),
        Markup(sprintf "[%s]%s[/]" Theme.Comment keyStats)
      ) |> ignore

    AnsiConsole.Clear()
    printBanner()
    AnsiConsole.Write(table)
    AnsiConsole.WriteLine()
    AnsiConsole.Markup(sprintf "[bold %s]Press any key to return to menu...[/]" Theme.Yellow)
    Console.ReadKey(true) |> ignore

  let private createCustomCombatantInteractive () : ArchetypeInfo =
    AnsiConsole.Clear()
    printBanner()
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]Custom Combatant Builder[/]" Theme.Purple)
        .LeftJustified()
        .RuleStyle(Theme.StyleCurrentLine)
    )
    AnsiConsole.WriteLine()

    let name = AnsiConsole.Ask<string>(sprintf "[%s]Enter combatant name:[/] " Theme.Foreground, "Gladiator")
    let hp = AnsiConsole.Ask<int>(sprintf "[%s]Enter Max Health (HP):[/] " Theme.Red, 2000)
    let morale = AnsiConsole.Ask<int>(sprintf "[%s]Enter Max Morale:[/] " Theme.Cyan, 2000)
    let armor = AnsiConsole.Ask<int>(sprintf "[%s]Enter Armor Durability (0-100):[/] " Theme.Yellow, 50)

    AnsiConsole.MarkupLine(sprintf "\n[bold %s]Assign Attributes (Uncapped 30–500):[/]" Theme.Yellow)
    let force = AnsiConsole.Ask<int>(sprintf "  [%s]Force (Physical Offense Power):[/] " Theme.Red, 120)
    let fort = AnsiConsole.Ask<int>(sprintf "  [%s]Fortitude (Physical Defense Power):[/] " Theme.Red, 110)
    let finesse = AnsiConsole.Ask<int>(sprintf "  [%s]Finesse (Physical Offense Agility):[/] " Theme.Green, 80)
    let reflex = AnsiConsole.Ask<int>(sprintf "  [%s]Reflex (Physical Defense Agility):[/] " Theme.Green, 80)
    let prowess = AnsiConsole.Ask<int>(sprintf "  [%s]Prowess (Martial Offense Discipline):[/] " Theme.Purple, 100)
    let poise = AnsiConsole.Ask<int>(sprintf "  [%s]Poise (Martial Defense Discipline):[/] " Theme.Purple, 100)

    let intellect = AnsiConsole.Ask<int>(sprintf "  [%s]Intellect / Presence (Mental Offense Power):[/] " Theme.Cyan, 60)
    let resolve = AnsiConsole.Ask<int>(sprintf "  [%s]Resolve / Will (Mental Defense Power):[/] " Theme.Cyan, 60)
    let acuity = AnsiConsole.Ask<int>(sprintf "  [%s]Acuity / Guile (Mental Offense Agility):[/] " Theme.Pink, 60)
    let intuition = AnsiConsole.Ask<int>(sprintf "  [%s]Intuition / Insight (Mental Defense Agility):[/] " Theme.Pink, 60)
    let acumen = AnsiConsole.Ask<int>(sprintf "  [%s]Acumen / Leverage (Mental Offense Discipline):[/] " Theme.Orange, 60)
    let composure = AnsiConsole.Ask<int>(sprintf "  [%s]Composure (Mental Defense Discipline):[/] " Theme.Orange, 60)

    let statsList = [
      Force, force; Fortitude, fort
      Finesse, finesse; Reflex, reflex
      Prowess, prowess; Poise, poise
      Intellect, intellect; Resolve, resolve
      Acuity, acuity; Intuition, intuition
      Acumen, acumen; Composure, composure
    ]

    let customFactory () =
      Archetypes.createCustom name hp morale armor statsList

    { Name = name
      Tier = Adept
      Discipline = if force + prowess >= intellect + acumen then CombatMode.Physical else CombatMode.Arcane
      Description = "Custom player-crafted combatant."
      Factory = customFactory }

  let private parseCliArgs (args: string array) : CliOptions =
    let hasFlag (f: string) = args |> Array.exists (fun a -> a = f)
    let hasGroupFlag = hasFlag "--group" || hasFlag "-g"

    let findArg (flags: string list) (defaultVal: string) =
      args
      |> Array.mapi (fun i a -> (i, a))
      |> Array.tryFind (fun (i, a) ->
        List.contains a flags && i + 1 < args.Length && not (args.[i + 1].StartsWith("-")))
      |> function
        | Some (i, _) -> args.[i + 1]
        | None -> defaultVal

    let isStandaloneSimFlag =
      args
      |> Array.tryFindIndex (fun a -> a = "-s")
      |> Option.map (fun i -> i + 1 >= args.Length || args.[i + 1].StartsWith("-"))
      |> Option.defaultValue false

    let isSim = hasFlag "--sim" || hasGroupFlag || isStandaloneSimFlag || hasFlag "-a1" || hasFlag "--solo"

    if not isSim then
      InteractiveMenu
    else
      let arch1 = findArg ["--solo"; "-s"; "-a1"; "--archetype1"] "Iron Vanguard"
      let arch2 = findArg ["--mob"; "--enemy"; "-e"; "-a2"; "--archetype2"] "Thought-Weaver"
      let itersStr = findArg ["-n"; "--iterations"] "100"
      let iters = match Int32.TryParse itersStr with true, v -> Math.Max(1, v) | _ -> 100

      let countStr = findArg ["-k"; "--count"] (if hasGroupFlag then "3" else "0")
      let mobCount = match Int32.TryParse countStr with true, v when v > 1 -> v | _ -> 0

      if mobCount > 1 || hasGroupFlag then
        let effectiveCount = Math.Max(2, mobCount)
        RunGroupSimulation(arch1, arch2, effectiveCount, iters)
      else
        RunSimulation(arch1, arch2, iters)


  [<EntryPoint>]
  let main (args: string array) =
    match parseCliArgs args with
    | RunSimulation (nameA, nameB, iters) ->
      printBanner()
      let optA = Archetypes.findByName nameA
      let optB = Archetypes.findByName nameB
      match optA, optB with
      | Some a, Some b ->
        let summary = Simulation.runBatch a b iters
        Simulation.renderDashboard summary (a.Factory()) (b.Factory())
        0
      | None, _ ->
        AnsiConsole.MarkupLine(sprintf "[bold %s]Error: Archetype '%s' not recognized.[/]" Theme.Red nameA)
        1
      | _, None ->
        AnsiConsole.MarkupLine(sprintf "[bold %s]Error: Archetype '%s' not recognized.[/]" Theme.Red nameB)
        1

    | RunGroupSimulation (soloName, mobName, mobCount, iters) ->
      printBanner()
      let optSolo = Archetypes.findByName soloName
      let optMob = Archetypes.findByName mobName
      match optSolo, optMob with
      | Some solo, Some mob ->
        let summary = Simulation.runGroupBatch solo mob mobCount iters
        Simulation.renderGroupDashboard summary (solo.Factory()) (mob.Factory())
        0
      | None, _ ->
        AnsiConsole.MarkupLine(sprintf "[bold %s]Error: Archetype '%s' not recognized.[/]" Theme.Red soloName)
        1
      | _, None ->
        AnsiConsole.MarkupLine(sprintf "[bold %s]Error: Archetype '%s' not recognized.[/]" Theme.Red mobName)
        1

    | InteractiveMenu ->
      let mutable running = true
      while running do
        AnsiConsole.Clear()
        printBanner()

        let choice =
          AnsiConsole.Prompt(
            SelectionPrompt<string>()
              .Title(sprintf "[bold %s]Select Mode:[/]" Theme.Yellow)
              .PageSize(8)
              .AddChoices([
                sprintf "⚔️   [bold %s]Interactive Duel Arena[/]" Theme.Green
                sprintf "📊  [bold %s]Monte-Carlo Balance Simulator (1 vs 1)[/]" Theme.Cyan
                sprintf "👥  [bold %s]1 vs N Encirclement Swarm Simulator[/]" Theme.Pink
                sprintf "🛠️   [bold %s]Custom Combatant Builder[/]" Theme.Orange
                sprintf "📜  [bold %s]View Archetype Roster[/]" Theme.Purple
                sprintf "🚪  [bold %s]Exit[/]" Theme.Red
              ])
          )

        if choice.Contains("Interactive Duel Arena") then
          let playerArch = promptSelectArchetype (sprintf "[bold %s]Select Player Combatant:[/]" Theme.Green)
          let enemyArch = promptSelectArchetype (sprintf "[bold %s]Select Opponent Combatant:[/]" Theme.Pink)
          runInteractiveDuel playerArch enemyArch

        elif choice.Contains("Monte-Carlo Balance Simulator (1 vs 1)") then
          let archA = promptSelectArchetype (sprintf "[bold %s]Select Combatant A:[/]" Theme.Green)
          let archB = promptSelectArchetype (sprintf "[bold %s]Select Combatant B:[/]" Theme.Pink)
          runBalanceSimulator archA archB

        elif choice.Contains("1 vs N Encirclement Swarm Simulator") then
          let soloArch = promptSelectArchetype (sprintf "[bold %s]Select Solo Champion:[/]" Theme.Green)
          let mobArch = promptSelectArchetype (sprintf "[bold %s]Select Swarm Opponent Archetype:[/]" Theme.Pink)
          runGroupBalanceSimulator soloArch mobArch

        elif choice.Contains("Custom Combatant Builder") then
          let customArch = createCustomCombatantInteractive ()
          let enemyArch = promptSelectArchetype (sprintf "[bold %s]Select Opponent to Test Against:[/]" Theme.Pink)
          runInteractiveDuel customArch enemyArch

        elif choice.Contains("View Archetype Roster") then
          showRoster ()

        elif choice.Contains("Exit") then
          running <- false

      AnsiConsole.MarkupLine(sprintf "[bold %s]Exiting Fornach Arena. Farewell![/]" Theme.Yellow)
      0
