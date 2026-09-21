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
        .Color(Color.Yellow)
    )
    AnsiConsole.Write(
      Rule("[bold yellow]Tactical Multi-Plane Combat Engine & Balance Workbench[/]")
        .Centered()
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
            | Novice -> "grey"
            | Adept -> "cyan"
            | Master -> "gold1"
          sprintf "[bold %s][[%A]][/] [bold white]%-22s[/] (%A) - %s" tierColor a.Tier a.Name a.Discipline a.Description)

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
    elif choice.Contains("Runic Ward Trap: Abjuration Glyph") then
      StandardAttack (RunicWardTrap false)
    elif choice.Contains("Runic Ward Trap: Anomalous Glyph") then
      StandardAttack (RunicWardTrap true)
    elif choice.Contains("Steady Form") then
      RecoveryAction SteadyForm
    elif choice.Contains("Center Mind") then
      RecoveryAction CenterMind
    else
      RecoveryAction SteadyForm

  let private buildActionChoices (enemy: Combatant) : string list =
    [
      if enemy.IsExecuteEligible then
        "☠️  [bold blink red]EXECUTE FINISHER (Physical Strike)[/]"
        "☠️  [bold blink red]EXECUTE FINISHER (Mental Strike)[/]"

      // Physical Martial Strikes
      "⚔️  [red]Force Strike: Standard Cleave[/] (Power - Cleave vs. Fortitude)"
      "⚡ [bold red]Force Strike: Wild Blow[/] (Power Gambit: +30 Recklessness, 1.5x Dmg)"
      "⚔️  [green]Finesse Cadence: Rapid Probing[/] (Agility - Speed vs. Reflex)"
      "⚡ [bold green]Finesse Cadence: Relentless Blitz[/] (Agility Gambit: +25 Recklessness)"
      "⚔️  [blue]Prowess Strike: Stance Pressure[/] (Discipline - Study vs. Poise)"
      "⚡ [bold blue]Prowess Strike: Invitational Bait[/] (Discipline Gambit: +35 Recklessness)"

      // Social / Rhetorical Techniques
      "🗣️  [gold1]Authority Decree: Imperious Command[/] (Presence vs. Will)"
      "⚡ [bold gold1]Authority Decree: Overwhelming Demand[/] (Social Gambit: +25 Recklessness)"
      "🗣️  [yellow]Guile Deception: Rhetorical Misdirection[/] (Guile vs. Insight)"
      "⚡ [bold yellow]Guile Deception: Confidence Trap[/] (Social Gambit: +20 Recklessness)"
      "🗣️  [orange1]Acumen Interrogation: Procedural Pressure[/] (Leverage vs. Composure)"
      "⚡ [bold orange1]Acumen Interrogation: Socratic Checkmate[/] (Social Gambit: +30 Recklessness)"

      // Arcane Techniques
      "✨ [magenta]Arcane Cataclysm: Elemental Blast[/] (Intellect vs. Resolve)"
      "⚡ [bold magenta]Arcane Cataclysm: Overchannel[/] (Arcane Gambit: +35 Recklessness)"
      "✨ [purple]Synaptic Glamour: Neural Static[/] (Acuity vs. Intuition)"
      "⚡ [bold purple]Synaptic Glamour: Mind Fracture[/] (Arcane Gambit: +25 Recklessness)"
      "✨ [cyan]Runic Ward Trap: Abjuration Glyph[/] (Acumen vs. Composure)"
      "⚡ [bold cyan]Runic Ward Trap: Anomalous Glyph[/] (Arcane Gambit: +25 Recklessness)"

      // Defensive Resets
      "🛡️  [green]Steady Form[/] (Physical Reset: Drain Recklessness via Poise, build Study)"
      "🧠 [deepskyblue1]Center Mind[/] (Mental Reset: Drain Recklessness via Composure, clear Confusion)"
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
      let choices = buildActionChoices enemy
      let choice =
        AnsiConsole.Prompt(
          SelectionPrompt<string>()
            .Title(sprintf "[bold yellow]Round %d - Select Tactical Action for %s:[/]" round player.Name)
            .PageSize(10)
            .AddChoices(choices)
        )

      let playerIntent = parseActionChoice choice

      AnsiConsole.MarkupLine(sprintf "\n[bold green]%s executes %s...[/]" player.Name choice)
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
          Rule(sprintf "[bold green]★★★ VICTORY: %s HAS PREVAILED OVER %s! ★★★[/]" player.Name enemy.Name)
            .Centered()
        )
      else
        // 2. Enemy AI Turn
        AnsiConsole.MarkupLine(sprintf "\n[bold red]%s evaluates the field and responds...[/]" enemy.Name)
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
            Rule(sprintf "[bold red]☠☠☠ DEFEAT: %s HAS FALLEN TO %s! ☠☠☠[/]" player.Name enemy.Name)
              .Centered()
          )

      if not combatOver then
        AnsiConsole.WriteLine()
        AnsiConsole.Markup("[grey]Press any key to proceed to the next round...[/]")
        Console.ReadKey(true) |> ignore
        round <- round + 1

    AnsiConsole.WriteLine()
    AnsiConsole.Markup("[bold yellow]Combat concluded. Press any key to return to menu...[/]")
    Console.ReadKey(true) |> ignore

  let private runBalanceSimulator (archA: ArchetypeInfo) (archB: ArchetypeInfo) =
    let iterations =
      AnsiConsole.Prompt(
        SelectionPrompt<int>()
          .Title("[bold yellow]Select number of simulation iterations:[/]")
          .AddChoices([ 50; 100; 250; 500; 1000 ])
      )

    let summary = Simulation.runBatch archA archB iterations
    Simulation.renderDashboard summary (archA.Factory()) (archB.Factory())

    AnsiConsole.Markup("[bold yellow]Simulation complete. Press any key to return to menu...[/]")
    Console.ReadKey(true) |> ignore

  let private showRoster () =
    let table = Table().Border(TableBorder.Rounded).BorderColor(Color.Gold1)
    table.AddColumn(TableColumn("[bold white]Tier[/]")) |> ignore
    table.AddColumn(TableColumn("[bold white]Name[/]")) |> ignore
    table.AddColumn(TableColumn("[bold white]Discipline[/]")) |> ignore
    table.AddColumn(TableColumn("[bold white]HP / Morale[/]")) |> ignore
    table.AddColumn(TableColumn("[bold white]Armor (Soak)[/]")) |> ignore
    table.AddColumn(TableColumn("[bold white]Key Attributes[/]")) |> ignore

    for arch in Archetypes.allArchetypes do
      let sample = arch.Factory()
      let tierColor =
        match arch.Tier with
        | Novice -> "grey"
        | Adept -> "cyan"
        | Master -> "gold1"

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
        Markup(sprintf "[bold white]%s[/]" arch.Name),
        Markup(sprintf "%A" arch.Discipline),
        Markup(sprintf "%d / %d" sample.Health.Maximum sample.Morale.Maximum),
        Markup(sprintf "%d (%d%%)" sample.Armor.Max soakPct),
        Markup(sprintf "[grey]%s[/]" keyStats)
      ) |> ignore

    AnsiConsole.Clear()
    printBanner()
    AnsiConsole.Write(table)
    AnsiConsole.WriteLine()
    AnsiConsole.Markup("[bold yellow]Press any key to return to menu...[/]")
    Console.ReadKey(true) |> ignore

  let private createCustomCombatantInteractive () : ArchetypeInfo =
    AnsiConsole.Clear()
    printBanner()
    AnsiConsole.Write(Rule("[bold cyan]Custom Combatant Builder[/]").LeftJustified())
    AnsiConsole.WriteLine()

    let name = AnsiConsole.Ask<string>("Enter combatant name: ", "Gladiator")
    let hp = AnsiConsole.Ask<int>("Enter Max Health (HP): ", 2000)
    let morale = AnsiConsole.Ask<int>("Enter Max Morale: ", 2000)
    let armor = AnsiConsole.Ask<int>("Enter Armor Durability (0-100): ", 50)

    AnsiConsole.MarkupLine("\n[bold yellow]Assign Attributes (Uncapped 30–500):[/]")
    let force = AnsiConsole.Ask<int>("  Force (Physical Offense Power): ", 120)
    let fort = AnsiConsole.Ask<int>("  Fortitude (Physical Defense Power): ", 110)
    let finesse = AnsiConsole.Ask<int>("  Finesse (Physical Offense Agility): ", 80)
    let reflex = AnsiConsole.Ask<int>("  Reflex (Physical Defense Agility): ", 80)
    let prowess = AnsiConsole.Ask<int>("  Prowess (Martial Offense Discipline): ", 100)
    let poise = AnsiConsole.Ask<int>("  Poise (Martial Defense Discipline): ", 100)

    let intellect = AnsiConsole.Ask<int>("  Intellect / Presence (Mental Offense Power): ", 60)
    let resolve = AnsiConsole.Ask<int>("  Resolve / Will (Mental Defense Power): ", 60)
    let acuity = AnsiConsole.Ask<int>("  Acuity / Guile (Mental Offense Agility): ", 60)
    let intuition = AnsiConsole.Ask<int>("  Intuition / Insight (Mental Defense Agility): ", 60)
    let acumen = AnsiConsole.Ask<int>("  Acumen / Leverage (Mental Offense Discipline): ", 60)
    let composure = AnsiConsole.Ask<int>("  Composure (Mental Defense Discipline): ", 60)

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
    let isSim = args |> Array.exists (fun a -> a = "--sim" || a = "-s")
    if not isSim then
      InteractiveMenu
    else
      let findArg (flag: string) (defaultVal: string) =
        match args |> Array.tryFindIndex (fun a -> a = flag) with
        | Some idx when idx + 1 < args.Length -> args.[idx + 1]
        | _ -> defaultVal

      let arch1 = findArg "-a1" (findArg "--archetype1" "Iron Vanguard")
      let arch2 = findArg "-a2" (findArg "--archetype2" "Thought-Weaver")
      let itersStr = findArg "-n" (findArg "--iterations" "100")
      let iters = match Int32.TryParse itersStr with true, v -> Math.Max(1, v) | _ -> 100
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
        AnsiConsole.MarkupLine(sprintf "[bold red]Error: Archetype '%s' not recognized.[/]" nameA)
        1
      | _, None ->
        AnsiConsole.MarkupLine(sprintf "[bold red]Error: Archetype '%s' not recognized.[/]" nameB)
        1

    | InteractiveMenu ->
      let mutable running = true
      while running do
        AnsiConsole.Clear()
        printBanner()

        let choice =
          AnsiConsole.Prompt(
            SelectionPrompt<string>()
              .Title("[bold yellow]Select Mode:[/]")
              .PageSize(8)
              .AddChoices([
                "⚔️   [bold green]Interactive Duel Arena[/]"
                "📊  [bold cyan]Monte-Carlo Balance Simulator[/]"
                "🛠️   [bold yellow]Custom Combatant Builder[/]"
                "📜  [bold grey]View Archetype Roster[/]"
                "🚪  [bold red]Exit[/]"
              ])
          )

        if choice.Contains("Interactive Duel Arena") then
          let playerArch = promptSelectArchetype "[bold green]Select Player Combatant:[/]"
          let enemyArch = promptSelectArchetype "[bold red]Select Opponent Combatant:[/]"
          runInteractiveDuel playerArch enemyArch

        elif choice.Contains("Monte-Carlo Balance Simulator") then
          let archA = promptSelectArchetype "[bold green]Select Combatant A:[/]"
          let archB = promptSelectArchetype "[bold red]Select Combatant B:[/]"
          runBalanceSimulator archA archB

        elif choice.Contains("Custom Combatant Builder") then
          let customArch = createCustomCombatantInteractive ()
          let enemyArch = promptSelectArchetype "[bold red]Select Opponent to Test Against:[/]"
          runInteractiveDuel customArch enemyArch

        elif choice.Contains("View Archetype Roster") then
          showRoster ()

        elif choice.Contains("Exit") then
          running <- false

      AnsiConsole.MarkupLine("[bold yellow]Exiting Fornach Arena. Farewell![/]")
      0
