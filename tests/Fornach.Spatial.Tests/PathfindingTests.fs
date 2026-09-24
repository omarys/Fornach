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
