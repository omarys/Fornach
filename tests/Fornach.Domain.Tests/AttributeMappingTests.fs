namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain

module AttributeMappingTests =

  [<Fact>]
  let ``Mental stats map to expected social names`` () =
    let expectedMappings =
      [ Intellect, "Presence / Command"
        Resolve, "Will / Defiance"
        Acuity, "Guile / Charm"
        Intuition, "Insight / Scrutiny"
        Acumen, "Leverage / Wit"
        Composure, "Poise / Composure" ]

    for (stat, expectedName) in expectedMappings do
      let actual = Attributes.socialNameOf stat
      Assert.Equal(expectedName, actual)

  [<Fact>]
  let ``Physical stats keep canonical names in social context`` () =
    let physicalStats = [ Force; Fortitude; Finesse; Reflex; Prowess; Poise ]

    for stat in physicalStats do
      let desc = Attributes.descriptorOf stat
      Assert.Equal(desc.CanonicalName, desc.SocialName)

  [<Fact>]
  let ``displayNameFor respects combat mode context`` () =
    Assert.Equal("Presence / Command", Attributes.displayNameFor CombatMode.Social StatId.Intellect)
    Assert.Equal("Intellect", Attributes.displayNameFor CombatMode.Arcane StatId.Intellect)
    Assert.Equal("Intellect", Attributes.displayNameFor CombatMode.Physical StatId.Intellect)
    Assert.Equal("Force", Attributes.displayNameFor CombatMode.Physical StatId.Force)

  [<Fact>]
  let ``tryParse resolves canonical and social alias names`` () =
    let testCases =
      [ // Canonical
        "Intellect", Intellect
        "intellect", Intellect
        // Social primary and secondary aliases
        "Presence", Intellect
        "Command", Intellect
        "Presence / Command", Intellect
        "presence / command", Intellect
        "Will", Resolve
        "Defiance", Resolve
        "Guile", Acuity
        "Charm", Acuity
        "Insight", Intuition
        "Scrutiny", Intuition
        "Leverage", Acumen
        "Wit", Acumen
        "Poise / Composure", Composure
        "Poise", Poise // note: Poise is physical stat canonical name, and alias on Composure; canonical Poise matches first
        "Composure", Composure ]

    for (input, expectedStat) in testCases do
      let result = Attributes.tryParse input
      Assert.Equal(Some expectedStat, result)
