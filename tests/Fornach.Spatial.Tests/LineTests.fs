module Fornach.Spatial.Tests.LineTests

open Xunit
open Fornach.Spatial

[<Fact>]
let ``Bresenham traces horizontal straight line`` () =
  let start = { X = 1; Y = 2 }
  let target = { X = 4; Y = 2 }
  let line = Line.bresenham start target

  let expected =
    [ { X = 1; Y = 2 }; { X = 2; Y = 2 }; { X = 3; Y = 2 }; { X = 4; Y = 2 } ]

  Assert.Equal<Point list>(expected, line)

[<Fact>]
let ``LOS is clear when no obstacles exist`` () =
  let start = { X = 0; Y = 0 }
  let target = { X = 3; Y = 0 }
  let noObstacles _ = false // No tiles block vision

  Assert.True(Los.check noObstacles start target)

[<Fact>]
let ``LOS is blocked when a wall is in between`` () =
  let start = { X = 0; Y = 0 }
  let target = { X = 4; Y = 0 }
  // Wall located at (2, 0)
  let isOpaque p = p = { X = 2; Y = 0 }

  Assert.False(Los.check isOpaque start target)

[<Fact>]
let ``LOS can see the wall itself, but not past it`` () =
  let start = { X = 0; Y = 0 }
  let wall = { X = 2; Y = 0 }
  let pastWall = { X = 3; Y = 0 }
  let isOpaque p = p = wall

  // Can see the wall
  Assert.True(Los.check isOpaque start wall)
  // Cannot see past the wall
  Assert.False(Los.check isOpaque start pastWall)
