namespace Fornach.Cli

open System
open System.Threading
open System.Threading.Tasks
open Spectre.Console
open Spectre.Console.Rendering
open Fornach.Domain
open Fornach.Engine

/// Raised when the user presses Escape to abandon the current menu and return to the main menu.
/// Caught by the interactive menu loop; anything in between is unwound.
exception MenuCancelled

type VimInput(inner: IAnsiConsoleInput, enableHelpHotkey: bool, enableVimKeys: bool) =
  let keyQueue = System.Collections.Generic.Queue<ConsoleKeyInfo>()

  new(inner: IAnsiConsoleInput) = VimInput(inner, false, true)
  new(inner: IAnsiConsoleInput, enableHelpHotkey: bool) = VimInput(inner, enableHelpHotkey, true)

  interface IAnsiConsoleInput with
    member this.IsKeyAvailable() = keyQueue.Count > 0 || inner.IsKeyAvailable()
    member this.ReadKey(intercept: bool) =
      if keyQueue.Count > 0 then
        Nullable (keyQueue.Dequeue())
      else
        let n = inner.ReadKey(intercept)
        if n.HasValue then
          let k = n.Value
          if k.Key = ConsoleKey.Escape then
            raise MenuCancelled
          elif enableHelpHotkey && (k.Key = ConsoleKey.F1 || k.KeyChar = '?') then
            keyQueue.Enqueue(ConsoleKeyInfo(char 13, ConsoleKey.Enter, false, false, false))
            Nullable (ConsoleKeyInfo(char 0, ConsoleKey.End, false, false, false))
          elif not enableVimKeys then
            Nullable k
          else
            let translated =
              match k.Key with
              | ConsoleKey.J -> ConsoleKeyInfo(char 0, ConsoleKey.DownArrow, false, false, false)
              | ConsoleKey.K -> ConsoleKeyInfo(char 0, ConsoleKey.UpArrow, false, false, false)
              | _ ->
                match k.KeyChar with
                | 'j' | 'J' -> ConsoleKeyInfo(char 0, ConsoleKey.DownArrow, false, false, false)
                | 'k' | 'K' -> ConsoleKeyInfo(char 0, ConsoleKey.UpArrow, false, false, false)
                | _ -> k
            Nullable translated
        else
          Nullable()

    member this.ReadKeyAsync(intercept: bool, ct: CancellationToken) =
      task {
        if keyQueue.Count > 0 then
          return Nullable (keyQueue.Dequeue())
        else
          let! n = inner.ReadKeyAsync(intercept, ct)
          if n.HasValue then
            let k = n.Value
            if k.Key = ConsoleKey.Escape then
              return raise MenuCancelled
            elif enableHelpHotkey && (k.Key = ConsoleKey.F1 || k.KeyChar = '?') then
              keyQueue.Enqueue(ConsoleKeyInfo(char 13, ConsoleKey.Enter, false, false, false))
              return Nullable (ConsoleKeyInfo(char 0, ConsoleKey.End, false, false, false))
            elif not enableVimKeys then
              return Nullable k
            else
              let translated =
                match k.Key with
                | ConsoleKey.J -> ConsoleKeyInfo(char 0, ConsoleKey.DownArrow, false, false, false)
                | ConsoleKey.K -> ConsoleKeyInfo(char 0, ConsoleKey.UpArrow, false, false, false)
                | _ ->
                  match k.KeyChar with
                  | 'j' | 'J' -> ConsoleKeyInfo(char 0, ConsoleKey.DownArrow, false, false, false)
                  | 'k' | 'K' -> ConsoleKeyInfo(char 0, ConsoleKey.UpArrow, false, false, false)
                  | _ -> k
              return Nullable translated
          else
            return Nullable()
      }

type VimConsole(inner: IAnsiConsole, enableHelpHotkey: bool, enableVimKeys: bool) =
  let vimInput = VimInput(inner.Input, enableHelpHotkey, enableVimKeys)
  new(inner: IAnsiConsole) = VimConsole(inner, false, true)
  new(inner: IAnsiConsole, enableHelpHotkey: bool) = VimConsole(inner, enableHelpHotkey, true)
  interface IAnsiConsole with
    member this.Profile = inner.Profile
    member this.Cursor = inner.Cursor
    member this.Input = vimInput :> IAnsiConsoleInput
    member this.ExclusivityMode = inner.ExclusivityMode
    member this.Pipeline = inner.Pipeline
    member this.Clear(home) = inner.Clear(home)
    member this.Write(renderable: IRenderable) = inner.Write(renderable)
    member this.WriteAnsi(action: System.Action<AnsiWriter>) = inner.WriteAnsi(action)

