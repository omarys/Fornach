namespace Fornach.Spatial.Tests

open Xunit
open Fornach.Spatial

module FlankingTests =

  let private at x y = { X = x; Y = y }

  [<Fact>]
  let ``an attack from the watched arc is met head-on`` () =
    Assert.Equal(Flank.Front, Flanking.classify Direction.East (at 5 5) (at 9 5))
    Assert.Equal(Flank.Front, Flanking.classify Direction.North (at 5 5) (at 5 1))
    Assert.Equal(Flank.Front, Flanking.classify Direction.SouthWest (at 5 5) (at 4 6))

  [<Fact>]
  let ``an attack from directly behind is a rear strike`` () =
    Assert.Equal(Flank.Rear, Flanking.classify Direction.East (at 5 5) (at 1 5))
    Assert.Equal(Flank.Rear, Flanking.classify Direction.North (at 5 5) (at 5 9))
    Assert.Equal(Flank.Rear, Flanking.classify Direction.NorthWest (at 5 5) (at 6 6))

  [<Fact>]
  let ``an attack from the side is a flank`` () =
    Assert.Equal(Flank.Flank, Flanking.classify Direction.East (at 5 5) (at 5 1))
    Assert.Equal(Flank.Flank, Flanking.classify Direction.East (at 5 5) (at 5 9))
    Assert.Equal(Flank.Flank, Flanking.classify Direction.North (at 5 5) (at 9 5))

  [<Fact>]
  let ``an attacker sharing the defender's tile is met head-on`` () =
    Assert.Equal(Flank.Front, Flanking.classify Direction.South (at 5 5) (at 5 5))
