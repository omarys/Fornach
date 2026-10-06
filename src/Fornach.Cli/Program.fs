namespace Fornach.Cli

open System
open Spectre.Console
open Fornach.Domain
open Fornach.Engine
open Fornach.Story

module Program =

  let private printBanner () =
    let termWidth = Math.Max(80, AnsiConsole.Profile.Width)
    let termHeight =
      if Console.IsOutputRedirected || Console.WindowHeight <= 0 then 30
      else Console.WindowHeight

    AnsiConsole.Write(
      FigletText("FORNACH")
        .Centered()
        .Color(Theme.ColorPurple)
    )

    let telemetryGrid = Grid()
    telemetryGrid.AddColumn(GridColumn().Centered()) |> ignore
    telemetryGrid.AddColumn(GridColumn().Centered()) |> ignore
    telemetryGrid.AddColumn(GridColumn().Centered()) |> ignore
    telemetryGrid.AddColumn(GridColumn().Centered()) |> ignore
    telemetryGrid.AddRow(
      Markup(sprintf "[grey]󰍹 Screen:[/] [bold %s]%dx%d[/]" Theme.Cyan termWidth termHeight),
      Markup(sprintf "[grey]󰘚 Engine:[/] [bold %s]v0.3.0 Turbo[/]" Theme.Green),
      Markup(sprintf "[grey]󰀝 Roster:[/] [bold %s]%d Archetypes[/]" Theme.Yellow Archetypes.allArchetypes.Length),
      Markup(sprintf "[grey]󰌌 Input:[/] [bold %s]Vim (j/k/h/l) + Pad[/]" Theme.Pink)
    ) |> ignore
    telemetryGrid.Expand <- true

    let telemetryPanel =
      Panel(telemetryGrid)
        .Border(BoxBorder.Rounded)
        .BorderStyle(Theme.StyleCurrentLine)
        .Header(sprintf "[bold %s] 󰒋 COMMAND COCKPIT & SYSTEM TELEMETRY [/]" Theme.Yellow)
        .Expand()

    AnsiConsole.Write(telemetryPanel)
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
            | Veteran -> Theme.Cyan
            | Master -> Theme.Yellow
            | GrandMaster -> Theme.Purple
          sprintf "[bold %s][[%A]][/] [bold %s]󰀝 %-22s[/] ([%s]%A[/]) ── [%s]%s[/]"
            tierColor a.Tier Theme.Foreground a.Name Theme.Pink a.Discipline Theme.Comment a.Description)

    prompt.AddChoices(Archetypes.allArchetypes) |> ignore
    Display.promptWithVim prompt

  let parseActionChoice (choice: string) : ActionIntent =
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
    elif choice.Contains("Trauma: Denial Phase Shift") then
      StandardAttack (TraumaAttack DenialPhaseShift)
    elif choice.Contains("Trauma: Basalt Eruption") then
      StandardAttack (TraumaAttack BasaltEruption)
    elif choice.Contains("Trauma: Coercive Bargain") then
      StandardAttack (TraumaAttack CoerciveBargain)
    elif choice.Contains("Trauma: Apathy Doldrums") then
      StandardAttack (TraumaAttack ApathyDoldrums)
    elif choice.Contains("Trauma: Serene Resolution") then
      StandardAttack (TraumaAttack SereneResolution)
    elif choice.Contains("Thread Form: Resonance Spike") then
      ThreadComplexForm ComplexForm.ResonanceSpike
    elif choice.Contains("Thread Form: Phantasmal Diffusion") then
      ThreadComplexForm ComplexForm.PhantasmalDiffusion
    elif choice.Contains("Thread Form: Aegis Lattice") then
      ThreadComplexForm ComplexForm.AegisLattice
    elif choice.Contains("Steady Form") then
      RecoveryAction SteadyForm
    elif choice.Contains("Center Mind") then
      RecoveryAction CenterMind
    elif choice.Contains("Steady Breathing") then
      RecoveryAction SteadyBreathing
    else
      RecoveryAction SteadyForm

  let buildActionChoices (player: Combatant) (enemy: Combatant) : string list =
    [
      // Finisher (only for the player's operative discipline)
      if enemy.IsExecuteEligible then
        if player.Plane = Physical then
          sprintf "☠️  [bold blink %s]EXECUTE FINISHER (Physical Strike)[/]" Theme.Red
        else
          sprintf "☠️  [bold blink %s]EXECUTE FINISHER (Mental Strike)[/]" Theme.Red

      // Psychological Trauma Gambits (only when relevant to boss encounter or manifestation)
      if enemy.Name.Contains("Acceptance") || player.Name.Contains("Acceptance") then
        sprintf "🕊️  [bold %s]Trauma: Serene Resolution[/] (Pacifist Release: Drain Recklessness, restore Morale)" Theme.Cyan
      elif player.Name.Contains("Denial") then
        sprintf "🌫️  [bold %s]Trauma: Denial Phase Shift[/] (Agility Gambit: Conjure Mirror Clone, inflict Confusion)" Theme.Green
      elif player.Name.Contains("Anger") then
        sprintf "🌋 [bold %s]Trauma: Basalt Eruption[/] (Power Gambit: Massive Damage, shred 25 Armor)" Theme.Red
      elif player.Name.Contains("Bargaining") then
        sprintf "⚖️  [bold %s]Trauma: Coercive Bargain[/] (Discipline Gambit: Steal 35 Morale to restore Health)" Theme.Purple
      elif player.Name.Contains("Depression") then
        sprintf "⚓ [bold %s]Trauma: Apathy Doldrums[/] (Discipline Gambit: Inflict 40 Cognitive Fatigue)" Theme.Comment

      if player.Plane = Physical then
        // Physical Martial Strikes grouped by vector
        let powerStrikes = [
          sprintf "⚔️  [%s]Force Strike: Standard Cleave[/] (Power - Cleave vs. Fortitude)" Theme.Red
          sprintf "⚡ [bold %s]Force Strike: Wild Blow[/] (Power Gambit: +30 Recklessness, 1.5x Dmg)" Theme.Red
        ]

        let agilityStrikes = [
          sprintf "⚔️  [%s]Finesse Cadence: Rapid Probing[/] (Agility - Probing Cadence vs. Reflex)" Theme.Green
          sprintf "⚡ [bold %s]Finesse Cadence: Relentless Blitz[/] (Agility Gambit: +25 Recklessness)" Theme.Green
        ]

        let disciplineStrikes = [
          sprintf "⚔️  [%s]Prowess Strike: Stance Pressure[/] (Discipline - Study Stacks vs. Poise)" Theme.Purple
          sprintf "⚡ [bold %s]Prowess Strike: Invitational Bait[/] (Discipline Gambit: +35 Recklessness)" Theme.Purple
          // Dedicated Discipline Gambits (cost Study Stacks with 0 Recklessness!)
          if player.StudyStacks >= 2 then
            sprintf "🎯 [bold %s]Calculated Flaw Strike[/] (Discipline Gambit: Spend Study Stacks for Vital Opening, 0 Recklessness)" Theme.Purple
          if player.StudyStacks >= 3 then
            sprintf "⚔️  [bold %s]Masterful Disarm[/] (Discipline Gambit: Spend Study Stacks to Degrade Opponent Weapon, 0 Recklessness)" Theme.Purple
        ]

        // Order strikes prioritizing player's primary class archetype vector
        let martialStrikes =
          match player.Class.Vector with
          | Vector.Power -> powerStrikes @ agilityStrikes @ disciplineStrikes
          | Vector.Agility -> agilityStrikes @ disciplineStrikes @ powerStrikes
          | Vector.Discipline -> disciplineStrikes @ powerStrikes @ agilityStrikes

        yield! martialStrikes

        // Tactical Stance Shifts ordered by class archetype preference
        let powerStanceShift =
          if player.Stance <> CombatStance.PowerStance then
            [ sprintf "↺ [bold %s]Shift Stance: Power Stance[/] (Sweeping Cleaves & Sunder Armor/Weapon)" Theme.Red ]
          else []

        let agilityStanceShift =
          if player.Stance <> CombatStance.AgilityStance then
            [ sprintf "↺ [bold %s]Shift Stance: Agility Stance[/] (Probing Cadence & Overwhelm Crits; -20%% AoO vs Flanks)" Theme.Green ]
          else []

        let disciplineStanceShift =
          if player.Stance <> CombatStance.DisciplineStance then
            [ sprintf "↺ [bold %s]Shift Stance: Discipline Stance[/] (Chained Strikes, Study Stacks & Unpenalized AoO)" Theme.Purple ]
          else []

        let stanceShifts =
          match player.Class.Vector with
          | Vector.Power -> powerStanceShift @ agilityStanceShift @ disciplineStanceShift
          | Vector.Agility -> agilityStanceShift @ disciplineStanceShift @ powerStanceShift
          | Vector.Discipline -> disciplineStanceShift @ powerStanceShift @ agilityStanceShift

        yield! stanceShifts

        // Physical Defensive Reset
        sprintf "🛡️  [%s]Steady Form[/] (Physical Reset: Drain Recklessness via Poise, build Study)" Theme.Green

      else
        // Arcane Spellcraft (Mental Characters)
        let powProf = int (Math.Round(player.GetArcaneProficiency Power * 100.0))
        let agiProf = int (Math.Round(player.GetArcaneProficiency Agility * 100.0))
        let disProf = int (Math.Round(player.GetArcaneProficiency Discipline * 100.0))

        let strainTag (prof: int) =
          if prof < 85 then sprintf " [%s](%d%% Prof - Off-School Strain)[/]" Theme.Comment prof
          else sprintf " [bold %s](%d%% Prof - Specialization)[/]" Theme.Green prof

        let powerSpells = [
          sprintf "✨ [%s]Arcane Cataclysm: Elemental Blast[/] (Power - Intellect vs. Resolve)%s" Theme.Pink (strainTag powProf)
          if player.ComplexForm <> Some ComplexForm.AegisLattice then
            sprintf "⚡ [bold %s]Arcane Cataclysm: Overchannel[/] (Power Gambit: +35 Recklessness, Splash)%s" Theme.Pink (strainTag powProf)
        ]

        let agilitySpells = [
          sprintf "✨ [%s]Synaptic Glamour: Neural Static[/] (Agility - Acuity vs. Intuition)%s" Theme.Purple (strainTag agiProf)
          sprintf "⚡ [bold %s]Synaptic Glamour: Mind Fracture[/] (Agility Gambit: +25 Recklessness)%s" Theme.Purple (strainTag agiProf)
          sprintf "🪞 [%s]Mirror Illusion: Phantasmal Decoys[/] (Agility - Weave Mirror Clones)%s" Theme.Purple (strainTag agiProf)
          sprintf "⚡ [bold %s]Mirror Illusion: Decoy Swarm[/] (Agility Gambit: +25 Recklessness, Extra Clones)%s" Theme.Purple (strainTag agiProf)
        ]

        let disciplineSpells = [
          sprintf "🛡️  [%s]Runic Ward Trap: Abjuration Glyph[/] (Discipline - Acumen vs. Composure, Ward)%s" Theme.Cyan (strainTag disProf)
          sprintf "⚡ [bold %s]Runic Ward Trap: Anomalous Glyph[/] (Discipline Gambit: +30 Recklessness, Heavy Ward)%s" Theme.Cyan (strainTag disProf)
          sprintf "🌀 [%s]Disorienting Shockwave: Balance Disruption[/] (Discipline - Break Posture & Tempo)%s" Theme.Orange (strainTag disProf)
          sprintf "⚡ [bold %s]Disorienting Shockwave: Staggering Pulse[/] (Discipline Gambit: +25 Recklessness, Swarm Pulse)%s" Theme.Orange (strainTag disProf)
        ]

        let spells =
          match player.Class.Vector with
          | Vector.Power -> powerSpells @ agilitySpells @ disciplineSpells
          | Vector.Agility -> agilitySpells @ disciplineSpells @ powerSpells
          | Vector.Discipline -> disciplineSpells @ powerSpells @ agilitySpells

        yield! spells

        // Tactical Complex Form Threading (Mental Stance Shifts)
        let powerForm =
          if player.ComplexForm <> Some ComplexForm.ResonanceSpike then
            [ sprintf "🧵 [bold %s]Thread Form: Resonance Spike[/] (+25%% Spell Dmg & Fatigue; Fading Drain)" Theme.Pink ]
          else []

        let agilityForm =
          if player.ComplexForm <> Some ComplexForm.PhantasmalDiffusion then
            [ sprintf "🧵 [bold %s]Thread Form: Phantasmal Diffusion[/] (Decoy Evasion Swap, Passive Clones; -15%% Dmg)" Theme.Green ]
          else []

        let disciplineForm =
          if player.ComplexForm <> Some ComplexForm.AegisLattice then
            [ sprintf "🧵 [bold %s]Thread Form: Aegis Lattice[/] (+15 Ward/turn, Retribution Ward; Locks Overchannel)" Theme.Cyan ]
          else []

        let forms =
          match player.Class.Vector with
          | Vector.Power -> powerForm @ agilityForm @ disciplineForm
          | Vector.Agility -> agilityForm @ disciplineForm @ powerForm
          | Vector.Discipline -> disciplineForm @ powerForm @ agilityForm

        yield! forms

        // Mental Defensive Reset
        sprintf "🧠 [%s]Center Mind[/] (Mental Reset: Drain Recklessness, clear Confusion, restore Arcane Ward)" Theme.Cyan

      // Universal Respiratory Regulation Reset (always available across all disciplines)
      sprintf "🌬️  [%s]Steady Breathing[/] (Universal Reset: Deep breath vents Confusion, Frustration, Exhaustion & Overwhelm, restores Morale)" Theme.Cyan
    ]

  let private runInteractiveDuel (playerArch: ArchetypeInfo) (enemyArch: ArchetypeInfo) =
    let rng = Random()
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    let mutable player = playerArch.Factory ()
    let mutable enemy = enemyArch.Factory ()
    let mutable round = 1
    let mutable combatOver = false
    let mutable outcome = "PlayerDefeated"

    let session =
      CombatLogger.startSession
        (sprintf "%s vs %s" playerArch.Name enemyArch.Name)
        "Arena Tactical Duel"
        player
        enemy

    while not combatOver do
      Display.renderHUD player enemy round

      // 1. Choose Player Action
      let choices = buildActionChoices player enemy
      let choice =
        Display.promptSelectionWithHelp
          (sprintf "[bold %s]Round %d - Select Tactical Action for %s (Press '?' or F1 for Symbol Legend):[/]" Theme.Yellow round player.Name)
          choices
          (Some 11)
          (fun () -> Display.renderHUD player enemy round)

      let playerIntent = parseActionChoice choice

      let playerBefore = player
      let enemyBefore = enemy

      AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s executes %s...[/]" Theme.Green player.Name choice)
      let playerResult = ActionResolver.resolve roller playerIntent player enemy
      player <- playerResult.Actor
      enemy <- playerResult.Target

      CombatLogger.recordTurn session round 1 choice playerBefore enemyBefore playerResult

      Display.renderRollBreakdown choice player.Name playerResult.Contest
      playerResult.Events |> List.iter Display.logEvent

      let playerExecutedEnemy =
        playerResult.Events
        |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)

      if playerExecutedEnemy || enemy.Health.IsDepleted || enemy.Morale.IsDepleted then
        combatOver <- true
        outcome <- "PlayerVictorious"
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

        let enemyBeforeTurn = enemy
        let playerBeforeTurn = player

        let enemyResult = ActionResolver.resolve roller enemyIntent enemy player
        enemy <- enemyResult.Actor
        player <- enemyResult.Target

        let intentDesc = sprintf "%A" enemyIntent
        CombatLogger.recordTurn session round 2 intentDesc enemyBeforeTurn playerBeforeTurn enemyResult

        Display.renderRollBreakdown intentDesc enemy.Name enemyResult.Contest
        enemyResult.Events |> List.iter Display.logEvent

        let enemyExecutedPlayer =
          enemyResult.Events
          |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)

        if enemyExecutedPlayer || player.Health.IsDepleted || player.Morale.IsDepleted then
          combatOver <- true
          outcome <- "PlayerDefeated"
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

    let latestLog, _ = CombatLogger.writeSession session outcome player enemy
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(sprintf "[dim grey]Tactical combat log saved to: %s[/]" latestLog)
    AnsiConsole.Markup(sprintf "[bold %s]Combat concluded. Press any key to return to menu...[/]" Theme.Yellow)
    Console.ReadKey(true) |> ignore

  let private runBalanceSimulator (archA: ArchetypeInfo) (archB: ArchetypeInfo) =
    let iterations =
      Display.promptWithVim(
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
      Display.promptWithVim(
        SelectionPrompt<int>()
          .Title(sprintf "[bold %s]Select Swarm Size (Number of Opponents fighting simultaneously):[/]" Theme.Yellow)
          .AddChoices([ 2; 3; 4; 5; 6; 8; 10; 20; 50; 100 ])
      )

    let iterations =
      Display.promptWithVim(
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
        | Veteran -> Theme.Cyan
        | Master -> Theme.Yellow
        | GrandMaster -> Theme.Purple

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
      Tier = Veteran
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

    let isBestiarySwarm = hasFlag "--bestiary-swarm" || hasFlag "--monster-swarm" || hasFlag "-ms"
    let isPeerMatrix = hasFlag "--peer-matrix" || hasFlag "--peer-balance" || hasFlag "-pbm"
    let isMatrix = hasFlag "--balance-matrix" || hasFlag "--matrix" || hasFlag "-b"
    let isSim = hasFlag "--sim" || hasGroupFlag || isStandaloneSimFlag || hasFlag "-a1" || hasFlag "--solo"

    if isBestiarySwarm then
      let tierArg = findArg ["-t"; "--tier"] ""
      let tierOpt =
        match tierArg.ToLowerInvariant() with
        | "novice" -> Some CombatTier.Novice
        | "veteran" -> Some CombatTier.Veteran
        | "master" -> Some CombatTier.Master
        | "grandmaster" | "gm" -> Some CombatTier.GrandMaster
        | _ -> None
      RunBestiarySwarmMatrix tierOpt
    elif isPeerMatrix then
      RunPeerBalanceMatrix
    elif isMatrix then
      RunBalanceMatrix
    elif not isSim then
      InteractiveMenu
    else
      let arch1 = findArg ["--solo"; "-s"; "-a1"; "--archetype1"] "Veteran Berserker"
      let arch2 = findArg ["--mob"; "--enemy"; "-e"; "-a2"; "--archetype2"] "Veteran Inquisitor"
      let itersStr = findArg ["-n"; "--iterations"] "100"
      let iters = match Int32.TryParse itersStr with true, v -> Math.Max(1, v) | _ -> 100

      let countStr = findArg ["-k"; "--count"] (if hasGroupFlag then "3" else "0")
      let mobCount = match Int32.TryParse countStr with true, v when v > 1 -> v | _ -> 0

      if mobCount > 1 || hasGroupFlag then
        let effectiveCount = Math.Max(2, mobCount)
        RunGroupSimulation(arch1, arch2, effectiveCount, iters)
      else
        RunSimulation(arch1, arch2, iters)


  let private runStoryDuel (player: Combatant) (boss: Combatant) : CombatOutcome * Combatant =
    let rng = Random()
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    let mutable currentCombatant = player
    let mutable currentBoss = boss
    let mutable round = 1
    let mutable combatOver = false
    let mutable outcome = CombatOutcome.PlayerDefeated

    let session =
      CombatLogger.startSession
        (sprintf "%s vs %s" currentCombatant.Name currentBoss.Name)
        "Story / Tower Tactical Duel"
        currentCombatant
        currentBoss

    while not combatOver do
      Display.renderHUD currentCombatant currentBoss round

      // 1. Choose Player Action
      let choices = buildActionChoices currentCombatant currentBoss
      let choice =
        Display.promptSelectionWithHelp
          (sprintf "[bold %s]Round %d - Tactical Action against %s (Press '?' or F1 for Symbol Legend):[/]" Theme.Yellow round currentBoss.Name)
          choices
          (Some 11)
          (fun () -> Display.renderHUD currentCombatant currentBoss round)

      let playerIntent = parseActionChoice choice

      let playerBefore = currentCombatant
      let bossBefore = currentBoss

      AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s executes %s...[/]" Theme.Green currentCombatant.Name choice)
      let playerResult = ActionResolver.resolve roller playerIntent currentCombatant currentBoss
      currentCombatant <- playerResult.Actor
      currentBoss <- playerResult.Target

      CombatLogger.recordTurn session round 1 choice playerBefore bossBefore playerResult

      Display.renderRollBreakdown choice currentCombatant.Name playerResult.Contest
      playerResult.Events |> List.iter Display.logEvent

      let playerExecutedEnemy =
        playerResult.Events
        |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)

      if playerExecutedEnemy || currentBoss.Health.IsDepleted || currentBoss.Morale.IsDepleted then
        combatOver <- true
        outcome <- CombatOutcome.PlayerVictorious
        AnsiConsole.WriteLine()
        AnsiConsole.Write(
          Rule(sprintf "[bold %s]★★★ MANIFESTATION RESOLVED: %s PREVAILED! ★★★[/]" Theme.Green currentCombatant.Name)
            .Centered()
            .RuleStyle(Theme.StyleGreen)
        )
      else
        // 2. Enemy AI Turn
        AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s lashes out with psychological fury...[/]" Theme.Pink currentBoss.Name)
        let enemyIntent = AI.chooseIntent currentBoss currentCombatant

        let bossBeforeTurn = currentBoss
        let playerBeforeTurn = currentCombatant

        let enemyResult = ActionResolver.resolve roller enemyIntent currentBoss currentCombatant
        currentBoss <- enemyResult.Actor
        currentCombatant <- enemyResult.Target

        let intentDesc = sprintf "%A" enemyIntent
        CombatLogger.recordTurn session round 2 intentDesc bossBeforeTurn playerBeforeTurn enemyResult

        Display.renderRollBreakdown intentDesc currentBoss.Name enemyResult.Contest
        enemyResult.Events |> List.iter Display.logEvent

        let enemyExecutedPlayer =
          enemyResult.Events
          |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)

        if enemyExecutedPlayer || currentCombatant.Health.IsDepleted || currentCombatant.Morale.IsDepleted then
          combatOver <- true
          outcome <- CombatOutcome.PlayerDefeated
          AnsiConsole.WriteLine()
          AnsiConsole.Write(
            Rule(sprintf "[bold %s]☠☠☠ DEFEATED BY TRAUMA: %s HAS FALLEN! ☠☠☠[/]" Theme.Red currentCombatant.Name)
              .Centered()
              .RuleStyle(Theme.StyleRed)
          )

      if not combatOver then
        AnsiConsole.WriteLine()
        AnsiConsole.Markup(sprintf "[%s]Press any key to proceed to next round...[/]" Theme.Comment)
        Console.ReadKey(true) |> ignore
        round <- round + 1

    let outcomeStr = if outcome = CombatOutcome.PlayerVictorious then "PlayerVictorious" else "PlayerDefeated"
    let latestLog, _ = CombatLogger.writeSession session outcomeStr currentCombatant currentBoss
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(sprintf "[dim grey]Tactical combat log saved to: %s[/]" latestLog)

    outcome, currentCombatant

  let private createProloguePlayer (className: string) : Combatant =
    StoryBosses.createProloguePlayer className

  let private runInteractiveStory () =
    AnsiConsole.Clear()
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]FORNACH: THE TRAUMA LOOP & THE GRIEF STAGES[/]" Theme.Yellow)
        .Centered()
        .RuleStyle(Theme.StylePurple)
    )
    AnsiConsole.WriteLine()

    // 1. Atmospheric Prologue Introduction
    let introText =
      "The asphalt was slick with evening drizzle... Headlights tore through the downpour, metal screamed, and the cold darkness took you.\n\n" +
      "Now, the roar of screaming metal recedes into the sound of rhythmic rain drumming against wet shale.\n" +
      "You peel your face out of the mud of the Abandoned Iron Quarry. Your past is an empty, dark vault.\n\n" +
      "Beside you in the muck lies a battered chest, its iron lock already broken and hanging loose."

    let introPanel =
      Panel(Markup(sprintf "[bold %s]%s[/]" Theme.Foreground (Markup.Escape introText)))
        .Header(sprintf "[bold %s] PROLOGUE: THE FATAL CROSSING [/]" Theme.Cyan)
        .Border(BoxBorder.Rounded)
        .BorderStyle(Style(foreground = Nullable Theme.ColorCyan))

    AnsiConsole.Write(introPanel)
    AnsiConsole.WriteLine()

    // 2. Scavenger Chest Class Selection
    let choices =
      [ "🗡️ Two-handed Greatsword (Berserker) — Ferocious momentum, sweeping cleaves & high force"
        "🤺 Paired Stiletto & Rapier (Duelist) — Fencing precision, high reflex, agile cadences"
        "🛡️ Arming Sword & Reinforced Shield (Warden) — Bastion defense, fortress poise, counterplay"
        "🪄 Carved Ash Staff (Inquisitor) — Arcane resonance, psionic intellect, mental clarity" ]

    let choice =
      Display.promptSelectionWithHelp
        (sprintf "[bold %s]Scavenge the battered chest (Choose your weapon armament and awaken your class):[/]" Theme.Yellow)
        choices
        None
        (fun () -> ())

    let chosenClass =
      if choice.Contains("Berserker") then "berserker"
      elif choice.Contains("Duelist") then "duelist"
      elif choice.Contains("Warden") then "warden"
      else "inquisitor"

    let player = StoryBosses.createProloguePlayer chosenClass
    let weapon = player.EquippedItems |> List.tryHead |> Option.map (fun w -> w.Name) |> Option.defaultValue "Armament"
    let desc =
      match player.Class with
      | CharacterClass.Berserker -> "Power Specialist • Sweeping Cleaves & Wild Blows"
      | CharacterClass.Duelist -> "Agility Specialist • Probing Finesse & Stacking Bleeds"
      | CharacterClass.Warden -> "Discipline Specialist • Bastion Defense & Counterplay"
      | _ -> "Mental Power Arcanist • Psionic Cataclysms & Cognitive Strain"

    let panel =
      Panel(Markup(sprintf "[bold %s]󰓥 CLASS AWAKENED: %s[/] [grey](%s)[/]\n[italic white]Equipped: %s  •  Combat Stance: %A[/]\n[grey]Bundle of sharpened caltrops recovered inside lid.[/]"
        Theme.Green (player.Class.Name.ToUpperInvariant()) desc (Markup.Escape weapon) player.Stance))
        .Border(BoxBorder.Heavy)
        .BorderStyle(Style(foreground = Nullable Theme.ColorGreen))

    AnsiConsole.Clear()
    AnsiConsole.Write(panel)
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(sprintf "[bold %s]A child's terrified scream cuts through the metallic clangor of the quarry...[/]" Theme.Yellow)
    AnsiConsole.MarkupLine(sprintf "[italic %s]Ahead lies the sprawling 2D spatial map of the Iron Quarry. Navigate, explore, grind, and conquer the shadows.[/]" Theme.Comment)
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(sprintf "[%s]Press any key to enter the quarry map and begin your expedition...[/]" Theme.Comment)
    Console.ReadKey(true) |> ignore

    // 3. Launch 2D Spatial Map Loop for Story Campaign
    TowerDisplay.runStoryCrawl player 1 runStoryDuel DeathAnimation.playTruckReplay

  [<EntryPoint>]
  let main (args: string array) =
    match parseCliArgs args with
    | RunBestiarySwarmMatrix tierOpt ->
      printBanner()
      Simulation.renderBestiarySwarmMatrix tierOpt
      0

    | RunPeerBalanceMatrix ->
      printBanner()
      Simulation.renderPeerBalanceMatrix()
      0

    | RunBalanceMatrix ->
      printBanner()
      Simulation.renderBalanceMatrix()
      0

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

        let termWidth = Math.Max(80, AnsiConsole.Profile.Width)
        let contentWidth = Math.Min(96, termWidth - 4)
        let margin = Math.Max(0, (termWidth - contentWidth) / 2)
        let pad = String(' ', margin)

        let formatMenuItem icon title desc color =
          sprintf "%s%s [bold %s]%-40s[/] [italic %s]── %s[/]" pad icon color title Theme.Comment desc

        // Center the header and hint on the full terminal width; `pad` is the
        // menu margin, which is narrower than the terminal whenever contentWidth
        // (96) is capped below the terminal width. Count codepoints, not UTF-16
        // units: the Nerd Font icons are non-BMP and occupy a single cell.
        let centerPad (text: string) =
          let visible = (Markup.Remove text).EnumerateRunes() |> Seq.length
          String(' ', Math.Max(0, (termWidth - visible) / 2))

        let promptHeader = "══════════ 󰒋 SELECT EXPEDITION OR BENCHMARK MODE ══════════"
        let promptHint = "      (󰌌 Navigate: [bold white]↑/↓[/] or [bold white]j/k[/]  •  󰌑 Select: [bold white]Enter[/]  •  󰗼 Quit: [bold white]Exit[/])"

        let promptTitle =
          sprintf "%s[bold %s]%s[/]\n%s[grey]%s[/]\n"
            (centerPad promptHeader) Theme.Yellow promptHeader (centerPad promptHint) promptHint

        let choice =
          Display.promptWithVim(
            SelectionPrompt<string>()
              .Title(promptTitle)
              .PageSize(12)
              .AddChoices([
                formatMenuItem "󰈙 " "Interactive Story Mode" "Narrative prologue & aspect battles" Theme.Cyan
                formatMenuItem "󰒋 " "Ascend The Infinite Tower" "Roguelike procedural dungeon crawl" Theme.Yellow
                formatMenuItem "󰓥 " "Interactive Duel Arena" "Tactical turn-based combat duel" Theme.Green
                formatMenuItem "󰓎 " "Monte-Carlo Balance Simulator (1 vs 1)" "Statistical win-rate analysis" Theme.Cyan
                formatMenuItem " " "1 vs N Encirclement Swarm Simulator" "Swarm overwhelm stress test" Theme.Pink
                formatMenuItem " " "Custom Combatant Builder" "Interactive stat & stance forge" Theme.Orange
                formatMenuItem "󰂺 " "View Archetype Roster" (sprintf "Inspect %d mastery archetypes" Archetypes.allArchetypes.Length) Theme.Purple
                formatMenuItem "󰓥 " "Peer Class 1v1 Balance Matrix" "Cross-tier 100-run pairwise duel benchmark" Theme.Green
                formatMenuItem "󰈷 " "Swarm Tipping Point Balance Matrix" "128-matchup macro balance benchmark" Theme.Yellow
                formatMenuItem "󰞁 " "Bestiary Monster Swarm Matrix" "Swarm tipping point for all 21 monsters vs classes" Theme.Red
                formatMenuItem "󰒋 " "Symbol & Glyph Reference Manual" "Exhaustive guide to icons, meters & hazards" Theme.Yellow
                formatMenuItem "󰗼 " "Exit" "Close the Fornach Arena" Theme.Red
              ])
          )

        if choice.Contains("Interactive Story Mode") then
          runInteractiveStory ()
        elif choice.Contains("Ascend The Infinite Tower") then
          let playerArch = promptSelectArchetype (sprintf "[bold %s]Select Expedition Champion:[/]" Theme.Yellow)
          let player = playerArch.Factory()
          TowerDisplay.runTowerCrawl player 1 runStoryDuel DeathAnimation.playTruckReplay
        elif choice.Contains("Interactive Duel Arena") then
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

        elif choice.Contains("Peer Class 1v1 Balance Matrix") then
          Simulation.renderPeerBalanceMatrix ()
          AnsiConsole.MarkupLine(sprintf "[%s]Press any key to return to menu...[/]" Theme.Comment)
          Console.ReadKey(true) |> ignore

        elif choice.Contains("Swarm Tipping Point Balance Matrix") then
          Simulation.renderBalanceMatrix ()
          AnsiConsole.MarkupLine(sprintf "[%s]Press any key to return to menu...[/]" Theme.Comment)
          Console.ReadKey(true) |> ignore

        elif choice.Contains("Bestiary Monster Swarm Matrix") then
          let tierChoice =
            Display.promptWithVim(
              SelectionPrompt<string>()
                .Title("[bold yellow]Select Champion Progression Tier for Bestiary Swarm Benchmark:[/]")
                .AddChoices([
                  "All Tiers (Novice, Veteran, Master, GrandMaster)"
                  "Novice Tier (Level 1 Adventurers)"
                  "Veteran Tier (Level 40 Champions)"
                  "Master Tier (Level 100 Champions)"
                  "GrandMaster Tier (Level 200 Paragons)"
                ])
            )
          let selectedTier =
            if tierChoice.Contains("Novice") then Some CombatTier.Novice
            elif tierChoice.Contains("Veteran") then Some CombatTier.Veteran
            elif tierChoice.Contains("Master") && not (tierChoice.Contains("GrandMaster")) then Some CombatTier.Master
            elif tierChoice.Contains("GrandMaster") then Some CombatTier.GrandMaster
            else None
          Simulation.renderBestiarySwarmMatrix selectedTier
          AnsiConsole.MarkupLine(sprintf "[%s]Press any key to return to menu...[/]" Theme.Comment)
          Console.ReadKey(true) |> ignore

        elif choice.Contains("Symbol & Glyph Reference Manual") then
          Display.showSymbolAndGlyphLegend ()

        elif choice.Contains("Exit") then
          running <- false

      AnsiConsole.MarkupLine(sprintf "[bold %s]Exiting Fornach Arena. Farewell![/]" Theme.Yellow)
      0
