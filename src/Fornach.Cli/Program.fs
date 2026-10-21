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
        .Header(Display.stripNerdGlyphs (sprintf "[bold %s] 󰒋 COMMAND COCKPIT & SYSTEM TELEMETRY [/]" Theme.Yellow))
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
    elif choice.Contains("Force Strike: Insight Cleave") then
      StandardAttack (ForceInsightStrike 3)
    elif choice.Contains("Finesse Cadence: Rapid Probing") then
      StandardAttack (FinesseCadence false)
    elif choice.Contains("Finesse Cadence: Relentless Blitz") then
      StandardAttack (FinesseCadence true)
    elif choice.Contains("Finesse Cadence: Threaded Opening") then
      StandardAttack (FinesseInsightStrike 3)
    elif choice.Contains("Prowess Strike: Stance Pressure") then
      StandardAttack (ProwessStrike false)
    elif choice.Contains("Prowess Strike: Invitational Bait") then
      StandardAttack (ProwessStrike true)
    elif choice.Contains("Shield Bash: Heavy Slam") then
      StandardAttack (ShieldBash true)
    elif choice.Contains("Shield Bash") then
      StandardAttack (ShieldBash false)
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
    elif choice.Contains("Staff Siphon") then
      StandardAttack StaffSiphon
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
    elif choice.Contains("Raise Shield") then
      RecoveryAction RaiseShield
    elif choice.Contains("Steady Form") then
      RecoveryAction SteadyForm
    elif choice.Contains("Center Mind") then
      RecoveryAction CenterMind
    elif choice.Contains("Steady Breathing") then
      RecoveryAction SteadyBreathing
    else
      RecoveryAction SteadyForm

  let buildActionChoicesWithTelegraph (player: Combatant) (enemy: Combatant) (enemyIntentOpt: ActionIntent option) : string list =
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

      let assess = TacticalAssessment.assess player enemy

      // Tactical Counterplay Badges based on Telegraphed Enemy Intent
      let powerCounterTag, agilityCounterTag, disciplineCounterTag, shieldInterruptTag, raiseShieldTag =
        match enemyIntentOpt with
        | Some (StandardAttack atk) ->
          let isHeavyGambit =
            match atk with
            | ForceStrike true | FinesseCadence true | ProwessStrike true | ArcaneCataclysm true | TraumaAttack _ -> true
            | _ -> false

          let interruptBadge =
            if isHeavyGambit then sprintf " [bold %s]★ SHIELD INTERRUPT (Disrupts Windup!)[/]" Theme.Yellow
            elif atk.Vector = Vector.Power then sprintf " [bold %s]★ INTERRUPT OPPORTUNITY[/]" Theme.Cyan
            else ""

          let powTag = if atk.Vector = Vector.Discipline then sprintf " [bold %s]★ Guard Break[/]" Theme.Red else ""
          let agiTag = if atk.Vector = Vector.Power then sprintf " [bold %s]★ Counter-Evasion[/]" Theme.Green else ""
          let disTag = if atk.Vector = Vector.Agility then sprintf " [bold %s]★ Bastion Counter[/]" Theme.Purple else ""
          let rShieldTag = if atk.Vector = Vector.Agility then sprintf " [bold %s]★ Parry Opening[/]" Theme.Green else ""

          powTag, agiTag, disTag, interruptBadge, rShieldTag
        | _ -> "", "", "", "", ""

      // Orders attack groups by estimated effectiveness against what the player has scouted.
      // Obscured insight means nothing is known, so the archetype's own priority order stands.
      let rankStrikes (groups: (AttackClassification * string list) list) : string list =
        if assess.InsightLevel = Obscured then
          groups |> List.collect snd
        else
          groups
          |> List.map (fun (attack, labels) -> AttackEstimator.estimate player enemy attack, labels)
          |> List.sortBy (fun (score, _) -> -score)
          |> List.collect snd

      if player.Plane = Physical then
        let fortSuffix =
          match assess.InsightLevel with
          | Penetrating | Keen ->
            let fortVal = enemy.GetStat StatId.Fortitude
            if fst assess.WeakestDefense = StatId.Fortitude then sprintf " - [bold %s]%d ★ Opening[/]" Theme.Green fortVal
            elif fst assess.BestDefense = StatId.Fortitude then sprintf " - [bold %s]%d ⚠ Apex Guard[/]" Theme.Red fortVal
            else sprintf " - [%s]%d[/]" Theme.Comment fortVal
          | Discerning ->
            if fst assess.WeakestDefense = StatId.Fortitude then sprintf " - [bold %s]Opening[/]" Theme.Green
            elif fst assess.BestDefense = StatId.Fortitude then sprintf " - [bold %s]Apex Guard[/]" Theme.Red
            else ""
          | Obscured -> ""

        let refSuffix =
          match assess.InsightLevel with
          | Penetrating | Keen ->
            let refVal = enemy.GetStat StatId.Reflex
            if fst assess.WeakestDefense = StatId.Reflex then sprintf " - [bold %s]%d ★ Opening[/]" Theme.Green refVal
            elif fst assess.BestDefense = StatId.Reflex then sprintf " - [bold %s]%d ⚠ Apex Guard[/]" Theme.Red refVal
            else sprintf " - [%s]%d[/]" Theme.Comment refVal
          | Discerning ->
            if fst assess.WeakestDefense = StatId.Reflex then sprintf " - [bold %s]Opening[/]" Theme.Green
            elif fst assess.BestDefense = StatId.Reflex then sprintf " - [bold %s]Apex Guard[/]" Theme.Red
            else ""
          | Obscured -> ""

        let poiseSuffix =
          match assess.InsightLevel with
          | Penetrating | Keen ->
            let poiseVal = enemy.GetStat StatId.Poise
            if fst assess.WeakestDefense = StatId.Poise then sprintf " - [bold %s]%d ★ Opening[/]" Theme.Green poiseVal
            elif fst assess.BestDefense = StatId.Poise then sprintf " - [bold %s]%d ⚠ Apex Guard[/]" Theme.Red poiseVal
            else sprintf " - [%s]%d[/]" Theme.Comment poiseVal
          | Discerning ->
            if fst assess.WeakestDefense = StatId.Poise then sprintf " - [bold %s]Opening[/]" Theme.Green
            elif fst assess.BestDefense = StatId.Poise then sprintf " - [bold %s]Apex Guard[/]" Theme.Red
            else ""
          | Obscured -> ""

        // Physical Martial Strikes grouped by vector (attacks fluidly transition combat stance)
        let powerStrikes = [
          sprintf "⚔️  [%s]Force Strike: Standard Cleave[/] (Power Stance • Cleave vs. Fortitude%s)%s" Theme.Red fortSuffix powerCounterTag
          sprintf "⚡ [bold %s]Force Strike: Wild Blow[/] (Power Stance Gambit: +30 Recklessness, 1.5x Dmg)" Theme.Red
          // Dedicated Power Gambit (costs Study Stacks with 0 Recklessness!)
          if player.StudyStacks >= 2 then
            sprintf "🎯 [bold %s]Force Strike: Insight Cleave[/] (Power Stance Gambit: Spend Study Stacks for a Read Opening, 0 Recklessness)" Theme.Red
        ]

        let agilityStrikes = [
          sprintf "⚔️  [%s]Finesse Cadence: Rapid Probing[/] (Agility Stance • Probing Cadence vs. Reflex%s)%s" Theme.Green refSuffix agilityCounterTag
          sprintf "⚡ [bold %s]Finesse Cadence: Relentless Blitz[/] (Agility Stance Gambit: +25 Recklessness)" Theme.Green
          // Dedicated Agility Gambit (costs Study Stacks with 0 Recklessness!)
          if player.StudyStacks >= 2 then
            sprintf "🎯 [bold %s]Finesse Cadence: Threaded Opening[/] (Agility Stance Gambit: Spend Study Stacks to Thread a Vital Opening, 0 Recklessness)" Theme.Green
        ]

        let disciplineStrikes = [
          sprintf "⚔️  [%s]Prowess Strike: Stance Pressure[/] (Discipline Stance • Study Stacks vs. Poise%s)%s" Theme.Purple poiseSuffix disciplineCounterTag
          sprintf "⚡ [bold %s]Prowess Strike: Invitational Bait[/] (Discipline Stance Gambit: +35 Recklessness)" Theme.Purple
          // Active Armament Technique: Fighter Sword & Shield Bash
          sprintf "🛡️  [bold %s]Shield Bash: Standard Slam[/] (Discipline Stance • Prowess vs. Poise, Frustration & Stagger)%s" Theme.Cyan shieldInterruptTag
          sprintf "⚡ [bold %s]Shield Bash: Heavy Slam[/] (Discipline Stance Gambit: +25 Recklessness, Stagger & Interrupt)%s" Theme.Cyan shieldInterruptTag
          // Dedicated Discipline Gambits (cost Study Stacks with 0 Recklessness!)
          if player.StudyStacks >= 2 then
            sprintf "🎯 [bold %s]Calculated Flaw Strike[/] (Discipline Stance Gambit: Spend Study Stacks for Vital Opening, 0 Recklessness)" Theme.Purple
          if player.StudyStacks >= 3 then
            sprintf "⚔️  [bold %s]Masterful Disarm[/] (Discipline Stance Gambit: Spend Study Stacks to Degrade Opponent Weapon, 0 Recklessness)" Theme.Purple
        ]

        // Archetype vector leads the list and breaks ties between equally effective strikes
        let martialGroups =
          match player.Class.Vector with
          | Vector.Power ->
            [ ForceStrike false, powerStrikes
              FinesseCadence false, agilityStrikes
              ProwessStrike false, disciplineStrikes ]
          | Vector.Agility ->
            [ FinesseCadence false, agilityStrikes
              ProwessStrike false, disciplineStrikes
              ForceStrike false, powerStrikes ]
          | Vector.Discipline ->
            [ ProwessStrike false, disciplineStrikes
              ForceStrike false, powerStrikes
              FinesseCadence false, agilityStrikes ]

        yield! rankStrikes martialGroups

        // Active Armament Recovery: Shield Bastion Guard
        sprintf "🛡️  [bold %s]Raise Shield[/] (Bastion Posture: Fortify Armor +20, Gain 2 Study Stacks, Bleed Recklessness)%s" Theme.Green raiseShieldTag
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

        let resolveSuffix =
          match assess.InsightLevel with
          | Penetrating | Keen ->
            let resVal = enemy.GetStat StatId.Resolve
            if fst assess.WeakestDefense = StatId.Resolve then sprintf " - [bold %s]%d ★ Opening[/]" Theme.Green resVal
            elif fst assess.BestDefense = StatId.Resolve then sprintf " - [bold %s]%d ⚠ Apex Guard[/]" Theme.Red resVal
            else sprintf " - [%s]%d[/]" Theme.Comment resVal
          | Discerning ->
            if fst assess.WeakestDefense = StatId.Resolve then sprintf " - [bold %s]Opening[/]" Theme.Green
            elif fst assess.BestDefense = StatId.Resolve then sprintf " - [bold %s]Apex Guard[/]" Theme.Red
            else ""
          | Obscured -> ""

        let intuitionSuffix =
          match assess.InsightLevel with
          | Penetrating | Keen ->
            let intVal = enemy.GetStat StatId.Intuition
            if fst assess.WeakestDefense = StatId.Intuition then sprintf " - [bold %s]%d ★ Opening[/]" Theme.Green intVal
            elif fst assess.BestDefense = StatId.Intuition then sprintf " - [bold %s]%d ⚠ Apex Guard[/]" Theme.Red intVal
            else sprintf " - [%s]%d[/]" Theme.Comment intVal
          | Discerning ->
            if fst assess.WeakestDefense = StatId.Intuition then sprintf " - [bold %s]Opening[/]" Theme.Green
            elif fst assess.BestDefense = StatId.Intuition then sprintf " - [bold %s]Apex Guard[/]" Theme.Red
            else ""
          | Obscured -> ""

        let composureSuffix =
          match assess.InsightLevel with
          | Penetrating | Keen ->
            let compVal = enemy.GetStat StatId.Composure
            if fst assess.WeakestDefense = StatId.Composure then sprintf " - [bold %s]%d ★ Opening[/]" Theme.Green compVal
            elif fst assess.BestDefense = StatId.Composure then sprintf " - [bold %s]%d ⚠ Apex Guard[/]" Theme.Red compVal
            else sprintf " - [%s]%d[/]" Theme.Comment compVal
          | Discerning ->
            if fst assess.WeakestDefense = StatId.Composure then sprintf " - [bold %s]Opening[/]" Theme.Green
            elif fst assess.BestDefense = StatId.Composure then sprintf " - [bold %s]Apex Guard[/]" Theme.Red
            else ""
          | Obscured -> ""

        let powerSpells = [
          sprintf "✨ [%s]Arcane Cataclysm: Elemental Blast[/] (Resonance Spike Form • Intellect vs. Resolve%s)%s%s" Theme.Pink resolveSuffix (strainTag powProf) powerCounterTag
          if player.ComplexForm <> Some ComplexForm.AegisLattice then
            sprintf "⚡ [bold %s]Arcane Cataclysm: Overchannel[/] (Resonance Spike Gambit: +35 Recklessness, Splash)%s" Theme.Pink (strainTag powProf)
        ]

        let agilitySpells = [
          sprintf "✨ [%s]Synaptic Glamour: Neural Static[/] (Phantasmal Diffusion Form • Acuity vs. Intuition%s)%s%s" Theme.Purple intuitionSuffix (strainTag agiProf) agilityCounterTag
          sprintf "⚡ [bold %s]Synaptic Glamour: Mind Fracture[/] (Phantasmal Diffusion Gambit: +25 Recklessness)%s" Theme.Purple (strainTag agiProf)
          sprintf "🪞 [%s]Mirror Illusion: Phantasmal Decoys[/] (Phantasmal Diffusion Form • Weave Mirror Clones)%s" Theme.Purple (strainTag agiProf)
          sprintf "⚡ [bold %s]Mirror Illusion: Decoy Swarm[/] (Phantasmal Diffusion Gambit: +25 Recklessness, Extra Clones)%s" Theme.Purple (strainTag agiProf)
        ]

        let siphonClarityTag =
          if player.Meters.CognitiveFatigue.Value >= 20 || player.Meters.Recklessness.Value >= 20 then
            sprintf " [bold %s]★ Vents Fatigue & Calms Mind[/]" Theme.Green
          else ""

        let disciplineSpells = [
          // Active Armament Technique: Mage Staff Siphon
          sprintf "✨ [bold %s]Staff Siphon: Runic Clarity[/] (Aegis Lattice Form • Strike Morale, Siphon -20 Fatigue, -15 Recklessness, +15 Ward)%s%s%s" Theme.Cyan composureSuffix disciplineCounterTag siphonClarityTag
          sprintf "🛡️  [%s]Runic Ward Trap: Abjuration Glyph[/] (Aegis Lattice Form • Acumen vs. Composure%s, Ward)%s%s" Theme.Cyan composureSuffix (strainTag disProf) disciplineCounterTag
          sprintf "⚡ [bold %s]Runic Ward Trap: Anomalous Glyph[/] (Aegis Lattice Gambit: +30 Recklessness, Heavy Ward)%s" Theme.Cyan (strainTag disProf)
          sprintf "🌀 [%s]Disorienting Shockwave: Balance Disruption[/] (Aegis Lattice Form • Break Posture & Tempo)%s" Theme.Orange (strainTag disProf)
          sprintf "⚡ [bold %s]Disorienting Shockwave: Staggering Pulse[/] (Aegis Lattice Gambit: +25 Recklessness, Swarm Pulse)%s" Theme.Orange (strainTag disProf)
        ]

        let spellGroups =
          match player.Class.Vector with
          | Vector.Power ->
            [ ArcaneCataclysm false, powerSpells
              SynapticGlamour false, agilitySpells
              RunicWardTrap false, disciplineSpells ]
          | Vector.Agility ->
            [ SynapticGlamour false, agilitySpells
              RunicWardTrap false, disciplineSpells
              ArcaneCataclysm false, powerSpells ]
          | Vector.Discipline ->
            [ RunicWardTrap false, disciplineSpells
              ArcaneCataclysm false, powerSpells
              SynapticGlamour false, agilitySpells ]

        yield! rankStrikes spellGroups

        // Mental Defensive Reset
        sprintf "🧠 [%s]Center Mind[/] (Mental Reset: Drain Recklessness, clear Confusion, restore Arcane Ward)" Theme.Cyan

      // Universal Respiratory Regulation Reset (always available across all disciplines)
      sprintf "🌬️  [%s]Steady Breathing[/] (Universal Reset: Deep breath vents Confusion, Frustration, Exhaustion & Overwhelm, restores Morale)" Theme.Cyan
    ]

  let buildActionChoices (player: Combatant) (enemy: Combatant) : string list =
    buildActionChoicesWithTelegraph player enemy None

  let private runInteractiveDuel (playerArch: ArchetypeInfo) (enemyArch: ArchetypeInfo) =
    let rng = Random()
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    let mutable player = playerArch.Factory ()
    let mutable enemy = enemyArch.Factory ()
    let mutable round = 1
    let mutable combatOver = false
    let mutable outcome = "PlayerDefeated"
    let mutable enemyInterrupted = false

    let session =
      CombatLogger.startSession
        (sprintf "%s vs %s" playerArch.Name enemyArch.Name)
        "Arena Tactical Duel"
        player
        enemy

    while not combatOver do
      let enemyIntent = AI.chooseIntent enemy player
      Display.renderHUDWithTelegraph player enemy round (Some enemyIntent)

      // 1. Choose Player Action
      let choices = buildActionChoicesWithTelegraph player enemy (Some enemyIntent)
      let choice =
        Display.promptSelectionWithHelp
          (sprintf "[bold %s]Round %d - Select Tactical Action for %s (Press '?' or F1 for Symbol Legend):[/]" Theme.Yellow round player.Name)
          choices
          (Some 12)
          (fun () -> Display.renderHUDWithTelegraph player enemy round (Some enemyIntent))

      let playerIntent = parseActionChoice choice

      let playerBefore = player
      let enemyBefore = enemy

      AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s executes %s...[/]" Theme.Green player.Name choice)
      let playerResult = ActionResolver.resolve roller playerIntent player enemy
      player <- playerResult.Actor
      enemy <- playerResult.Target

      enemyInterrupted <-
        playerResult.Events
        |> List.exists (function CombatEvent.ActionInterrupted _ -> true | _ -> false)

      CombatLogger.recordTurn session round 1 choice playerBefore enemyBefore playerResult

      Display.renderRollBreakdown choice player.Name playerResult.Contest
      Display.logDamageSummary playerResult.Events enemyBefore enemy
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
        let enemyBeforeTurn = enemy
        let playerBeforeTurn = player

        let enemyResult =
          if enemyInterrupted then
            AnsiConsole.MarkupLine(sprintf "\n[bold %s]⚡ %s is staggered from the technique interrupt! Attack defused![/]" Theme.Yellow enemy.Name)
            let recoverIntent = RecoveryAction SteadyForm
            ActionResolver.resolve roller recoverIntent enemy player
          else
            AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s executes telegraphed action...[/]" Theme.Pink enemy.Name)
            ActionResolver.resolve roller enemyIntent enemy player

        enemy <- enemyResult.Actor
        player <- enemyResult.Target

        let intentDesc = if enemyInterrupted then "Staggered Recovery (Interrupted)" else sprintf "%A" enemyIntent
        CombatLogger.recordTurn session round 2 intentDesc enemyBeforeTurn playerBeforeTurn enemyResult

        Display.renderRollBreakdown intentDesc enemy.Name enemyResult.Contest
        Display.logDamageSummary enemyResult.Events playerBeforeTurn player
        enemyResult.Events |> List.iter Display.logEvent
        if enemyInterrupted then enemyInterrupted <- false

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

    let name = Display.askString(sprintf "[%s]Enter combatant name:[/] " Theme.Foreground, "Gladiator")
    let hp = Display.askInt(sprintf "[%s]Enter Max Health (HP):[/] " Theme.Red, 2000)
    let morale = Display.askInt(sprintf "[%s]Enter Max Morale:[/] " Theme.Cyan, 2000)
    let armor = Display.askInt(sprintf "[%s]Enter Armor Durability (0-100):[/] " Theme.Yellow, 50)

    AnsiConsole.MarkupLine(sprintf "\n[bold %s]Assign Attributes (Uncapped 30–500):[/]" Theme.Yellow)
    let force = Display.askInt(sprintf "  [%s]Force (Physical Offense Power):[/] " Theme.Red, 120)
    let fort = Display.askInt(sprintf "  [%s]Fortitude (Physical Defense Power):[/] " Theme.Red, 110)
    let finesse = Display.askInt(sprintf "  [%s]Finesse (Physical Offense Agility):[/] " Theme.Green, 80)
    let reflex = Display.askInt(sprintf "  [%s]Reflex (Physical Defense Agility):[/] " Theme.Green, 80)
    let prowess = Display.askInt(sprintf "  [%s]Prowess (Martial Offense Discipline):[/] " Theme.Purple, 100)
    let poise = Display.askInt(sprintf "  [%s]Poise (Martial Defense Discipline):[/] " Theme.Purple, 100)

    let intellect = Display.askInt(sprintf "  [%s]Intellect / Presence (Mental Offense Power):[/] " Theme.Cyan, 60)
    let resolve = Display.askInt(sprintf "  [%s]Resolve / Will (Mental Defense Power):[/] " Theme.Cyan, 60)
    let acuity = Display.askInt(sprintf "  [%s]Acuity / Guile (Mental Offense Agility):[/] " Theme.Pink, 60)
    let intuition = Display.askInt(sprintf "  [%s]Intuition / Insight (Mental Defense Agility):[/] " Theme.Pink, 60)
    let acumen = Display.askInt(sprintf "  [%s]Acumen / Leverage (Mental Offense Discipline):[/] " Theme.Orange, 60)
    let composure = Display.askInt(sprintf "  [%s]Composure (Mental Defense Discipline):[/] " Theme.Orange, 60)

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

  let parseCliArgs (args: string array) : CliOptions * DebugOptions =
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

    let debugOptions =
      let forcedSeed =
        args
        |> Array.mapi (fun i a -> (i, a))
        |> Array.tryFind (fun (i, a) -> a = "--seed" && i + 1 < args.Length)
        |> Option.bind (fun (i, _) ->
          match Int32.TryParse args.[i + 1] with
          | true, v -> Some v
          | _ -> None)

      { Enabled = hasFlag "--debug" || hasFlag "-d"
        ForcedSeed = forcedSeed }

    let mode =
      if isBestiarySwarm then
        let tierArg = findArg [ "-t"; "--tier" ] ""
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
        let arch1 = findArg [ "--solo"; "-s"; "-a1"; "--archetype1" ] "Veteran Fighter"
        let arch2 = findArg [ "--mob"; "--enemy"; "-e"; "-a2"; "--archetype2" ] "Veteran Mage"
        let itersStr = findArg [ "-n"; "--iterations" ] "100"
        let iters = match Int32.TryParse itersStr with true, v -> Math.Max(1, v) | _ -> 100

        let countStr = findArg [ "-k"; "--count" ] (if hasGroupFlag then "3" else "0")
        let mobCount = match Int32.TryParse countStr with true, v when v > 1 -> v | _ -> 0

        if mobCount > 1 || hasGroupFlag then
          let effectiveCount = Math.Max(2, mobCount)
          RunGroupSimulation(arch1, arch2, effectiveCount, iters)
        else
          RunSimulation(arch1, arch2, iters)

    mode, debugOptions

  let private runStoryDuel (floorNumber: int) (player: Combatant) (boss: Combatant) : CombatOutcome * Combatant =
    let rng = Random()
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    let mutable currentCombatant = player
    let mutable currentBoss = boss
    let mutable round = 1
    let mutable combatOver = false
    let mutable outcome = CombatOutcome.PlayerDefeated
    let mutable enemyInterrupted = false

    let session =
      CombatLogger.startSession
        (sprintf "%s vs %s" currentCombatant.Name currentBoss.Name)
        "Story / Tower Tactical Duel"
        currentCombatant
        currentBoss

    RuntimeLog.write (sprintf "combat start: %s vs %s (round 1)" currentCombatant.Name currentBoss.Name)

    while not combatOver do
      let enemyIntent = AI.chooseIntent currentBoss currentCombatant
      Display.renderHUDWithTelegraph currentCombatant currentBoss round (Some enemyIntent)

      // 1. Choose Player Action
      let choices = buildActionChoicesWithTelegraph currentCombatant currentBoss (Some enemyIntent)
      let choice =
        Display.promptSelectionWithHelp
          (sprintf "[bold %s]Round %d - Tactical Action against %s (Press '?' or F1 for Symbol Legend):[/]" Theme.Yellow round currentBoss.Name)
          choices
          (Some 12)
          (fun () -> Display.renderHUDWithTelegraph currentCombatant currentBoss round (Some enemyIntent))

      let playerIntent = parseActionChoice choice

      let playerBefore = currentCombatant
      let bossBefore = currentBoss

      AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s executes %s...[/]" Theme.Green currentCombatant.Name choice)
      let playerResult = ActionResolver.resolve roller playerIntent currentCombatant currentBoss
      currentCombatant <- playerResult.Actor
      currentBoss <- playerResult.Target

      enemyInterrupted <-
        playerResult.Events
        |> List.exists (function CombatEvent.ActionInterrupted _ -> true | _ -> false)

      CombatLogger.recordTurn session round 1 choice playerBefore bossBefore playerResult
      RuntimeLog.write (
        sprintf "combat round %d player turn: %s (%d events)" round choice playerResult.Events.Length)

      Display.renderRollBreakdown choice currentCombatant.Name playerResult.Contest
      Display.logDamageSummary playerResult.Events bossBefore currentBoss
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
        let bossBeforeTurn = currentBoss
        let playerBeforeTurn = currentCombatant

        let enemyResult =
          if enemyInterrupted then
            AnsiConsole.MarkupLine(sprintf "\n[bold %s]⚡ %s is staggered from the technique interrupt! Attack defused![/]" Theme.Yellow currentBoss.Name)
            let recoverIntent = RecoveryAction SteadyForm
            ActionResolver.resolve roller recoverIntent currentBoss currentCombatant
          else
            AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s executes telegraphed action...[/]" Theme.Pink currentBoss.Name)
            if floorNumber = 1 then
              // Tutorial grace: the level-1 protagonist cannot be one-shot on the opening stage.
              ActionResolver.resolveWithoutLethal roller enemyIntent currentBoss currentCombatant
            else
              ActionResolver.resolve roller enemyIntent currentBoss currentCombatant

        currentBoss <- enemyResult.Actor
        currentCombatant <- enemyResult.Target

        let intentDesc = if enemyInterrupted then "Staggered Recovery (Interrupted)" else sprintf "%A" enemyIntent
        CombatLogger.recordTurn session round 2 intentDesc bossBeforeTurn playerBeforeTurn enemyResult
        RuntimeLog.write (
          sprintf "combat round %d enemy turn: %s (%d events)" round intentDesc enemyResult.Events.Length)

        Display.renderRollBreakdown intentDesc currentBoss.Name enemyResult.Contest
        Display.logDamageSummary enemyResult.Events playerBeforeTurn currentCombatant
        enemyResult.Events |> List.iter Display.logEvent
        if enemyInterrupted then enemyInterrupted <- false

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
    RuntimeLog.write (sprintf "combat end: %s (log %s)" outcomeStr latestLog)
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(sprintf "[dim grey]Tactical combat log saved to: %s[/]" latestLog)

    outcome, currentCombatant

  /// Renders the multi-enemy engagement HUD: the full player panel on the left, compact enemy
  /// panels stacked on the right, and a combat-log pane spanning the bottom.
  let private renderGroupHud (player: Combatant) (enemies: TowerEnemy list) (round: int) (logLines: string list) =
    AnsiConsole.Clear()
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]⚔ MULTI-ENEMY ENGAGEMENT — ROUND %d[/]" Theme.Yellow round)
        .Centered()
        .RuleStyle(Theme.StyleYellow)
    )

    let enemyColumn = Grid()
    enemyColumn.AddColumn(GridColumn()) |> ignore
    for e in enemies do
      let defeated = e.IsDefeated || e.Combatant.Health.IsDepleted || e.Combatant.Morale.IsDepleted
      let borderColor, headerColor =
        if defeated then Theme.ColorRed, Theme.Red else Theme.ColorPink, Theme.Pink
      enemyColumn.AddRow(Display.createCompactCombatantPanel e.Combatant borderColor headerColor (Some player)) |> ignore

    let body = Grid()
    body.AddColumn(GridColumn()) |> ignore
    body.AddColumn(GridColumn()) |> ignore
    body.AddRow(Display.createCombatantPanel player Theme.ColorGreen Theme.Green None, enemyColumn) |> ignore
    body.Expand <- true
    AnsiConsole.Write(body)

    let logGrid = Grid()
    logGrid.AddColumn(GridColumn()) |> ignore
    let recent = logLines |> List.rev |> List.truncate 10 |> List.rev
    if recent.IsEmpty then
      logGrid.AddRow(Markup(sprintf "[%s]Awaiting the first exchange...[/]" Theme.Comment)) |> ignore
    else
      for line in recent do
        logGrid.AddRow(Markup(line)) |> ignore
    logGrid.Expand <- true
    AnsiConsole.Write(
      Panel(logGrid)
        .Header(sprintf "[bold %s] COMBAT LOG [/]" Theme.Cyan)
        .Border(BoxBorder.Rounded)
        .BorderColor(Theme.ColorCurrentLine)
        .Expand()
    )
    AnsiConsole.WriteLine()

  /// Interactive duel against a pack of enemies, modelled on runStoryDuel. The lone player resolves
  /// actions through the group-turn resolver (cleaves/splashes) while each surviving enemy strikes
  /// back in turn. When canFlee, the player may disengage at the cost of one parting strike per foe.
  let private runStoryGroupDuel (floorNumber: int) (player: Combatant) (enemies: TowerEnemy list) (canFlee: bool) : TowerDisplay.GroupDuelResult =
    let rng = Random()
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    let mutable currentPlayer = player
    let mutable currentEnemies = enemies
    let mutable round = 1
    let mutable priorDefenses = 0
    let mutable combatOver = false
    let mutable outcome = TowerDisplay.GroupDuelOutcome.Victorious

    let combatLog = System.Collections.Generic.List<string>()

    let session =
      CombatLogger.startSession
        (sprintf "%s vs the pack [%s]" currentPlayer.Name (currentEnemies |> List.map (fun e -> e.Name) |> String.concat ", "))
        "Story / Tower Group Engagement"
        currentPlayer
        (currentEnemies |> List.tryHead |> Option.map (fun e -> e.Combatant) |> Option.defaultValue currentPlayer)

    let appendTurn (header: string) (events: CombatEvent list) =
      combatLog.Add(sprintf "[bold %s]%s[/]" Theme.Yellow (Markup.Escape header))
      for evt in events do
        combatLog.Add(sprintf "  [grey]• %s[/]" (Markup.Escape (CombatLogger.formatCombatEvent evt)))

    let logLines () = combatLog |> List.ofSeq

    let isLiving (e: TowerEnemy) =
      not e.IsDefeated && not e.Combatant.Health.IsDepleted && not e.Combatant.Morale.IsDepleted

    let living () = currentEnemies |> List.filter isLiving

    while not combatOver && not (living ()).IsEmpty do
      priorDefenses <- 0
      let mutable turnNum = 1
      renderGroupHud currentPlayer currentEnemies round (logLines ())
      let livingNow = living ()
      RuntimeLog.write (sprintf "group combat round %d: %d foe(s) standing" round livingNow.Length)

      // 1. Target selection (only meaningful with more than one foe standing)
      let target =
        match livingNow with
        | [ only ] -> only
        | many ->
          let indexed = many |> List.mapi (fun i e -> (sprintf "%d.  %s" (i + 1) e.Name, e))
          let pick =
            Display.promptSelectionWithHelp
              (sprintf "[bold %s]Choose your target:[/]" Theme.Yellow)
              (indexed |> List.map fst)
              None
              (fun () -> renderGroupHud currentPlayer currentEnemies round (logLines ()))
          indexed |> List.find (fun (label, _) -> label = pick) |> snd

      // 2. Action selection (with an optional disengage)
      let targetCombatant = target.Combatant
      let targetIntent = AI.chooseIntent targetCombatant currentPlayer
      let actionChoices =
        buildActionChoicesWithTelegraph currentPlayer targetCombatant (Some targetIntent)
        @ (if canFlee then [ "🏃  Disengage (withdraw — each foe strikes as you break away)" ] else [])

      let choice =
        Display.promptSelectionWithHelp
          (sprintf "[bold %s]Round %d - Action against %s (Press '?' or F1 for Symbol Legend):[/]" Theme.Yellow round target.Name)
          actionChoices
          (Some 12)
          (fun () -> renderGroupHud currentPlayer currentEnemies round (logLines ()))

      if choice.Contains("Disengage") then
        // Parting strikes from the living enemies that can reach you (eight surrounding tiles at most).
        AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s breaks away, but the pack lashes out![/]" Theme.Yellow currentPlayer.Name)
        let mutable survived = true
        for e in living () |> List.truncate 8 do
          let intent = AI.chooseIntent e.Combatant currentPlayer
          let playerBeforeTurn = currentPlayer
          let r =
            if floorNumber = 1 then
              ActionResolver.resolveWithoutLethal roller intent e.Combatant currentPlayer
            else
              ActionResolver.resolveEx roller intent e.Combatant currentPlayer priorDefenses
          CombatLogger.recordTurn session round turnNum (sprintf "%A" intent) e.Combatant playerBeforeTurn r
          turnNum <- turnNum + 1
          appendTurn (sprintf "Round %d — %s strikes %s (parting blow)" round e.Name currentPlayer.Name) r.Events
          currentPlayer <- r.Target
          currentEnemies <- currentEnemies |> List.map (fun en -> if en.Id = e.Id then { en with Combatant = r.Actor } else en)
          priorDefenses <- priorDefenses + 1
          if currentPlayer.Health.IsDepleted || currentPlayer.Morale.IsDepleted then survived <- false
        if survived then
          outcome <- TowerDisplay.GroupDuelOutcome.Fled
          AnsiConsole.MarkupLine(sprintf "\n[bold %s]You slip away from the pack![/]" Theme.Green)
        else
          outcome <- TowerDisplay.GroupDuelOutcome.Defeated
        combatOver <- true
      else
        // 3. Player action via the group-turn resolver (cleave/splash onto nearby foes)
        let intent = parseActionChoice choice
        let otherLiving = livingNow |> List.filter (fun e -> e.Id <> target.Id) |> List.map (fun e -> e.Combatant)
        AnsiConsole.MarkupLine(sprintf "\n[bold %s]%s executes %s...[/]" Theme.Green currentPlayer.Name choice)
        let playerBefore = currentPlayer
        let targetBefore = targetCombatant
        let res = ActionResolver.resolveGroupTurn roller intent currentPlayer targetCombatant otherLiving
        let isTargetInterrupted =
          res.Events
          |> List.exists (function CombatEvent.ActionInterrupted(_, tId, _, _) when tId = target.Combatant.Id -> true | _ -> false)
        CombatLogger.recordTurn session round turnNum choice playerBefore targetBefore
          { Actor = res.Actor; Target = res.PrimaryTarget; Events = res.Events; Contest = None }
        turnNum <- turnNum + 1
        appendTurn (sprintf "Round %d — %s uses %s on %s" round currentPlayer.Name choice target.Name) res.Events
        currentPlayer <- res.Actor
        currentEnemies <-
          currentEnemies
          |> List.map (fun e ->
            if e.Id = target.Id then { e with Combatant = res.PrimaryTarget }
            else
              match res.SecondaryTargets |> List.tryFind (fun (c: Combatant) -> c.Id = e.Combatant.Id) with
              | Some c -> { e with Combatant = c }
              | None -> e)

        if (living ()).IsEmpty then
          outcome <- TowerDisplay.GroupDuelOutcome.Victorious
          combatOver <- true
          AnsiConsole.WriteLine()
          AnsiConsole.Write(
            Rule(sprintf "[bold %s]★★★ PACK VANQUISHED: %s PREVAILED! ★★★[/]" Theme.Green currentPlayer.Name)
              .Centered()
              .RuleStyle(Theme.StyleGreen)
          )
        else
          // 4. Enemy turn: each surviving foe strikes in deterministic order, capped at 8 attackers
          // (the eight tiles surrounding the player — any extras cannot reach).
          AnsiConsole.MarkupLine(sprintf "\n[bold %s]The pack retaliates...[/]" Theme.Pink)
          for e in living () |> List.truncate 8 do
            if not combatOver then
              let playerBeforeTurn = currentPlayer
              let r =
                if e.Combatant.Id = target.Combatant.Id && isTargetInterrupted then
                  AnsiConsole.MarkupLine(sprintf "\n[bold %s]⚡ %s is staggered from the technique interrupt! Attack defused![/]" Theme.Yellow e.Name)
                  let recoverIntent = RecoveryAction SteadyForm
                  ActionResolver.resolve roller recoverIntent e.Combatant currentPlayer
                elif floorNumber = 1 then
                  let intent = AI.chooseIntent e.Combatant currentPlayer
                  ActionResolver.resolveWithoutLethal roller intent e.Combatant currentPlayer
                else
                  let intent = AI.chooseIntent e.Combatant currentPlayer
                  ActionResolver.resolveEx roller intent e.Combatant currentPlayer priorDefenses
              CombatLogger.recordTurn session round turnNum (sprintf "%A" intent) e.Combatant playerBeforeTurn r
              turnNum <- turnNum + 1
              appendTurn (sprintf "Round %d — %s strikes %s" round e.Name currentPlayer.Name) r.Events
              currentPlayer <- r.Target
              currentEnemies <- currentEnemies |> List.map (fun en -> if en.Id = e.Id then { en with Combatant = r.Actor } else en)
              priorDefenses <- priorDefenses + 1
              let executed = r.Events |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)
              if executed || currentPlayer.Health.IsDepleted || currentPlayer.Morale.IsDepleted then
                outcome <- TowerDisplay.GroupDuelOutcome.Defeated
                combatOver <- true
                AnsiConsole.WriteLine()
                AnsiConsole.Write(
                  Rule(sprintf "[bold %s]☠☠☠ OVERWHELMED: %s HAS FALLEN! ☠☠☠[/]" Theme.Red currentPlayer.Name)
                    .Centered()
                    .RuleStyle(Theme.StyleRed)
                )

      if not combatOver then
        AnsiConsole.WriteLine()
        AnsiConsole.Markup(sprintf "[%s]Press any key to proceed to next round...[/]" Theme.Comment)
        Console.ReadKey(true) |> ignore
        round <- round + 1

    let outcomeStr =
      match outcome with
      | TowerDisplay.GroupDuelOutcome.Victorious -> "PlayerVictorious"
      | TowerDisplay.GroupDuelOutcome.Defeated -> "PlayerDefeated"
      | TowerDisplay.GroupDuelOutcome.Fled -> "PlayerFled"

    let finalOpponent =
      currentEnemies |> List.tryHead |> Option.map (fun e -> e.Combatant) |> Option.defaultValue currentPlayer

    let latestLog, _ = CombatLogger.writeSession session outcomeStr currentPlayer finalOpponent
    RuntimeLog.write (sprintf "group combat end: %s (log %s)" outcomeStr latestLog)
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(sprintf "[dim grey]Tactical combat log saved to: %s[/]" latestLog)

    let finalEnemies =
      currentEnemies
      |> List.map (fun e -> if isLiving e then e else { e with IsDefeated = true })

    { Outcome = outcome; Player = currentPlayer; Enemies = finalEnemies }

  let private createProloguePlayer (className: string) : Combatant =
    StoryBosses.createProloguePlayer className

  let private runInteractiveStory (debug: bool) (forcedSeed: int option) =
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
      "Now, the roar of screaming metal recedes into the steady hiss of rain through black pine boughs.\n" +
      "You peel your face out of the wet loam of a rain-soaked forest. Your past is an empty, dark vault.\n\n" +
      "Beside you among the roots lies a battered chest, its iron lock already broken and hanging loose."

    let introPanel =
      Panel(Markup(sprintf "[bold %s]%s[/]" Theme.Foreground (Markup.Escape introText)))
        .Header(sprintf "[bold %s] PROLOGUE: THE FATAL CROSSING [/]" Theme.Cyan)
        .Border(BoxBorder.Rounded)
        .BorderStyle(Style(foreground = Nullable Theme.ColorCyan))

    AnsiConsole.Write(introPanel)
    AnsiConsole.WriteLine()

    // 2. Scavenger Chest Class Selection
    let choices =
      [ "⚔️ Tempered Arming Sword & Reinforced Shield (Fighter) — Balanced martial master of kinetic momentum, agile parries, and bastion guard"
        "🪄 Carved Ash Staff (Mage) — Arcane practitioner channeling cognitive resonance, phantasmal static, and protective wards" ]

    let choice =
      Display.promptSelectionWithHelp
        (sprintf "[bold %s]Scavenge the battered chest (Choose your weapon armament and awaken your class):[/]" Theme.Yellow)
        choices
        None
        (fun () -> ())

    let chosenClass = if choice.Contains("Fighter") then "fighter" else "mage"

    let player = StoryBosses.createProloguePlayer chosenClass
    let weapon =
      player.EquippedItems
      |> List.map (fun w -> w.Name)
      |> String.concat ", "
    let desc =
      match player.Class with
      | CharacterClass.Mage -> "Arcane Practitioner • Resonance Spikes, Phantasmal Weaving & Aegis Lattices"
      | _ -> "Martial Master • Kinetic Force, Finesse Cadence & Bastion Defense"

    let panel =
      Panel(Markup(sprintf "[bold %s]󰓥 CLASS AWAKENED: %s[/] [grey](%s)[/]\n[italic white]Equipped: %s  •  Combat Stance: %A[/]\n[grey]Bundle of sharpened caltrops recovered inside lid.[/]"
        Theme.Green (player.Class.Name.ToUpperInvariant()) desc (Markup.Escape weapon) player.Stance))
        .Border(BoxBorder.Heavy)
        .BorderStyle(Style(foreground = Nullable Theme.ColorGreen))

    AnsiConsole.Clear()
    AnsiConsole.Write(panel)
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(sprintf "[bold %s]A child's terrified scream cuts through the hiss of the rain and the creak of the pines...[/]" Theme.Yellow)
    AnsiConsole.MarkupLine(sprintf "[italic %s]Ahead lies the rain-soaked forest floor. Navigate, explore, and fight your way to the bandit camp at the cave-mouth descending into the quarry.[/]" Theme.Comment)
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(sprintf "[%s]Press any key to enter the forest and begin your expedition...[/]" Theme.Comment)
    Console.ReadKey(true) |> ignore

    // 3. Launch 2D Spatial Map Loop for Story Campaign
    TowerDisplay.runStoryCrawl player 1 runStoryDuel runStoryGroupDuel DeathAnimation.playTruckReplay debug forcedSeed

  [<EntryPoint>]
  let main (args: string array) =
    let cliOptions, debugOptions = parseCliArgs args

    if args |> Array.exists (fun a -> a = "--verbose" || a = "-v" || a = "--log") then
      RuntimeLog.``begin`` args |> ignore

    let onUnhandled (e: UnhandledExceptionEventArgs) =
      match e.ExceptionObject with
      | :? exn as ex -> RuntimeLog.exn "UNHANDLED EXCEPTION" ex
      | other -> RuntimeLog.write (sprintf "UNHANDLED EXCEPTION (non-exception object): %A" other)

    AppDomain.CurrentDomain.UnhandledException.Add onUnhandled

    RuntimeLog.section "MODE SELECTION"
    RuntimeLog.write (sprintf "mode=%A debug=%b forcedSeed=%A" cliOptions debugOptions.Enabled debugOptions.ForcedSeed)

    try
      match cliOptions with
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
          try
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

            RuntimeLog.write (sprintf "menu choice: %s" (Markup.Remove choice))

            if choice.Contains("Interactive Story Mode") then
              runInteractiveStory debugOptions.Enabled debugOptions.ForcedSeed
            elif choice.Contains("Ascend The Infinite Tower") then
              let playerArch = promptSelectArchetype (sprintf "[bold %s]Select Expedition Champion:[/]" Theme.Yellow)
              let player = playerArch.Factory()
              TowerDisplay.runTowerCrawl player 1 runStoryDuel runStoryGroupDuel DeathAnimation.playTruckReplay debugOptions.Enabled debugOptions.ForcedSeed
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
              // The swarm benchmark reports the apex tier and the Legend tier (level 1000) only, so there
              // is nothing to pick; renderBestiarySwarmMatrix ignores its tier argument.
              Simulation.renderBestiarySwarmMatrix None
              AnsiConsole.MarkupLine(sprintf "[%s]Press any key to return to menu...[/]" Theme.Comment)
              Console.ReadKey(true) |> ignore

            elif choice.Contains("Symbol & Glyph Reference Manual") then
              Display.showSymbolAndGlyphLegend ()

            elif choice.Contains("Exit") then
              running <- false
          with
          | MenuCancelled ->
            AnsiConsole.Cursor.Show(true)

        AnsiConsole.MarkupLine(sprintf "[bold %s]Exiting Fornach Arena. Farewell![/]" Theme.Yellow)
        0
    with e ->
      RuntimeLog.exn "FATAL" e
      AnsiConsole.Write(
        Panel(Markup(sprintf "[bold red]Fatal error:[/] %s" (Markup.Escape e.Message)))
          .Header("[bold red]UNHANDLED RUNTIME ERROR[/]")
          .BorderColor(Color.Red)
      )
      if RuntimeLog.enabled then
        AnsiConsole.MarkupLine(sprintf "[%s]Verbose log written to: %s[/]" Theme.Comment RuntimeLog.path)
      Console.Error.WriteLine(e.ToString())
      1
