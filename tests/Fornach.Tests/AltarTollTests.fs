namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// An altar's whole point is that the player weighs something. The Gale-Worn Monolith used to charge
/// 20% Health and pay back 35% Health, so it healed rather than cost — and healed most in the moment the
/// toll would have bitten hardest, because the sacrifice floors at one point. The rule these pin is
/// structural: no altar may reward the pool it charges. They walk the union, so a new theme cannot slip
/// under it.
module AltarTollTests =

  let private everyTheme : FloorTheme list =
    Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<FloorTheme>)
    |> Array.map (fun case ->
      Microsoft.FSharp.Reflection.FSharpValue.MakeUnion(case, [||]) :?> FloorTheme)
    |> List.ofArray

  [<Fact>]
  let ``every theme is covered by the toll rule`` () =
    Assert.Equal(7, everyTheme.Length)

  [<Fact>]
  let ``no altar rewards the pool it charges`` () =
    for theme in everyTheme do
      for boon in [ PrimaryPair; SecondaryPair ] do
        let altar = WorldEvents.createAltar theme 3 boon

        Assert.False(
          AltarReward.RefundsItsOwnToll altar.Cost altar.Reward,
          sprintf
            "%s (%s) charges %s and pays %s back"
            altar.Name
            (string theme)
            altar.Cost.ChargedPool
            (altar.Reward.RewardedPools |> String.concat "/")
        )

  [<Fact>]
  let ``the gale-worn monolith takes flesh and gives resolve`` () =
    let altar = WorldEvents.createAltar TempestTerraces 3 PrimaryPair
    Assert.Equal("Health", altar.Cost.ChargedPool)
    Assert.False(altar.Reward.RewardedPools |> List.contains "Health")
    Assert.Equal("+35% Morale", altar.Reward.Description)

  [<Fact>]
  let ``a surge that pays no health does not claim to`` () =
    Assert.Equal("+35% HP", AltarReward.VitalitySurge(35, 0).Description)
    Assert.Equal("+35% HP and +20% Morale", AltarReward.VitalitySurge(35, 20).Description)
