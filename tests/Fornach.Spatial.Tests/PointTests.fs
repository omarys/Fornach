module Fornach.Spatial.Tests.PointTests

open Xunit
open Fornach.Spatial

[<Fact>]
let ``Point addition adds coordinates correctly`` () =
  let p1 = { X = 3; Y = 5 }
  let p2 = { X = -1; Y = 2 }
  let result = p1 + p2
  Assert.Equal({ X = 2; Y = 7 }, result)

[<Fact>]
let ``Point subtraction subtracts coordinates correctly`` () =
  let p1 = { X = 10; Y = 8 }
  let p2 = { X = 4; Y = 3 }
  let result = p1 - p2
  Assert.Equal({ X = 6; Y = 5 }, result)

[<Fact>]
let ``Chebyshev distance treats diagonals as 1 step`` () =
  let origin = { X = 0; Y = 0 }
  let diagonal = { X = 5; Y = 5 }
  // In Chebyshev distance, (5, 5) is 5 steps away, not 10 or 7.07
  Assert.Equal(5, Distance.chebyshev origin diagonal)

[<Fact>]
let ``Direction toDelta moves in the correct direction`` () =
  let origin = { X = 10; Y = 10 }
  let movedNorth = origin + Direction.toDelta Direction.North
  Assert.Equal({ X = 10; Y = 9 }, movedNorth)
