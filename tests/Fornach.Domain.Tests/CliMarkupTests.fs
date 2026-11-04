namespace Fornach.Domain.Tests

open System
open Xunit
open Spectre.Console
open Fornach.Domain
open Fornach.Cli

module CliMarkupTests =

  [<Fact>]
  let ``HelpChoiceLabel parses cleanly in Spectre AnsiMarkup without style errors`` () =
    // Should not throw System.InvalidOperationException
    let markup = Markup(Display.HelpChoiceLabel)
    Assert.NotNull(markup)

  [<Fact>]
  let ``SelectionPrompt with HelpChoiceLabel parses all choices cleanly`` () =
    let sampleChoices = [
      "⚔️  Engage in Tactical Combat"
      "⚡  Quick Resolve"
      "🏃  Step Back"
      Display.HelpChoiceLabel
    ]
    for choice in sampleChoices do
      let m = Markup(choice)
      Assert.NotNull(m)

  [<Fact>]
  let ``combat log damage lines parse cleanly in Spectre markup`` () =
    // A single-bracketed [CRITICAL] is read as a style tag and throws; this guards the pack log.
    let id = CombatantId.New()
    let names (_: CombatantId) = "Aspect of Guilt"
    let damage crit compromised =
      { TargetId = id
        Plane = Physical
        Amount = 42
        IsCritical = crit
        IsArmorCompromised = compromised }

    for (crit, compromised) in [ false, false; true, false; false, true; true, true ] do
      let line = Display.formatDamageLogLine names true (damage crit compromised)
      Assert.NotNull(Markup(line))
      Assert.Contains("42", line)
      Assert.Contains("Aspect of Guilt", line)

  [<Fact>]
  let ``Message log items containing square brackets are escaped cleanly`` () =
    let rawLogMessages = [
      "The Ascension Portal is sealed. Requires: [Golden Key]. (Find in Vault)"
      "Communed with Sacrificial Altar: Paid [50 HP], Received [Blade Boon]!"
      "󰔱 Discovered Memory Echo: [Lost Fragment]. A scent of rain (+40 Morale)"
    ]
    for msg in rawLogMessages do
      let escaped = Markup.Escape msg
      let line = sprintf "[bold %s]%s[/]" Theme.Foreground escaped
      let m = Markup(line)
      Assert.NotNull(m)

  [<Fact>]
  let ``KeyholeLock and Quest descriptions with escaped brackets parse cleanly`` () =
    let keyName = "Obsidian Key"
    let hint = "Hidden in lower catacombs"
    let keyDesc = sprintf "Heavy iron vault portal sealed tight. Requires: [[%s]]. Hint: %s" keyName hint
    let mKey = Markup(keyDesc)
    Assert.NotNull(mKey)

    let questTitle = "Trial of Faith"
    let req = "Slay 3 Harpies"
    let questDesc = sprintf "Ascension barred by architectural trial: [[%s]]. Requirement: %s" questTitle req
    let mQuest = Markup(questDesc)
    Assert.NotNull(mQuest)

  [<Fact>]
  let ``Quest status badges parse cleanly without style name lookup error`` () =
    let completedBadge = sprintf "[bold %s][[COMPLETED]][/]" Theme.Green
    let inProgressBadge = sprintf "[bold %s][[IN PROGRESS]][/]" Theme.Yellow
    Assert.NotNull(Markup(completedBadge))
    Assert.NotNull(Markup(inProgressBadge))

  [<Fact>]
  let ``DeathAnimation truck replay frames parse cleanly in Markup without style errors`` () =
    for idx in [ 0; 1 ] do
      for line in DeathAnimation.truckBodyFrames idx do
        let row = sprintf "[bold white]%s[/]" line
        let m = Markup(row)
        Assert.NotNull(m)

    for (r0, r1, r2, r3) in DeathAnimation.impactSequenceFrames do
      for line in [ r0; r1; r2; r3 ] do
        let m = Markup(line)
        Assert.NotNull(m)

  [<Fact>]
  let ``Symbol and glyph legend panel parses and renders cleanly without style errors`` () =
    let panel = Display.createSymbolAndGlyphLegendPanel()
    Assert.NotNull(panel)
    use writer = new System.IO.StringWriter()
    let console = AnsiConsole.Create(AnsiConsoleSettings(Out = AnsiConsoleOutput(writer)))
    console.Write(panel)
    let output = writer.ToString()
    Assert.False(String.IsNullOrWhiteSpace(output))

  [<Fact>]
  let ``Battered chest prompt title and choices parse and render cleanly in SelectionPrompt`` () =
    let title =
      sprintf "[bold %s]⌹ SCAVENGING THE BATTERED CHEST (Lock Broken)[/]\n[italic %s]Choose your weapon armament and awaken your class:[/]"
        Theme.Yellow Theme.Comment
    let choices =
      [ "🗡️ Two-handed Greatsword (Berserker) — Ferocious momentum, sweeping cleaves & high force"
        "🤺 Paired Stiletto & Rapier (Duelist) — Fencing precision, high reflex, agile cadences"
        "🛡️ Arming Sword & Reinforced Shield (Warden) — Bastion defense, fortress poise, counterplay"
        "🪄 Carved Ash Staff (Inquisitor) — Arcane resonance, psionic intellect, mental clarity"
        "🪞 Silvered Hand Mirror (Mesmer) — Phantasmal decoys, mirror clones & sensory static"
        "📖 Dusty Tome (Abjurer) — Runic wards, composure barriers & destabilizing runes"
        Display.HelpChoiceLabel ]
    let m = Markup(title)
    Assert.NotNull(m)
    for c in choices do
      Assert.NotNull(Markup(c))

  [<Fact>]
  let ``All Tower encounter prompt titles parse cleanly in Markup without unbalanced stack`` () =
    let titles = [
      sprintf "[bold %s]⌹ SCAVENGING THE BATTERED CHEST (Lock Broken)[/]\n[italic %s]Choose your weapon armament and awaken your class:[/]" Theme.Yellow Theme.Comment
      sprintf "[bold red]󰈸 %s blocks your path: %s![/]" "TRAUMA MANIFESTATION" "Aspect of Guilt"
      sprintf "[bold %s]♨ %s[/]\n[italic %s]%s[/]\n[bold %s]Cost: %s[/]  ──>  [bold %s]Reward: %s[/]" Theme.Purple "Altar" Theme.Comment "Desc" Theme.Red "Cost" Theme.Green "Reward"
      sprintf "[bold %s]󱁠 %s, %s[/]\n[italic %s]\"%s\"[/]\n[bold gold1]Your Souls: %d[/]" Theme.Cyan "Merchant" "Title" Theme.Comment "Dialogue" 100
      sprintf "[bold %s]⌹ %s[/]\n[italic %s]%s[/]\n[bold %s]%s[/]\nGuaranteed: [bold gold1]+%d Souls[/] + %d Relic(s)" Theme.Yellow "Vault" Theme.Comment "Desc" Theme.Orange "Puzzle" 50 1
    ]
    for t in titles do
      let m = Markup(t)
      Assert.NotNull(m)

  [<Fact>]
  let ``Combat action choices for all classes against all monsters parse cleanly in Markup without style errors`` () =
    let classes = [ "berserker"; "duelist"; "warden"; "inquisitor"; "mesmer"; "abjurer" ]
    let enemies = [
      Bestiary.slagHound.ToCombatant (CombatantId.New())
      Bestiary.stoneGargoyle.ToCombatant (CombatantId.New())
      Bestiary.quarryOverseer.ToCombatant (CombatantId.New())
      StoryBosses.createGuiltAspect ()
      StoryBosses.createDenialAspect ()
    ]

    for cls in classes do
      let player = StoryBosses.createProloguePlayer cls
      for enemy in enemies do
        let choices = Program.buildActionChoices player enemy
        for choice in choices do
          let m = Markup(choice)
          Assert.NotNull(m)

  [<Fact>]
  let ``Seize Indes counter follows the stance the player is fighting in, not the class vector`` () =
    // Reported case: an Agility-stance build was offered a Power Force Strike as its Indes counter. The
    // offer read Class.Vector, so the gambit was made in a vector the player had not slipped into.
    let enemy = Bestiary.slagHound.ToCombatant (CombatantId.New())

    let slipped =
      { StoryBosses.createProloguePlayer "berserker" with
          Tempo = TacticalTiming.Nach
          Stance = CombatStance.AgilityStance }

    let counterOf (p: Combatant) =
      Program.buildActionChoicesWithTelegraph p enemy None true
      |> List.filter (fun c -> c.Contains("Seize Indes"))

    let slippedCounter = counterOf slipped
    Assert.Single(slippedCounter) |> ignore
    Assert.Contains("Finesse Cadence", slippedCounter.Head)

    // The same build braced in Power Stance is offered the Power counter.
    let braced = { slipped with Stance = CombatStance.PowerStance }
    let bracedCounter = counterOf braced
    Assert.Single(bracedCounter) |> ignore
    Assert.Contains("Force Strike", bracedCounter.Head)

  [<Fact>]
  let ``buildActionChoicesWithTelegraph surfaces preparations, Press the Advantage, and Env Gambits cleanly`` () =
    let enemy =
      { StoryBosses.createDenialAspect () with
          IsStaggered = true }

    let player =
      { StoryBosses.createProloguePlayer "berserker" with
          Tempo = TacticalTiming.Vor
          Momentum = 4
          Preparations =
            [ PreparationSlot.create PreparationType.ShockwaveSlam 2
              PreparationSlot.create PreparationType.ConcealedBlade 2 ] }

    let choices = Program.buildActionChoicesWithTelegraph player enemy None true
    for choice in choices do
      Assert.NotNull(Markup(choice))

    let prepChoices = choices |> List.filter (fun c -> c.Contains("Deploy Prep:"))
    Assert.Equal(2, prepChoices.Length)
    Assert.Equal(Fornach.Engine.ActionIntent.DeployPreparation(PreparationType.ShockwaveSlam, None), Program.parseActionChoice prepChoices.[0])

    let pressChoices = choices |> List.filter (fun c -> c.Contains("PRESS THE ADVANTAGE"))
    Assert.NotEmpty(pressChoices)

    let envChoices = choices |> List.filter (fun c -> c.Contains("Env Gambit:"))
    Assert.NotEmpty(envChoices)
