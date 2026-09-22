namespace Fornach.Spatial

/// Immutable 2D integer coodinates representing a tile on the grid.
[<Struct>]
type Point =
  { X: int
    Y: int }

  /// Vector addition: p1 + p2
  static member (+)(a: Point, b: Point) = { X = a.X + b.X; Y = a.Y + b.Y }

  /// Vector subtraction: a - b
  static member (-)(a: Point, b: Point) = { X = a.X - b.X; Y = a.Y - b.Y }

  /// Scalar multiplication: p * scalar
  static member (*)(p: Point, s: int) = { X = p.X * s; Y = p.Y * s }

[<RequireQualifiedAccess>]
module Point =
  /// Coordinate origin (0, 0)
  let zero = { X = 0; Y = 0 }

  /// Convenience consctructor
  let create x y = { X = x; Y = y }

  /// Returns the 4 orthogonal cardinal neighbors (North, South, East, West)
  let cardinalNeighbors (p: Point) =
    [ { X = p.X; Y = p.Y - 1 } // North
      { X = p.X + 1; Y = p.Y } // East
      { X = p.X; Y = p.Y + 1 } // South
      { X = p.X - 1; Y = p.Y } ] // West

  /// Returns all 8 surrounding neighbors (cardinals + diagonals)
  let allNeighbors (p: Point) =
    [ { X = p.X - 1; Y = p.Y - 1 }
      { X = p.X; Y = p.Y - 1 }
      { X = p.X + 1; Y = p.Y - 1 }
      { X = p.X - 1; Y = p.Y }
      { X = p.X + 1; Y = p.Y }
      { X = p.X - 1; Y = p.Y + 1 }
      { X = p.X; Y = p.Y + 1 }
      { X = p.X + 1; Y = p.Y + 1 } ]
