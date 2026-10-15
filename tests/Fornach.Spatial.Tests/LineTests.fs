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

// Regression: bresenham used a positive dy, so any non-horizontal line spun forever
// (err only grew and y never advanced). These non-horizontal cases would hang before the fix.
[<Fact>]
let ``Bresenham traces a diagonal line`` () =
  let line = Line.bresenham { X = 0; Y = 0 } { X = 3; Y = 3 }
  let expected =
    [ { X = 0; Y = 0 }; { X = 1; Y = 1 }; { X = 2; Y = 2 }; { X = 3; Y = 3 } ]
  Assert.Equal<Point list>(expected, line)

[<Fact>]
let ``Bresenham traces a shallow non-horizontal line`` () =
  let line = Line.bresenham { X = 0; Y = 0 } { X = 5; Y = 2 }
  let expected =
    [ { X = 0; Y = 0 }; { X = 1; Y = 0 }; { X = 2; Y = 1 }
      { X = 3; Y = 1 }; { X = 4; Y = 2 }; { X = 5; Y = 2 } ]
  Assert.Equal<Point list>(expected, line)

[<Fact>]
let ``Bresenham traces a steep non-horizontal line`` () =
  let line = Line.bresenham { X = 0; Y = 0 } { X = 2; Y = 5 }
  let expected =
    [ { X = 0; Y = 0 }; { X = 0; Y = 1 }; { X = 1; Y = 2 }
      { X = 1; Y = 3 }; { X = 2; Y = 4 }; { X = 2; Y = 5 } ]
  Assert.Equal<Point list>(expected, line)

[<Fact>]
let ``LOS on a diagonal is blocked by an off-axis wall`` () =
  let isOpaque p = p = { X = 1; Y = 1 }
  Assert.True(Los.check isOpaque { X = 0; Y = 0 } { X = 1; Y = 1 })
  Assert.False(Los.check isOpaque { X = 0; Y = 0 } { X = 2; Y = 2 })
