namespace Fornach.Spatial

/// 8-way compass directions for facing vectors and movement
type Direction =
  | North
  | NorthEast
  | East
  | SouthEast
  | South
  | SouthWest
  | West
  | NorthWest

[<RequireQualifiedAccess>]
module Direction =
  /// Converts a compass direction into a unit delta Point on a grid (Y-down)
  let toDelta =
    function
    | Direction.North -> { X = 0; Y = -1 }
    | Direction.NorthEast -> { X = 1; Y = -1 }
    | Direction.East -> { X = 1; Y = 0 }
    | Direction.SouthEast -> { X = 1; Y = 1 }
    | Direction.South -> { X = 0; Y = 1 }
    | Direction.SouthWest -> { X = -1; Y = 1 }
    | Direction.West -> { X = -1; Y = 0 }
    | Direction.NorthWest -> { X = -1; Y = -1 }

  /// Returns the opposite direction (180 degree flip)
  let opposite =
    function
    | Direction.North -> Direction.South
    | Direction.NorthEast -> Direction.SouthWest
    | Direction.East -> Direction.West
    | Direction.SouthEast -> Direction.NorthWest
    | Direction.South -> Direction.North
    | Direction.SouthWest -> Direction.NorthEast
    | Direction.West -> Direction.East
    | Direction.NorthWest -> Direction.SouthEast

  /// Returns the standard angle in degrees for each direction (East = 0, South = 90 for Y-down)
  let toDegrees =
    function
    | Direction.East -> 0.0
    | Direction.SouthEast -> 45.0
    | Direction.South -> 90.0
    | Direction.SouthWest -> 135.0
    | Direction.West -> 180.0
    | Direction.NorthWest -> 225.0
    | Direction.North -> 270.0
    | Direction.NorthEast -> 315.0
