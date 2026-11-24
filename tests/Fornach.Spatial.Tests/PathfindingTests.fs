module Fornach.Spatial.Tests.PathfindingTests

open Xunit
open Fornach.Spatial

[<Fact>]
let ``Pathfinding finds direct path in open terrain`` () =
  let start = { X = 0; Y = 0 }
  let goal = { X = 3; Y = 0 }
  let isPassable _ = true

  let result = Pathfinding.aStar isPassable start goal
  Assert.True(result.IsSome)
  let path = result.Value

  Assert.Equal(4, path.Length)
  Assert.Equal(start, path.Head)
  Assert.Equal(goal, List.last path)

[<Fact>]
let ``Pathfinding navigates around a wall obstacle`` () =
  let start = { X = 0; Y = 0 }
  let goal = { X = 2; Y = 0 }
  // A wall at (1, 0) directly blocking the straight path
  let wall = { X = 1; Y = 0 }
  let isPassable p = (p <> wall)

  let result = Pathfinding.aStar isPassable start goal
  Assert.True(result.IsSome)
  let path = result.Value

  // Path must reach goal without stepping on the wall
  Assert.Equal(start, path.Head)
  Assert.Equal(goal, List.last path)
  Assert.DoesNotContain(wall, path)

[<Fact>]
let ``Pathfinding returns None when completely walled off`` () =
  let start = { X = 0; Y = 0 }
  let goal = { X = 5; Y = 5 }
  // Goal is completely blocked
  let isPassable p = (p <> goal)

  let result = Pathfinding.aStar isPassable start goal
  Assert.True(result.IsNone)

[<Fact>]
let ``Pathfinding steps straight along the row when the goal shares it`` () =
  let start = { X = 7; Y = 8 }
  let goal = { X = 3; Y = 8 }
  let path = (Pathfinding.aStar (fun _ -> true) start goal).Value
  // Diagonal steps cost sqrt(2), so a straight line beats any staircase: the first
  // step must stay on the row instead of sidestepping diagonally.
  Assert.Equal({ X = 6; Y = 8 }, path.[1])
  Assert.True(path |> List.forall (fun p -> p.Y = 8))

[<Fact>]
let ``Pathfinding still cuts diagonally toward a diagonal goal`` () =
  let start = { X = 0; Y = 0 }
  let goal = { X = 3; Y = 3 }
  let path = (Pathfinding.aStar (fun _ -> true) start goal).Value
  Assert.Equal<Point list>([ { X = 0; Y = 0 }; { X = 1; Y = 1 }; { X = 2; Y = 2 }; { X = 3; Y = 3 } ], path)

[<Fact>]
let ``aStarBounded finds path within node budget`` () =
  let start = { X = 0; Y = 0 }
  let goal = { X = 4; Y = 0 }
  let result = Pathfinding.aStarBounded 32 (fun _ -> true) start goal
  Assert.True(result.IsSome)
  Assert.Equal(start, result.Value.Head)
  Assert.Equal(goal, List.last result.Value)

[<Fact>]
let ``aStarBounded returns None when blocked goal exceeds node budget`` () =
  let start = { X = 0; Y = 0 }
  let goal = { X = 5; Y = 0 }
  // Goal itself is passable, but surrounded on all 8 sides so it is unreachable
  let isPassable p =
    p = goal || abs (p.X - goal.X) > 1 || abs (p.Y - goal.Y) > 1

  let mutable visits = 0

  let trackedIsPassable p =
    visits <- visits + 1
    isPassable p

  let result = Pathfinding.aStarBounded 25 trackedIsPassable start goal
  Assert.True(result.IsNone)
  // Bounded search must stop well short of flooding an unbounded grid (25 nodes * 8 neighbors + 1 goal check)
  Assert.True(visits <= 25 * 8 + 1)
