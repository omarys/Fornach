module Fornach.Spatial.Tests.DijkstraTests

open Xunit
open Fornach.Spatial

[<Fact>]
let ``Dijkstra map radiates distance from goal`` () =
  let goal = { X = 0; Y = 0 }
  let isPassable _ = true
  let map = Dijkstra.compute isPassable [ goal ] 3

  Assert.Equal(Some 0, Map.tryFind goal map)
  Assert.Equal(Some 1, Map.tryFind { X = 1; Y = 0 } map)
  Assert.Equal(Some 1, Map.tryFind { X = 1; Y = 1 } map) // Diagonal is 1 step
  Assert.Equal(Some 3, Map.tryFind { X = 3; Y = 0 } map)
  Assert.Equal(None, Map.tryFind { X = 4; Y = 0 } map) // Beyond maxDistance 3

[<Fact>]
let ``nextStepTowards moves towards the goal`` () =
  let goal = { X = 0; Y = 0 }
  let monster = { X = 3; Y = 0 }
  let isPassable _ = true
  let map = Dijkstra.compute isPassable [ goal ] 5

  let step = Dijkstra.nextStepTowards map monster
  Assert.True step.IsSome
  let dist = Map.find step.Value map
  Assert.Equal(2, dist)

[<Fact>]
let ``nextStepAway flees from the goal`` () =
  let goal = { X = 0; Y = 0 }
  let fleeingMonster = { X = 2; Y = 0 }
  let isPassable _ = true
  let map = Dijkstra.compute isPassable [ goal ] 5

  let step = Dijkstra.nextStepAway map fleeingMonster
  Assert.True step.IsSome
  let dist = Map.find step.Value map
  Assert.Equal(3, dist)
