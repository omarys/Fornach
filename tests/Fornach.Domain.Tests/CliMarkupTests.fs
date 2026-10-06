namespace Fornach.Domain.Tests

open System
open Xunit
open Spectre.Console
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
    let truckFrame2 = @" (   _    _ _\  [[==HONK!==]]  "
    let row = sprintf "   / \\  [bold yellow]%s[/]" truckFrame2
    let m = Markup(row)
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