module Display =

  /// Strips supplementary-plane glyphs (nerd-font icons such as the tree and reticle marks) from
  /// text destined for a panel header. Spectre measures such a glyph as one cell while it occupies
  /// two UTF-16 units, so it pads the header line one cell too wide per glyph and the top border
  /// overhangs the rest of the box. BMP glyphs are measured correctly and pass through untouched.
  let stripNerdGlyphs (text: string) : string =
    text |> String.filter (fun c -> not (Char.IsSurrogate c))

  /// Executes a prompt with Vim-style navigation enabled (j/J = down, k/K = up)
  let promptWithVim (prompt: IPrompt<'T>) : 'T =
    let prev = AnsiConsole.Console
    try
      AnsiConsole.Console <- VimConsole(prev, false)
      AnsiConsole.Prompt(prompt)
    finally
      AnsiConsole.Console <- prev

  /// Executes a prompt with Vim-style navigation and '?' / F1 help hotkey enabled
  let promptWithVimAndHelp (prompt: IPrompt<'T>) : 'T =
    let prev = AnsiConsole.Console
    try
      AnsiConsole.Console <- VimConsole(prev, true)
      AnsiConsole.Prompt(prompt)
    finally
      AnsiConsole.Console <- prev

  /// Asks for free text where Escape abandons the current menu. Vim navigation stays off so a
  /// typed 'j' or 'k' reaches the field instead of being translated into an arrow key.
  let private askCancellable (prompt: TextPrompt<'T>) : 'T =
    let prev = AnsiConsole.Console
    try
      AnsiConsole.Console <- VimConsole(prev, false, false)
      AnsiConsole.Prompt(prompt)
    finally
      AnsiConsole.Console <- prev

  let askString (message: string, defaultValue: string) : string =
    askCancellable (TextPrompt<string>(message).DefaultValue(defaultValue))

  let askInt (message: string, defaultValue: int) : int =
    askCancellable (TextPrompt<int>(message).DefaultValue(defaultValue))

  /// Widest label any bar/meter row carries (`󰧑 Cognitive Fatigue`), measured in UTF-16 units — the
  /// leading Nerd Font glyph sits outside the BMP so it counts as 2. Every bar pads its label to
  /// this width, which is what puts the bar, the numbers and the percentage in one column across
  /// both the vitals and the strain block. Raise this if a longer label is ever introduced.
  let private barLabelWidth = 20

  let renderBar (label: string) (current: int) (maxVal: int) (colorHex: string) =
    let safeMax = Math.Max(1, maxVal)
    let safeCurr = Math.Clamp(current, 0, safeMax)
    let pctRatio = float safeCurr / float safeMax
    let pctInt = int (Math.Round(pctRatio * 100.0))
    let barUnits = Math.Clamp(int (Math.Round(pctRatio * 7.0)), 0, 7)
    let filled = String('█', barUnits)
    let empty = String('░', 7 - barUnits)

    let dynamicColor, alertTag =
      if label.Contains("HP") || label.Contains("Health") then
        if pctInt > 60 then Theme.Green, ""
        elif pctInt >= 25 then Theme.Yellow, sprintf " [%s](Wounded)[/]" Theme.Yellow
        else Theme.Red, sprintf " [bold blink %s]⚠ CRITICAL[/]" Theme.Red
      elif label.Contains("Morale") then
        if pctInt > 60 then colorHex, ""
        elif pctInt >= 25 then Theme.Yellow, sprintf " [%s](Shaken)[/]" Theme.Yellow
        else Theme.Red, sprintf " [bold blink %s]⚠ BREAKING[/]" Theme.Red
      else
        colorHex, ""

    // Label is padded before interpolation, not by a %-Ns format: `label.Length` overcounts the
    // glyph by one unit, and .NET's width specifier counts that overcount as real width.
    sprintf "%s [%s]%s[/][grey27]%s[/] [bold %s]%4d[/][%s]/[/]%4d [bold %s](%3d%%)[/]%s"
      (label.PadRight barLabelWidth) dynamicColor filled empty dynamicColor safeCurr Theme.Comment safeMax dynamicColor pctInt alertTag

  let renderMeter (label: string) (m: Meter) (colorHex: string) =
    let pct = Math.Clamp(int (Math.Round((float m.Value / 100.0) * 8.0)), 0, 8)
    let filled = String('█', pct)
    let empty = String('░', 8 - pct)
    let warnColor =
      if m.Value >= 75 then Theme.Red
      elif m.Value >= 40 then Theme.Yellow
      else colorHex
    sprintf "%s [%s]%s[/][%s]%s[/] [bold %s]%3d%%[/]"
      (label.PadRight barLabelWidth) warnColor filled Theme.CurrentLine empty Theme.Foreground m.Value

  let createCombatantPanel (c: Combatant) (borderColor: Color) (headerColor: string) (observerOpt: Combatant option) =
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore

    if c.Plane = Mental then
      let formColor, formName =
        match c.ComplexForm with
        | Some ComplexForm.ResonanceSpike -> Theme.Pink, "Resonance Spike (Power)"
        | Some ComplexForm.PhantasmalDiffusion -> Theme.Green, "Phantasmal Diffusion (Agility)"
        | Some ComplexForm.AegisLattice -> Theme.Cyan, "Aegis Lattice (Discipline)"
        | None -> Theme.Comment, "Unthreaded"
      grid.AddRow(Markup(sprintf "%-22s [bold %s]%s[/]" "🧵 Complex Form" formColor formName)) |> ignore
    else
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
      grid.AddRow(Markup(sprintf "%-22s [bold %s]%s[/]" "󰓥 Active Stance" stanceColor stanceName)) |> ignore

    let weaponCondColor, weaponCondDesc =
      match c.WeaponCondition with
      | WeaponCondition.Pristine -> Theme.Green, "Pristine (100% eff)"
      | WeaponCondition.Notched -> Theme.Yellow, "Notched (-10% dmg)"
      | WeaponCondition.Damaged -> Theme.Orange, "Damaged (-25% dmg)"
      | WeaponCondition.Broken -> Theme.Red, "Broken (-50% dmg)"
    grid.AddRow(Markup(sprintf "%-22s [bold %s]%s[/]" "󰚌 Weapon Integrity" weaponCondColor weaponCondDesc)) |> ignore

    // Health & Morale pools
    grid.AddRow(Markup(renderBar "󰋑 HP (Physical)" c.Health.Current c.Health.Maximum Theme.Red)) |> ignore
    grid.AddRow(Markup(renderBar "󰧑 Morale (Mental)" c.Morale.Current c.Morale.Maximum Theme.Cyan)) |> ignore

    // Armor durability & Soak
    let armorPct = int (c.Armor.AbsorptionRatio * 100.0)
    let armorText =
      if c.Armor.IsShredded then
        sprintf "[bold %s]SHREDDED (0%% soak)[/]" Theme.Red
      else
        sprintf "[%s]%d / %d[/] [%s](%d%% soak)[/]" Theme.Comment c.Armor.Current c.Armor.Max Theme.Yellow armorPct
    grid.AddRow(Markup(sprintf "%-22s %s" " Armor Integrity" armorText)) |> ignore

    // Combat ramp: rises on every attack, cashed by a same-vector special.
    if c.Flow > 0 || c.Momentum > 0 || c.Focus > 0 then
      let rampText =
        sprintf
          "[bold %s]Flow %d[/] [%s]/ Momentum %d / Focus %d[/] [grey](+%d%% Finesse/Reflex)[/]"
          Theme.Green
          c.Flow
          Theme.Comment
          c.Momentum
          c.Focus
          (c.Flow * 10)

      grid.AddRow(Markup(sprintf "%-22s %s" " Combat Ramp" rampText)) |> ignore

    if c.BleedStacks > 0 || c.LimbDebuff > 0 || c.ArcaneWard > 0 || c.MirrorClones > 0 then
      let bleedText = if c.BleedStacks > 0 then sprintf "[bold %s]%d Bleed Stacks[/] " Theme.Red c.BleedStacks else ""
      let limbText = if c.LimbDebuff > 0 then sprintf "[%s]-%d Reflex (Crippled)[/] " Theme.Orange c.LimbDebuff else ""
      let wardText = if c.ArcaneWard > 0 then sprintf "[bold %s]🛡️ %d Arcane Ward[/] " Theme.Cyan c.ArcaneWard else ""
      let cloneText = if c.MirrorClones > 0 then sprintf "[bold %s]🪞 %d Mirror Clones[/] " Theme.Purple c.MirrorClones else ""
      grid.AddRow(Markup(sprintf "%-22s %s%s%s%s" "󱁕 Special State" wardText cloneText bleedText limbText)) |> ignore

    // Tactical Assessment (Acumen vs Composure)
    match observerOpt with
    | Some obs ->
      let assess = TacticalAssessment.assess obs c
      let headlineColor =
        match assess.InsightLevel with
        | Penetrating -> Theme.Green
        | Keen -> Theme.Yellow
        | Discerning -> Theme.Orange
        | Obscured -> Theme.Comment

      grid.AddRow(Rule().RuleStyle(Theme.StyleCurrentLine)) |> ignore
      grid.AddRow(Rule(sprintf "[bold %s] 󰓥 TACTICAL ASSESSMENT ── %s [/]" headlineColor (Markup.Escape assess.Headline)).RuleStyle(Theme.StyleCurrentLine)) |> ignore
      grid.AddRow(Markup(sprintf "%-22s %s" "󰈸 Primary Stat" (Markup.Escape assess.PrimarySummary))) |> ignore
      match assess.ThreatWarning with
      | Some threat ->
        grid.AddRow(Markup(sprintf "%-22s [bold %s]%s[/]" "⚠ Apex Threat" Theme.Red (Markup.Escape threat))) |> ignore
      | None -> ()
      match assess.VulnerabilitySummary with
      | Some vuln ->
        grid.AddRow(Markup(sprintf "%-22s %s" "🎯 Defensive Opening" (Markup.Escape vuln))) |> ignore
      | None -> ()
      grid.AddRow(Markup(sprintf "%-22s [italic %s]%s[/]" "💡 Tactical Advice" Theme.Yellow (Markup.Escape assess.StrategicAdvice))) |> ignore
      grid.AddRow(Markup(sprintf "%-22s [grey]%s[/]" "󰄬 Scrutiny vs Tell" (sprintf "Effective Acumen %d vs Composure %d (Ratio: %.2fx)" assess.EffectiveAcumen assess.TargetComposure assess.Ratio))) |> ignore
    | None -> ()

    grid.AddRow(Rule().RuleStyle(Theme.StyleCurrentLine)) |> ignore

    // Shared Entropy & Momentum
    grid.AddRow(Markup(renderMeter "󰈸 Recklessness" c.Meters.Recklessness Theme.Orange)) |> ignore
    // The three ramp meters, in the shorthand players see in the strike menu: Flow / Momentum / Focus.
    let comboText =
      sprintf "[%s]F%d · M%d · C%d ramp[/] | [%s]%d combo[/]" Theme.Purple c.Flow c.Momentum c.Focus Theme.Pink c.ComboTracker.ConsecutiveHits
    grid.AddRow(Markup(sprintf "%-22s %s" "󰓥 Tactical Combo" comboText)) |> ignore
    grid.AddRow(Rule().RuleStyle(Theme.StyleCurrentLine)) |> ignore

    // Physical Status Meters
    grid.AddRow(Markup(renderMeter "󰒓 Exhaustion" c.Meters.Exhaustion Theme.Yellow)) |> ignore
    grid.AddRow(Markup(renderMeter "󰓎 Overwhelm" c.Meters.Overwhelm Theme.Pink)) |> ignore
    grid.AddRow(Markup(renderMeter "󰞷 Frustration" c.Meters.Frustration Theme.Orange)) |> ignore
    grid.AddRow(Rule().RuleStyle(Theme.StyleCurrentLine)) |> ignore

    // Mental / Social Status Meters
    grid.AddRow(Markup(renderMeter "󰧑 Cognitive Fatigue" c.Meters.CognitiveFatigue Theme.Purple)) |> ignore
    grid.AddRow(Markup(renderMeter "󰘚 Confusion" c.Meters.Confusion Theme.Comment)) |> ignore
    grid.AddRow(Markup(renderMeter "󰈸 Provoke" c.Meters.Provoke Theme.Red)) |> ignore

    // Collapse Indicator
    match c.Collapse with
    | CollapseState.Collapsed reason ->
      grid.AddRow(Rule(sprintf "[bold %s on %s] 󰚌 COLLAPSE: %A [/]" Theme.Foreground Theme.Red reason).RuleStyle(Theme.StyleRed)) |> ignore
      grid.AddRow(Markup(sprintf "[bold blink %s]*** TARGET IS EXECUTE ELIGIBLE (75%% Defense Drop) ***[/]" Theme.Red)) |> ignore
    | CollapseState.Stable -> ()

    Panel(grid)
      .Header(sprintf "[bold %s] %s [/]" headerColor c.Name)
      .Border(BoxBorder.Rounded)
      .BorderColor(borderColor)
      .Expand()

  /// Field lines for the compact enemy panel used in multi-enemy engagements: vitals, weapon and
  /// armor integrity, plus — when an observer is supplied — the observer's read of the target's
  /// primary stat and defensive opening. The combatant name is the panel header, so it is omitted.
  let compactCombatantLines (c: Combatant) (observerOpt: Combatant option) : string list =
    let weaponCondColor, weaponCondDesc =
      match c.WeaponCondition with
      | WeaponCondition.Pristine -> Theme.Green, "Pristine (100% eff)"
      | WeaponCondition.Notched -> Theme.Yellow, "Notched (-10% dmg)"
      | WeaponCondition.Damaged -> Theme.Orange, "Damaged (-25% dmg)"
      | WeaponCondition.Broken -> Theme.Red, "Broken (-50% dmg)"

    let armorPct = int (c.Armor.AbsorptionRatio * 100.0)
    let armorText =
      if c.Armor.IsShredded then
        sprintf "[bold %s]SHREDDED (0%% soak)[/]" Theme.Red
      else
        sprintf "[%s]%d / %d[/] [%s](%d%% soak)[/]" Theme.Comment c.Armor.Current c.Armor.Max Theme.Yellow armorPct

    let coreLines =
      [ renderBar "󰋑 HP (Physical)" c.Health.Current c.Health.Maximum Theme.Red
        renderBar "󰧑 Morale (Mental)" c.Morale.Current c.Morale.Maximum Theme.Cyan
        sprintf "%-22s [bold %s]%s[/]" "󰚌 Weapon Integrity" weaponCondColor weaponCondDesc
        sprintf "%-22s %s" " Armor Integrity" armorText ]

    let assessmentLines =
      match observerOpt with
      | Some obs ->
        let assess = TacticalAssessment.assess obs c
        let primary =
          sprintf "%-22s %s" "󰈸 Primary Stat" (Markup.Escape assess.PrimarySummary)
        let threatLine =
          match assess.ThreatWarning with
          | Some t -> [ sprintf "%-22s [bold %s]%s[/]" "⚠ Apex Threat" Theme.Red (Markup.Escape t) ]
          | None -> []
        let vulnLine =
          match assess.VulnerabilitySummary with
          | Some vuln -> [ sprintf "%-22s %s" "🎯 Opening" (Markup.Escape vuln) ]
          | None -> []
        [ primary ] @ threatLine @ vulnLine
      | None -> []

    coreLines @ assessmentLines

  /// Wraps compactCombatantLines in a panel styled like createCombatantPanel, for the enemy column
  /// of a multi-enemy engagement HUD.
  let createCompactCombatantPanel (c: Combatant) (borderColor: Color) (headerColor: string) (observerOpt: Combatant option) =
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore
    for line in compactCombatantLines c observerOpt do
      grid.AddRow(Markup(line)) |> ignore
    Panel(grid)
      .Header(sprintf "[bold %s] %s [/]" headerColor c.Name)
      .Border(BoxBorder.Rounded)
      .BorderColor(borderColor)
      .Expand()

  /// Formats an autonomous combatant's telegraphed action intent into:
  /// (title: string, description: string, counterplayTip: string, vectorTag: string)
  let formatTelegraphedIntent (intent: ActionIntent) (actor: Combatant) : string * string * string * string =
    match intent with
    | StandardAttack atk ->
      match atk with
      | ForceStrike true ->
        sprintf "⚡ [bold %s]HEAVY POWER WINDUP: Wild Blow[/]" Theme.Red,
        "Coiling muscles for a devastating, reckless cleave (+30 Recklessness)!",
        sprintf "Disrupt with [bold %s]Shield Bash[/] or slip past with [bold %s]Agility Evasion[/]!" Theme.Cyan Theme.Green,
        "Power"
      | ForceStrike false ->
        sprintf "⚔️ [%s]Force Cleave[/]" Theme.Red,
        "Advancing with a heavy kinetic cleave against Fortitude.",
        sprintf "Counter with [bold %s]Agility Evasion[/] or brace with [bold %s]Discipline Stance[/]." Theme.Green Theme.Purple,
        "Power"
      | FinesseCadence true ->
        sprintf "⚡ [bold %s]RAPID AGILITY FLURRY: Relentless Blitz[/]" Theme.Green,
        "Stepping into an aggressive, relentless multi-thrust opening (+25 Recklessness)!",
        sprintf "Brace with [bold %s]Discipline Bastion / AoO[/] or parry with [bold %s]Raise Shield[/]!" Theme.Purple Theme.Cyan,
        "Agility"
      | FinesseCadence false ->
        sprintf "⚔️ [%s]Finesse Cadence[/]" Theme.Green,
        "Testing distance with rapid probing thrusts against Reflex.",
        sprintf "Intercept with [bold %s]Discipline Guard[/] to build the combat ramp." Theme.Purple,
        "Agility"
      | ProwessStrike true ->
        sprintf "⚡ [bold %s]MARTIAL TRAP: Invitational Bait[/]" Theme.Purple,
        "Lowering guard deliberately to bait a reckless overextension (+35 Recklessness)!",
        sprintf "Break the stance with [bold %s]Power Cleave[/] or match technique with [bold %s]Insight Strike[/]!" Theme.Red Theme.Yellow,
        "Discipline"
      | ProwessStrike false ->
        sprintf "⚔️ [%s]Prowess Pressure[/]" Theme.Purple,
        "Circling with measured poise to probe flaws and build the combat ramp.",
        sprintf "Overwhelm defensive poise with [bold %s]Power Guard-Break[/]." Theme.Red,
        "Discipline"
      | ShieldBash true ->
        sprintf "⚡ [bold %s]SHIELD SLAM WINDUP[/]" Theme.Purple,
        "Bracing shield forward for a bone-shattering charge!",
        sprintf "Dodge sideways with [bold %s]Agility Finesse[/]!" Theme.Green,
        "Discipline"
      | ShieldBash false ->
        sprintf "🛡️ [%s]Shield Bash[/]" Theme.Purple,
        "Advancing behind a raised guard to batter posture.",
        sprintf "Out-range or strike through with [bold %s]Power Cleave[/]." Theme.Red,
        "Discipline"
      | CalculatedFlawStrike ->
        sprintf "🎯 [bold %s]DISCIPLINE GAMBIT: Calculated Flaw Strike[/]" Theme.Purple,
        "Exploiting a studied vital opening with pinpoint precision!",
        "Deflect or mitigate by recovering posture.",
        "Discipline"
      | MasterfulDisarm _ ->
        sprintf "⚡ [bold %s]DISARM ATTEMPT: Masterful Disarm[/]" Theme.Purple,
        "Twisting weapon leverage to wrest your weapon away!",
        "Maintain distance or reset stance balance.",
        "Discipline"
      | ForceInsightStrike ->
        sprintf "🎯 [bold %s]POWER GAMBIT: Insight Cleave[/]" Theme.Red,
        "Unleashing accumulated study into a read opening!",
        "Evasive maneuvers or emergency recovery.",
        "Power"
      | FinesseInsightStrike ->
        sprintf "🎯 [bold %s]AGILITY GAMBIT: Threaded Opening[/]" Theme.Green,
        "Threading a lethal strike through an exposed seam!",
        "Brace guard and close vital openings.",
        "Agility"
      | AuthorityDecree true ->
        sprintf "⚡ [bold %s]SOCIAL DOMINANCE: Imperious Demand[/]" Theme.Pink,
        "Projecting overwhelming psychic authority (+30 Recklessness)!",
        "Disrupt focus or counter with dialectical pressure.",
        "Power"
      | AuthorityDecree false ->
        sprintf "👑 [%s]Authority Decree[/]" Theme.Pink,
        "Asserting command presence against Resolve.",
        "Steel mental composure.",
        "Power"
      | GuileDeception _ ->
        sprintf "🎭 [%s]Guile Misdirection[/]" Theme.Comment,
        "Weaving rhetorical deception and perceptual traps.",
        "Rely on disciplined intuition.",
        "Agility"
      | AcumenInterrogation _ ->
        sprintf "⚖️ [%s]Acumen Interrogation[/]" Theme.Purple,
        "Setting procedural traps to provoke frustration.",
        "Avoid reckless emotional outbursts.",
        "Discipline"
      | ArcaneCataclysm true ->
        sprintf "⚡ [bold %s]ARCANE OVERCHANNEL: Destructive Rupture[/]" Theme.Pink,
        "Channeling turbulent, raw elemental fury (+35 Recklessness)!",
        sprintf "Disrupt immediately with [bold %s]Shield Bash[/] or bypass with [bold %s]Agility Glamour[/]!" Theme.Cyan Theme.Purple,
        "Power"
      | ArcaneCataclysm false ->
        sprintf "✨ [%s]Arcane Cataclysm[/]" Theme.Pink,
        "Gathering focused elemental energy against Resolve.",
        sprintf "Weave [bold %s]Arcane Ward[/] or disrupt cast tempo." Theme.Cyan,
        "Power"
      | SynapticGlamour true ->
        sprintf "⚡ [bold %s]PSYCHIC FRACTURE: Neural Static[/]" Theme.Purple,
        "Broadcasting blinding psychic dissonance (+25 Recklessness)!",
        sprintf "Anchor focus with [bold %s]Center Mind[/] or [bold %s]Runic Ward Trap[/]!" Theme.Cyan Theme.Cyan,
        "Agility"
      | SynapticGlamour false ->
        sprintf "✨ [%s]Synaptic Glamour[/]" Theme.Purple,
        "Sending subtle sensory ripples against Intuition.",
        "Hold mental balance.",
        "Agility"
      | MirrorIllusion _ ->
        sprintf "🪞 [%s]Mirror Mirage Weave[/]" Theme.Purple,
        "Splitting reflections into phantasmal decoys.",
        "Area sweep or disciplined observation.",
        "Agility"
      | RunicWardTrap true ->
        sprintf "⚡ [bold %s]ARCANE TRAP: Anomalous Glyph[/]" Theme.Cyan,
        "Etching a reactive abjuration trap in the ether (+30 Recklessness)!",
        "Break through with overwhelming Power.",
        "Discipline"
      | RunicWardTrap false ->
        sprintf "🛡️ [%s]Runic Ward Trap[/]" Theme.Cyan,
        "Erecting an abjuration ward barrier.",
        "Shatter ward with raw power.",
        "Discipline"
      | DisorientingShockwave _ ->
        sprintf "🌀 [%s]Disorienting Shockwave[/]" Theme.Orange,
        "Pulsing psychic shockwaves to break tempo and posture.",
        "Maintain grounded poise.",
        "Discipline"
      | StaffSiphon ->
        sprintf "✨ [bold %s]Staff Siphon: Runic Clarity[/]" Theme.Cyan,
        "Channeling the staff to drain morale and siphon mental clarity.",
        "Interrupt or break engagement.",
        "Discipline"
      | TraumaAttack BasaltEruption ->
        sprintf "🌋 [bold %s]TRAUMA GAMBIT: Basalt Eruption[/]" Theme.Red,
        "Volcanic rage swelling to pulverize armor and flesh!",
        sprintf "Evade or interrupt with [bold %s]Shield Bash[/]!" Theme.Cyan,
        "Power"
      | TraumaAttack DenialPhaseShift ->
        sprintf "🌫️ [bold %s]TRAUMA GAMBIT: Denial Phase Shift[/]" Theme.Green,
        "Shifting phase through phantasmal decoys!",
        "Discipline containment or keen focus.",
        "Agility"
      | TraumaAttack CoerciveBargain ->
        sprintf "⚖️ [bold %s]TRAUMA GAMBIT: Coercive Bargain[/]" Theme.Purple,
        "Enforcing a predatory psychic contract to drain Morale!",
        "Break contract with relentless force.",
        "Discipline"
      | TraumaAttack ApathyDoldrums ->
        sprintf "⚓ [bold %s]TRAUMA GAMBIT: Apathy Doldrums[/]" Theme.Comment,
        "Drowning focus in numbing cognitive apathy!",
        "Venting breath or steeled resolve.",
        "Discipline"
      | TraumaAttack SereneResolution ->
        sprintf "🕊️ [bold %s]Pacifist Release: Serene Resolution[/]" Theme.Cyan,
        "Accepting the inevitable, calming all turbulent entropy.",
        "Hold your ground.",
        "Discipline"
    | RecoveryAction SteadyForm ->
      sprintf "🛡️ [%s]Steady Form (Recovery)[/]" Theme.Yellow,
      "Settling posture to vent all strain and restore composure.",
      "Seize the opening with an aggressive strike!",
      "Defensive"
    | RecoveryAction RaiseShield ->
      sprintf "🛡️ [bold %s]Raise Shield (Bastion Posture)[/]" Theme.Green,
      "Tucking behind the shield to restore armor and prepare counter-ripostes.",
      "Break guard with heavy Power cleave!",
      "Defensive"
    | RecoveryAction CenterMind ->
      sprintf "🧠 [%s]Center Mind (Recovery)[/]" Theme.Cyan,
      "Recentering mental focus to bleed fatigue and restore wards.",
      "Strike while somatic guard is resetting!",
      "Defensive"
    | ExecuteStrike plane ->
      sprintf "☠️ [bold blink %s]FATAL EXECUTION ATTEMPT (%A)[/]" Theme.Red plane,
      "Targeting a collapsed opening to deliver a fatal coup-de-grâce!",
      "Desperate emergency recovery required!",
      "Finisher"
    | ShiftStance stance ->
      sprintf "↺ [%s]Stance Shift: %A[/]" Theme.Purple stance,
      sprintf "Transitioning combat stance to %A." stance,
      "Adapt tactical counters to new stance.",
      "Stance"
    | ThreadComplexForm form ->
      sprintf "🧵 [%s]Form Thread: %s[/]" Theme.Cyan form.Name,
      sprintf "Attuning mental complex form to %s." form.Name,
      "Counter spell school vector.",
      "ComplexForm"
    | DeployPreparation (prep, _) ->
      sprintf "🎒 [%s]Deploying Preparation: %A[/]" Theme.Orange prep,
      "Deploying tactical asset.",
      "Prepare for tactical shift.",
      "Preparation"

  let renderHUDWithTelegraph (player: Combatant) (enemy: Combatant) (roundNumber: int) (enemyIntentOpt: ActionIntent option) =
    let pnlPlayer = createCombatantPanel player Theme.ColorGreen Theme.Green None
    let pnlEnemy = createCombatantPanel enemy Theme.ColorPink Theme.Pink (Some player)
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore
    grid.AddColumn(GridColumn()) |> ignore
    grid.AddRow(pnlPlayer, pnlEnemy) |> ignore
    grid.Expand <- true
    AnsiConsole.Clear()
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]󰓥 Fornach Duel Arena ── Round %d[/]" Theme.Yellow roundNumber)
        .LeftJustified()
        .RuleStyle(Theme.StylePurple)
    )
    AnsiConsole.Write(grid)

    match enemyIntentOpt with
    | Some intent ->
      let title, desc, tip, _ = formatTelegraphedIntent intent enemy
      let telegraphGrid = Grid()
      telegraphGrid.AddColumn(GridColumn()) |> ignore
      telegraphGrid.AddRow(Markup(sprintf "  [bold %s]󰓥 Telegraphed Windup:[/] %s" Theme.Yellow title)) |> ignore
      telegraphGrid.AddRow(Markup(sprintf "  [grey]%s[/]" desc)) |> ignore
      telegraphGrid.AddRow(Markup(sprintf "  [italic %s]💡 Counterplay:[/] %s" Theme.Green tip)) |> ignore
      let banner =
        Panel(telegraphGrid)
          .Header(sprintf "[bold %s] ⚡ ENEMY TELEGRAPHED INTENT ── READ THE WINDUP [/]" Theme.Yellow)
          .Border(BoxBorder.Double)
          .BorderColor(Theme.ColorYellow)
          .Expand()
      AnsiConsole.Write(banner)
    | None -> ()

    AnsiConsole.WriteLine()

  let renderHUD (player: Combatant) (enemy: Combatant) (roundNumber: int) =
    renderHUDWithTelegraph player enemy roundNumber None

  let renderRollBreakdown (actionName: string) (actorName: string) (contestOpt: ContestResult option) =
    match contestOpt with
    | None -> ()
    | Some contest ->
      let atk = contest.Attacker
      let def = contest.Defender
      let tierMult = ActionResolver.computeTierMultiplier contest.NetHits

      let table = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine).Expand()
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
          .Header(stripNerdGlyphs (sprintf "[bold %s] 󰓥 Tactical Contest Inspection [/]" Theme.Purple))
          .Border(BoxBorder.Rounded)
          .BorderColor(Theme.ColorCurrentLine)
          .Expand()

      AnsiConsole.Write(summaryPanel :> IRenderable)
      AnsiConsole.WriteLine()

  /// Totals the damage events that landed on one combatant, grouped by plane. Damage events name
  /// whoever they actually hit, so filtering by TargetId is what stops a counter-attack, reflect or
  /// ward burn from being attributed to the wrong combatant's line.
  let damageTotals (targetId: CombatantId) (events: CombatEvent list) : (Plane * int) list =
    events
    |> List.choose (function
      | CombatEvent.DamageApplied d when d.TargetId = targetId -> Some d
      | _ -> None)
    |> List.groupBy (fun d -> d.Plane)
    |> List.map (fun (plane, hits) -> plane, hits |> List.sumBy (fun d -> d.Amount))

  /// Reports the damage a resolved action actually applied, so the contest output reads as
  /// numbers instead of a bare tier multiplier. Per-plane totals come from the emitted
  /// DamageApplied events filtered to this target (each event caps at what the pool lost, so
  /// a finisher does not report four-digit overkill); the vitals swing beside them is the
  /// pool-applied truth.
  /// Formats one damage event for the multi-enemy combat log. The target is always named, because an
  /// unattributed "42 Physical" is unreadable in a pack. Bracket literals are doubled: Spectre reads
  /// a single-bracketed word as a style tag and throws on it.
  let formatDamageLogLine (nameOf: CombatantId -> string) (targetIsPlayer: bool) (d: DamageEvent) : string =
    let crit = if d.IsCritical then sprintf " [bold %s][[CRITICAL]][/]" Theme.Yellow else ""
    let comp = if d.IsArmorCompromised then sprintf " [%s][[ARMOR COMPROMISED]][/]" Theme.Orange else ""
    let nameColor = if targetIsPlayer then Theme.Red else Theme.Pink
    sprintf "  [grey]•[/] [bold %s]%s[/] takes [bold %s]%d %A[/]%s%s"
      nameColor (Markup.Escape (nameOf d.TargetId)) Theme.Red d.Amount d.Plane crit comp

  let logDamageSummary (events: CombatEvent list) (targetBefore: Combatant) (targetAfter: Combatant) =
    let applied =
      events
      |> List.choose (function
        | CombatEvent.DamageApplied d when d.TargetId = targetBefore.Id -> Some d
        | _ -> None)

    let planeTotals =
      damageTotals targetBefore.Id events
      |> List.map (fun (plane, total) ->
        let color = if plane = Physical then Theme.Red else Theme.Cyan
        sprintf "[bold %s]%d %A[/]" color total plane)
      |> String.concat " + "

    let damageText =
      if applied.IsEmpty then sprintf "[%s]no damage applied[/]" Theme.Comment
      else
        let crits = applied |> List.filter (fun d -> d.IsCritical) |> List.length
        let critTag = if crits > 0 then sprintf " [bold %s](%d critical)[/]" Theme.Yellow crits else ""
        sprintf "%s [%s](%d hit%s)[/]%s" planeTotals Theme.Comment applied.Length (if applied.Length = 1 then "" else "s") critTag

    let vitalsDelta (label: string) (color: string) (before: Pool) (after: Pool) =
      let lost = before.Current - after.Current
      // A resolve can also *raise* a pool (upkeep healing, a shrine pulse): print nothing then.
      if lost <= 0 then ""
      else sprintf "  |  [%s]%s[/] %d → %d [bold %s](-%d)[/]" color label before.Current after.Current color lost

    AnsiConsole.MarkupLine(
      sprintf "  [bold %s]⚔ Damage to %s:[/] %s%s%s"
        Theme.Yellow (Markup.Escape targetBefore.Name) damageText
        (vitalsDelta "HP" Theme.Red targetBefore.Health targetAfter.Health)
        (vitalsDelta "Morale" Theme.Cyan targetBefore.Morale targetAfter.Morale))

  let logEvent (evt: CombatEvent) =
    match evt with
    | CombatEvent.DamageApplied d ->
      let color = if d.Plane = Physical then Theme.Red else Theme.Cyan
      let critText = if d.IsCritical then sprintf " [bold %s]** CRITICAL STRIKE **[/]" Theme.Yellow else ""
      let armorText = if d.IsArmorCompromised then sprintf " [italic %s](Armor Compromised)[/]" Theme.Orange else ""
      AnsiConsole.MarkupLine(sprintf "  [bold %s]󰁔[/] Dealt [bold %s]%d %A damage[/]%s%s" color color d.Amount d.Plane critText armorText)

    | CombatEvent.DisparityTriggered (_, _, outcome) ->
      match outcome with
      | CrushingBlow bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]󰈸 DISPARITY:[/] Crushing Blow! Target suffered [bold %s]+%d Exhaustion[/] and is staggered!" Theme.Yellow Theme.Yellow bonus)
      | ArterialRupture bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]󰈸 DISPARITY:[/] Arterial Rupture! Target suffered [bold %s]+%d Overwhelm / Hemorrhage[/]!" Theme.Pink Theme.Pink bonus)
      | DisarmOrLimbDisable ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]󰈸 DISPARITY:[/] Disarm & Disable! Target's weapon arm posture compromised!" Theme.Orange)
      | CognitiveRupture bonus ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]󰈸 DISPARITY:[/] Cognitive Rupture! Target's psychic ward collapsed (+%d Fatigue)!" Theme.Purple bonus)
      | DialecticalParalysis ->
        AnsiConsole.MarkupLine(sprintf "  [bold %s]󰈸 DISPARITY:[/] Dialectical Paralysis! Target is locked in logical contradiction!" Theme.Purple)
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

    | CombatEvent.GambitDeclared (_, name, cost) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ GAMBIT DECLARED:[/] [bold underline %s]%s[/] (+%d Recklessness)" Theme.Orange Theme.Orange name cost)

    | CombatEvent.GambitPunished (_, reason) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]✖ GAMBIT PUNISHED:[/] %s" Theme.Red reason)

    | CombatEvent.FormStabilized (_, drained, gained) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]✓ FORM STABILIZED:[/] Drained [bold %s]%d Recklessness[/], gained [bold %s]%d ramp stacks[/]." Theme.Green Theme.Green drained Theme.Purple gained)

    | CombatEvent.BreathStabilized (_, summary, morale) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🌬 STEADY BREATHING:[/] %s (+[bold %s]%d Morale[/])." Theme.Cyan summary Theme.Green morale)

    | CombatEvent.CollapseRecovered _ ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]✦ POSTURE RECOVERED:[/] All strain meters vented below critical thresholds; collapsed state cleared!" Theme.Green)

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
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🎯 DISCIPLINE GAMBIT:[/] [bold underline %s]%s[/] (Expended %d meter stacks, 0 Recklessness)." Theme.Purple Theme.Purple name spent)

    | CombatEvent.EncirclementPenalized (_, priorDefenses, penalty) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🛡️ ENCIRCLED:[/] Flanked by attack #%d! Disparity penalty: [bold %s]-%d Defense Hits[/]!" Theme.Orange (priorDefenses + 1) Theme.Red penalty)

    | CombatEvent.AttackOfOpportunityTriggered (_, _, vectorName, dmg, disrupted) ->
      let statusStr = if disrupted then sprintf " [bold %s](Flank Intercepted & Attack Defused!)[/]" Theme.Green else ""
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ ATTACK OF OPPORTUNITY (%s):[/] Flank attempt punished! Dealt [bold %s]%d reactive damage[/]!%s" Theme.Cyan vectorName Theme.Red dmg statusStr)

    | CombatEvent.CleaveExecuted (_, _, dmg, reck) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚔ CLEAVE:[/] Wide sweeping strike cleaved adjacent target for [bold %s]%d damage[/]! (Disparity Recklessness: [bold %s]+%d[/])" Theme.Red Theme.Red dmg Theme.Orange reck)

    | CombatEvent.StrikeChained (_, _, step, dmg) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ CHAIN FLOW #%d:[/] Disciplined cadence flowed into adjacent target for [bold %s]%d damage[/]! (0 Recklessness)" Theme.Purple step Theme.Red dmg)

    | CombatEvent.ArcaneStrainIncurred (_, spellName, strain, profPct) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ ARCANE STRAIN:[/] Off-specialization cast of [bold underline %s]%s[/] (%d%% proficiency) caused [bold %s]+%d Cognitive Fatigue[/]!" Theme.Purple Theme.Cyan spellName profPct Theme.Purple strain)

    | CombatEvent.MirrorClonesConjured (_, count, total) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🪞 MIRROR ILLUSION:[/] Conjured [bold %s]+%d phantasmal mirror decoys[/] (Total Active: [bold %s]%d[/])." Theme.Purple Theme.Cyan count Theme.Purple total)

    | CombatEvent.MirrorCloneDecoyed (_, _, remaining) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🪞 DECOY SHATTERED:[/] Incoming strike was deceived and absorbed by a mirror clone! (Remaining: [bold %s]%d[/])" Theme.Cyan Theme.Purple remaining)

    | CombatEvent.MirrorCloneShattered (_, _, blastDamage, remaining) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]💥 DECOY SHATTERED:[/] Mirror clone shattered violently upon contact, blasting attacker for [bold %s]%d Morale damage[/]! (Remaining: [bold %s]%d[/])" Theme.Orange Theme.Yellow blastDamage Theme.Purple remaining)


    | CombatEvent.ArcaneWardErected (_, added, total) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🛡️ ARCANE WARD:[/] Abjuration barrier reinforced by [bold %s]+%d[/] (Active Barrier: [bold %s]%d[/])." Theme.Cyan Theme.Green added Theme.Cyan total)

    | CombatEvent.ArcaneWardAbsorbed (_, soaked, remaining) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🛡️ WARD ABSORPTION:[/] Arcane barrier absorbed [bold %s]%d damage[/]! (Barrier Remaining: [bold %s]%d[/])" Theme.Cyan Theme.Yellow soaked Theme.Cyan remaining)

    | CombatEvent.OpponentDisoriented (_, _, reason) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🌀 DISORIENTED:[/] %s" Theme.Orange reason)

    | CombatEvent.CataclysmSplashed (_, _, dmg) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]💥 CATACLYSM SPLASH:[/] Overwhelming arcane rupture splashed [bold %s]%d mental damage[/] to adjacent target!" Theme.Purple Theme.Cyan dmg)

    | CombatEvent.PreparationDeployed (_, prepType, _, desc) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🎯 PREPARATION DEPLOYED:[/] [bold underline %s]%s[/] - %s" Theme.Green Theme.Cyan prepType.Name desc)

    | CombatEvent.ShockwaveSurplusDamage (_, _, surplusHits, flatDmg) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]💥 SHOCKWAVE SLAM:[/] Surplus NetHits (%d) erupted for [bold %s]%d flat damage[/] to flanker!" Theme.Orange surplusHits Theme.Red flatDmg)

    | CombatEvent.SynapticBrandTriggered (_, _, bonusDmg) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🧠 SYNAPTIC BRAND:[/] Critical rupture detonated brand for [bold %s]+%d Morale damage[/]!" Theme.Purple Theme.Pink bonusDmg)

    | CombatEvent.ConcealedBladeCounter (_, _, dmg, disrupted) ->
      let statusStr = if disrupted then sprintf " [bold %s](Incoming strike intercepted & defused!)[/]" Theme.Green else ""
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🗡️ CONCEALED BLADE:[/] Quick-draw boot blade counter punctured for [bold %s]%d damage[/]!%s" Theme.Yellow Theme.Red dmg statusStr)

    | CombatEvent.PrismaticFlareBlinded (_, reckSpike, drain) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]✨ PRISMATIC FLARE:[/] Recklessness spike (+%d) ignited a blinding burst for [bold %s]%d Morale shock[/] (+20 Confusion)!" Theme.Yellow reckSpike Theme.Pink drain)

    | CombatEvent.MirrorMirageDeceived (_, _, confusion) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🪞 MIRROR MIRAGE:[/] Flanker struck an illusion! Incurred [bold %s]+%d Confusion[/] and lost action!" Theme.Cyan Theme.Purple confusion)

    | CombatEvent.IndesSeized (_, _, thresh, margin) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ INDES SEIZURE:[/] Defenses overwhelmed attack (Margin: %d >= Threshold: %d); seized the Vor!" Theme.Yellow margin thresh)

    | CombatEvent.BastionZoneErected _ ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🏰 BASTION ZONE:[/] Polearm planted! Limits simultaneous attackers strictly to 3 (frontline choke)!" Theme.Cyan)

    | CombatEvent.SocraticDossierExecuted (_, _, reckConverted, moraleDmg) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]📜 SOCRATIC DOSSIER:[/] Dialectical trap converted %d Recklessness into [bold %s]%d unmitigated Morale damage[/]!" Theme.Purple reckConverted Theme.Pink moraleDmg)

    | CombatEvent.HeraldicTreatiseRead (_, stacks) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]📖 HERALDIC TREATISE:[/] Tactical dossiers reviewed; granted [bold %s]+%d ramp stacks[/] on all visible foes!" Theme.Purple Theme.Green stacks)

    | CombatEvent.PsychicHemorrhageInflicted (_, _, bleedStacks, disparity) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🩸 CRANIAL HEMORRHAGE:[/] Overwhelming cognitive disparity (%+d) ruptured cerebral vessels! Inflicted [bold %s]+%d Bleed Stacks[/]!" Theme.Red disparity Theme.Red bleedStacks)

    | CombatEvent.PsychicShockwaveResonated (_, _, dmg) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🔮 PSYCHIC RESONANCE:[/] Overwhelming mental shockwave resonated into flanker for [bold %s]%d damage[/]!" Theme.Purple Theme.Cyan dmg)

    | CombatEvent.PhantasmalSwapExecuted (_, _, success, note) ->
      let color = if success then Theme.Cyan else Theme.Orange
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🪞 PHANTASMAL SWAP:[/] %s" color note)

    | CombatEvent.DestabilizingWardTriggered (_, _, outcome, _) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🛡️ DESTABILIZING WARD:[/] %s" Theme.Yellow outcome)

    | CombatEvent.SynapticMindShockDisrupted (_, _, fatigue, disrupted) ->
      let statusStr = if disrupted then " [bold #ff5555](Incoming strike completely shattered!)[/]" else ""
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🧠 SYNAPTIC MIND-SHOCK:[/] Mind-shock disrupted attacker's focus! Inflicted [bold %s]+%d Cognitive Fatigue[/]!%s" Theme.Purple Theme.Pink fatigue statusStr)

    | CombatEvent.PassiveGenerationTriggered (_, desc) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]✦ CLASS MASTERY:[/] %s" Theme.Green desc)

    | CombatEvent.RetributionReflected (_, _, dmg, frust) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚔️ RETRIBUTION AURA:[/] Barrier reflected [bold %s]%d radiant damage[/] back to attacker! Inflicted [bold %s]+%d Frustration[/]!" Theme.Yellow Theme.Cyan dmg Theme.Orange frust)

    | CombatEvent.DestabilizingWardTripped (_, _, dmg, frust) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ RUNIC INSTABILITY:[/] Attacker stumbled onto destabilizing ward! Suffered [bold %s]%d impact damage[/] and [bold %s]+%d Frustration[/]!" Theme.Orange Theme.Red dmg Theme.Orange frust)

    | CombatEvent.EnrageDamageShrugged (_, ignored) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🩸 ENRAGE SHRUG:[/] Pain deadened by stimulant! Shrugged off [bold %s]%d physical damage[/]!" Theme.Red Theme.Yellow ignored)

    | CombatEvent.ComposureDamageShrugged (_, ignored) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🛡️ DISCIPLINE DEFENSE:[/] Guard steeled by martial discipline! Deflected [bold %s]%d incoming damage[/]!" Theme.Yellow Theme.Cyan ignored)

    | CombatEvent.FrenzyStrikeExecuted (_, _) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ FRENZY ATTACK:[/] Enraged bloodlust triggered an immediate savage follow-up swing!" Theme.Orange)

    | CombatEvent.ComplexFormThreaded (_, oldFormOpt, newForm) ->
      let oldName = oldFormOpt |> Option.map (fun f -> f.Name) |> Option.defaultValue "None"
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🧵 COMPLEX FORM:[/] Threaded [bold underline %s]%s[/] (Previous: %s)." Theme.Cyan Theme.Pink newForm.Name oldName)

    | CombatEvent.FadingDrainSuffered (_, formName, fatigueDrain, reckSpike) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]⚡ FADING DRAIN:[/] Channeling [bold %s]%s[/] burned through mental reserves (+%d Cognitive Fatigue, +%d Recklessness)!" Theme.Purple Theme.Cyan formName fatigueDrain reckSpike)

    | CombatEvent.MonsterTraitTriggered (_, traitName, effectDesc) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🐾 TRAIT ([%s]%s[/]):[/] %s" Theme.Purple Theme.Yellow traitName effectDesc)

    | CombatEvent.AcidicArmorCorroded (_, corrosion) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🧪 ACIDIC CORROSION:[/] Searing acidic blood corroded [bold %s]-%d Armor durability[/]!" Theme.Green Theme.Red corrosion)

    | CombatEvent.MoltenBurnInflicted (_, burn) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🔥 MOLTEN AURA:[/] Searing volcanic heat scorched attacker for [bold %s]%d burn damage[/]!" Theme.Orange Theme.Red burn)

    | CombatEvent.AlchemicalTrophyHarvested (_, trophyName, value) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]💎 TROPHY HARVESTED:[/] Obtained [bold %s]%s[/] (Essence Value: [bold %s]%d[/])!" Theme.Cyan Theme.Yellow trophyName Theme.Cyan value)

    | CombatEvent.ActionInterrupted (_, _, tech, reason) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🛡️ TECHNIQUE COUNTER (%s):[/] [bold %s]%s[/]" Theme.Cyan tech Theme.Yellow reason)

    | CombatEvent.StaffSiphoned (_, _, fatigue, reck, ward) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]✨ STAFF SIPHON:[/] Siphoned mental clarity (-%d Fatigue, -%d Recklessness) and reinforced Arcane Ward (+%d Barrier)!" Theme.Cyan fatigue reck ward)

    | CombatEvent.ShieldRaised (_, armor, stacks) ->
      AnsiConsole.MarkupLine(sprintf "  [bold %s]🛡️ BASTION SHIELD:[/] Fortified armor soak (+%d Armor) and braced stance (+%d ramp stacks)!" Theme.Green armor stacks)

  /// Creates the Panel for the comprehensive Symbol & Glyph Reference Guide
  let createSymbolAndGlyphLegendPanel () : Panel =
    let grid = Grid()
    grid.AddColumn(GridColumn()) |> ignore

    grid.AddRow(Markup(sprintf "[bold %s]󰒋 FORNACH: COMPREHENSIVE SYMBOL & GLYPH REFERENCE MANUAL[/]" Theme.Yellow)) |> ignore
    grid.AddRow(Rule().RuleStyle(Theme.StylePurple)) |> ignore

    // Section 1: Navigation & Hotkeys
    grid.AddRow(Markup(sprintf "[bold %s]󰌌 Navigation & Hotkeys (Tower & Story Modes):[/]" Theme.Pink)) |> ignore

    let controlsTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorComment)
    controlsTable.AddColumn(TableColumn("[bold white]Hotkey / Input[/]").Centered()) |> ignore
    controlsTable.AddColumn(TableColumn("[bold white]Mode / Context[/]")) |> ignore
    controlsTable.AddColumn(TableColumn("[bold white]Functionality & Action[/]")) |> ignore

    controlsTable.AddRow(Markup("[bold #f1fa8c] ? [/] or [bold #f1fa8c] F1 [/]"), Markup("Global (Tower & Story)"), Markup("Open this comprehensive Symbol & Glyph Reference Guide at any time.")) |> ignore
    controlsTable.AddRow(Markup("[bold white]Arrows / Vim (H J K L)[/]"), Markup("Tower Mode (Locomotion)"), Markup("Move player North (K/Up), South (J/Down), West (H/Left), East (L/Right).")) |> ignore
    controlsTable.AddRow(Markup("[bold white]Y / U / B / N (Keypad)[/]"), Markup("Tower Mode (Locomotion)"), Markup("Diagonal movement: NW (Y/7), NE (U/9), SW (B/1), SE (N/3).")) |> ignore
    controlsTable.AddRow(Markup("[bold white]X [/] or [bold white]Semicolon (;)[/]"), Markup("Tower Mode"), Markup("Toggle Tile & Hazard Reticle Inspection Mode to examine distant cells.")) |> ignore
    controlsTable.AddRow(Markup("[bold white]Spacebar [/] or [bold white]Period (.)[/]"), Markup("Tower Mode"), Markup("Stand ground / Wait a turn to observe ambient flow and effects.")) |> ignore
    controlsTable.AddRow(Markup("[bold white]C [/]"), Markup("Tower Mode"), Markup("Open the Character Sheet: level, mastery tier, XP progress, and the full 12-attribute matrix.")) |> ignore
    controlsTable.AddRow(Markup("[bold white]J / K [/] or [bold white]Arrows[/]"), Markup("Story & Duel Prompts"), Markup("Vim navigation up/down through tactical menus and story choices.")) |> ignore
    controlsTable.AddRow(Markup("[bold white]Q [/] or [bold white]Escape[/]"), Markup("Tower Mode / Inspect"), Markup("Exit reticle inspection mode or retreat from tower expedition to menu.")) |> ignore

    grid.AddRow(controlsTable) |> ignore

    // Section 2: Map Entities & World Encounters
    grid.AddRow(Markup(sprintf "\n[bold %s]󰞁 Dungeon Map Entities & World Encounters (Tower Mode):[/]" Theme.Cyan)) |> ignore

    let entityTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorComment)
    entityTable.AddColumn(TableColumn("[bold white]Glyph[/]").Centered()) |> ignore
    entityTable.AddColumn(TableColumn("[bold white]Name / Feature[/]")) |> ignore
    entityTable.AddColumn(TableColumn("[bold white]Interaction & Effect[/]")) |> ignore

    entityTable.AddRow(Markup("[bold #f1fa8c on #ff5555] @ [/]"), Markup("[bold #f1fa8c]Player Character[/]"), Markup("Your current location on the floor grid.")) |> ignore
    entityTable.AddRow(Markup("[bold #ff5555] ! [/]"), Markup("[bold #ff5555]Hostile Guardian / Ambush[/]"), Markup("Formidable biome adversaries or lurking pack ambushes. Walk into tile to engage.")) |> ignore
    entityTable.AddRow(Markup("[#6272a4] % [/]"), Markup("[#6272a4]Neutralized Adversary[/]"), Markup("Defeated guardian or dispersed ambush site. Safely walkable.")) |> ignore
    entityTable.AddRow(Markup("[bold #8be9fd] ? [/]"), Markup("[bold #8be9fd]Inhabitant / Scholar[/]"), Markup("Friendly NPC offering dialogue, world lore, and optional ascension trials.")) |> ignore
    entityTable.AddRow(Markup("[bold #f1fa8c] ⌹ [/]"), Markup("[bold #f1fa8c]Vault Chest / Sealed Vault[/]"), Markup("Loot caches and puzzle-locked chambers. Recover keystones, Souls, and relics.")) |> ignore
    entityTable.AddRow(Markup("[bold #50fa7b] † [/]"), Markup("[bold #50fa7b]Runic Shrine[/]"), Markup("Ancient monolith; communing restores +40 Morale and resets Recklessness to 0.")) |> ignore
    entityTable.AddRow(Markup("[bold #bd93f9] ♨ [/]"), Markup("[bold #bd93f9]Sacrificial Altar[/]"), Markup("Dark stone offering permanent combat buffs in exchange for Health, Morale, or Armor.")) |> ignore
    entityTable.AddRow(Markup("[bold #8be9fd] $ [/]"), Markup("[bold #8be9fd]Spectral Merchant[/]"), Markup("Wandering trader exchanging rare equipment for Souls and alchemical trophies.")) |> ignore
    entityTable.AddRow(Markup("[bold #50fa7b] ✦ [/]"), Markup("[bold #50fa7b]Dormant Memory Echo[/]"), Markup("Sensory fragment of repressed trauma from the real world. Restores Morale.")) |> ignore
    entityTable.AddRow(Markup("[#6272a4] ✧ [/]"), Markup("[#6272a4]Communed Memory Echo[/]"), Markup("Previously awakened narrative echo. Safely walkable.")) |> ignore
    entityTable.AddRow(Markup("[bold #ff5555] ✕ [/]"), Markup("[bold #ff5555]Mechanical Trap[/]"), Markup("Concealed spikes, gas, or discharge. Disarms with Reflex/Perception; triggers on fail.")) |> ignore
    entityTable.AddRow(Markup("[bold #50fa7b] ▲ [/]"), Markup("[bold #50fa7b]Ascension Stairway[/]"), Markup("Open stairway portal leading up to the next tier of the Infinite Tower.")) |> ignore
    entityTable.AddRow(Markup("[bold #ffb86c] ⮝ [/]"), Markup("[bold #ffb86c]Sealed Ascension Door[/]"), Markup("Barred portal requiring a guardian keystone or completed trial to unlock.")) |> ignore
    entityTable.AddRow(Markup("[bold #f8f8f2] ∩ [/]"), Markup("[bold #f8f8f2]Colonnade Archway[/]"), Markup("Open architectural causeway transition connecting plazas.")) |> ignore

    grid.AddRow(entityTable) |> ignore

    // Section 3: Environmental Hazards & Terrain
    grid.AddRow(Markup(sprintf "\n[bold %s]⚠ Environmental Hazards & Architecture:[/]" Theme.Red)) |> ignore

    let hazardTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorComment)
    hazardTable.AddColumn(TableColumn("[bold white]Glyph[/]").Centered()) |> ignore
    hazardTable.AddColumn(TableColumn("[bold white]Terrain / Hazard[/]")) |> ignore
    hazardTable.AddColumn(TableColumn("[bold white]Tactical Effect[/]")) |> ignore

    hazardTable.AddRow(Markup("[bold #50fa7b] ≈ [/]"), Markup("[bold #50fa7b]Corrosive Acid Slag[/]"), Markup("[bold red]-15 Armor durability[/] dissolved immediately upon stepping into tile.")) |> ignore
    hazardTable.AddRow(Markup("[bold #ff5555] ≈ [/]"), Markup("[bold #ff5555]Molten Lava Rift[/]"), Markup("[bold red]-15 Direct Health[/] burned immediately upon stepping into tile.")) |> ignore
    hazardTable.AddRow(Markup("[bold #8be9fd] ≋ [/]"), Markup("[bold #8be9fd]Deep Current[/]"), Markup("[bold purple]+15 Exhaustion[/] inflicted from wading through heavy water currents.")) |> ignore
    hazardTable.AddRow(Markup("[bold #ff79c6] ❀ [/]"), Markup("[bold #ff79c6]Calming Spores[/]"), Markup("[bold green]Resets Recklessness to 0[/] via fragrant psychotropic blossom spores.")) |> ignore
    hazardTable.AddRow(Markup("[bold white] ∏ ♠ ▲ ☗ ⛩ ✦ [/]"), Markup("Colonnade Pillar"), Markup("Massive stone monoliths. Impassable; blocks movement and line-of-sight.")) |> ignore
    hazardTable.AddRow(Markup("[grey] [[Space]] [/]"), Markup("Abyssal Chasm / Void"), Markup("Endless drop. Impassable; blocks movement, but allows vision & ranged line-of-sight.")) |> ignore

    grid.AddRow(hazardTable) |> ignore

    // Section 4: Tactical Vitals, Meters & Economy
    grid.AddRow(Markup(sprintf "\n[bold %s]󰓥 Tactical Vitals, Meters & Story Icons:[/]" Theme.Yellow)) |> ignore

    let vitalsTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorComment)
    vitalsTable.AddColumn(TableColumn("[bold white]Glyph[/]").Centered()) |> ignore
    vitalsTable.AddColumn(TableColumn("[bold white]Resource / Concept[/]")) |> ignore
    vitalsTable.AddColumn(TableColumn("[bold white]Function & Significance[/]")) |> ignore

    vitalsTable.AddRow(Markup("[bold #ff5555]󰋑[/]"), Markup("Health (HP)"), Markup("Physical vitality. Reaching 0 causes defeat and trauma loop rewind.")) |> ignore
    vitalsTable.AddRow(Markup("[bold #8be9fd]󰧑[/]"), Markup("Morale"), Markup("Psychological resolve. Reaching 0 triggers mental collapse.")) |> ignore
    vitalsTable.AddRow(Markup("[bold #f1fa8c][/]"), Markup("Armor Integrity"), Markup("Absorbs incoming attack damage based on durability absorption ratio.")) |> ignore
    vitalsTable.AddRow(Markup("[bold #ff5555]󰈸[/]"), Markup("Recklessness (0-100)"), Markup("Offensive momentum; amplifies damage, but increases vulnerability if unchecked.")) |> ignore
    vitalsTable.AddRow(Markup("[bold #f1fa8c]󰓎[/]"), Markup("Overwhelm (0-100)"), Markup("Mental clutter and pressure from rapid tactical exchanges.")) |> ignore
    vitalsTable.AddRow(Markup("[bold #bd93f9]󰒓[/]"), Markup("Exhaustion (0-100)"), Markup("Physical stamina depletion. Increases dice difficulty for physical strikes.")) |> ignore
    vitalsTable.AddRow(Markup("[bold #50fa7b]󰓥[/]"), Markup("Martial Stance"), Markup("Physical combat stance (Power, Agility, Discipline).")) |> ignore
    vitalsTable.AddRow(Markup("[bold #ff79c6]🧵[/]"), Markup("Complex Form"), Markup("Arcane mental stance (Resonance Spike, Phantasmal Diffusion, Aegis Lattice).")) |> ignore
    vitalsTable.AddRow(Markup("[bold gold1]󰮯[/]"), Markup("Souls"), Markup("Primary currency harvested from fallen adversaries for spectral merchant wares.")) |> ignore
    vitalsTable.AddRow(Markup("[bold cyan]💎[/]"), Markup("Alchemical Trophies"), Markup("Rare monster remnants (Cores, Silk, Dust) required for legendary wares.")) |> ignore
    vitalsTable.AddRow(Markup("[bold yellow]󰌆[/]"), Markup("Vault Keys"), Markup("Collected keys used to unlock sealed ascension doors and vaults.")) |> ignore
    vitalsTable.AddRow(Markup(sprintf "[bold %s]󰆧[/]" Theme.Pink), Markup("Equipment Relics"), Markup("Ancient artifacts offering permanent stat boosts and reactive combat triggers.")) |> ignore
    vitalsTable.AddRow(Markup("[bold gold1]★[/]"), Markup("Story Memory"), Markup("Unlocked narrative milestone illuminating amnesia and the lost twin (Lyra).")) |> ignore
    vitalsTable.AddRow(Markup("[bold red]⚔️[/]"), Markup("Tactical Duel"), Markup("Turn-based contested roll battle against manifestations or guardians.")) |> ignore
    vitalsTable.AddRow(Markup("[bold yellow]󰍹[/]"), Markup("Inspection Reticle"), Markup("Reticle mode (press 'x' or ';') for inspecting distant cells and hazards.")) |> ignore

    grid.AddRow(vitalsTable) |> ignore

    grid.AddRow(Markup(sprintf "\n[%s]Press any key to resume...[/]" Theme.Comment)) |> ignore

    Panel(grid)
      .Border(BoxBorder.Double)
      .BorderStyle(Theme.StylePurple)
      .Expand()

  /// Displays a comprehensive interactive modal with the full symbol and glyph legend across Tower and Story modes
  let showSymbolAndGlyphLegend () : unit =
    let panel = createSymbolAndGlyphLegendPanel ()
    AnsiConsole.Clear()
    AnsiConsole.Write(panel)
    Console.ReadKey(true) |> ignore

  [<Literal>]
  let HelpChoiceLabel = "󰋜  [bold #f1fa8c][[Help & Legend]][/] View Symbol & Glyph Guide (? / F1)"

  /// Prompts for a selection with Vim navigation and '?' / F1 help hotkey support.
  /// If the user presses '?' or F1, or selects the Help option, the symbol/glyph legend
  /// is shown, and the prompt (and optional renderContext) is re-rendered without losing state.
  let promptSelectionWithHelp
    (title: string)
    (choices: string list)
    (pageSize: int option)
    (renderContext: unit -> unit) : string =

    let fullChoices = choices @ [ HelpChoiceLabel ]
    let mutable result = None

    while result.IsNone do
      renderContext ()
      let p =
        SelectionPrompt<string>()
          .Title(title)
          .AddChoices(fullChoices)
      match pageSize with
      | Some sz -> p.PageSize(sz) |> ignore
      | None -> ()

      let selected = promptWithVimAndHelp p
      if selected = HelpChoiceLabel || selected.Contains("Help & Legend") then
        showSymbolAndGlyphLegend ()
      else
        result <- Some selected

    result.Value
