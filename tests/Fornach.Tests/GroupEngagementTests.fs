namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Cli

module GroupEngagementTests =

  let private createCombatant name force acumen composure hp =
    let stats =
      StatBlock.Create
        [ StatId.Force, force
          StatId.Fortitude, 100
          StatId.Finesse, 100
          StatId.Reflex, 100
          StatId.Prowess, 100
          StatId.Poise, 100
          StatId.Intellect, 80
          StatId.Resolve, 80
          StatId.Acuity, 80
          StatId.Intuition, 100
          StatId.Acumen, acumen
          StatId.Composure, composure ]
    Combatant.create (CombatantId.New()) name hp hp stats

  [<Fact>]
  let ``compactCombatantLines includes health, morale, weapon and armor integrity`` () =
    let enemy = createCombatant "Pack Hound" 150 100 80 900
    let lines = Display.compactCombatantLines enemy None
    let text = String.concat "\n" lines
    Assert.Contains("HP", text)
    Assert.Contains("Morale", text)
    Assert.Contains("Weapon Integrity", text)
    Assert.Contains("Armor Integrity", text)

  [<Fact>]
  let ``compactCombatantLines includes primary stat and opening when observed`` () =
    let observer = createCombatant "Observer" 120 500 80 1000
    let enemy = createCombatant "Pack Hound" 150 100 80 900
    let lines = Display.compactCombatantLines enemy (Some observer)
    let text = String.concat "\n" lines
    Assert.Contains("Primary Stat", text)
    Assert.Contains("Opening", text)

  [<Fact>]
  let ``compactCombatantLines omits the opening without an observer`` () =
    let enemy = createCombatant "Pack Hound" 150 100 80 900
    let lines = Display.compactCombatantLines enemy None
    let text = String.concat "\n" lines
    Assert.DoesNotContain("Opening", text)
    Assert.DoesNotContain("Primary Stat", text)
