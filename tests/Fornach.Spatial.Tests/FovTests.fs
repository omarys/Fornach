module Fornach.Spatial.Tests.FovTests

open Xunit
open Fornach.Spatial

[<Fact>]
let ``360 FOV sees all tiles in an open room up to the max radius`` () =
  let origin = { X = 5; Y = 5 }

  let cone =
    { Origin = origin
      Facing = None
      ArcDegrees = 360.0
      MaxRadius = 3 }

  let isOpaque _ = false // No obstacles
  let visible = Fov.compute isOpaque cone

  // Origin is always visible
  Assert.Contains(origin, visible)
  // Points within radius 3 are visible
  Assert.Contains({ X = 5; Y = 2 }, visible) // 3 tiles North
  Assert.Contains({ X = 8; Y = 5 }, visible) // 3 tiles East
  // Points beyond radius 3 are not visible
  Assert.DoesNotContain({ X = 5; Y = 9 }, visible) // 4 tiles South

[<Fact>]
let ``A wall casts a shadow behind it`` () =
  let origin = { X = 0; Y = 0 }

  let cone =
    { Origin = origin
      Facing = None
      ArcDegrees = 360.0
      MaxRadius = 5 }
  // A wall at (2, 0)
  let wall = { X = 2; Y = 0 }
  let behindWall = { X = 3; Y = 0 }
  let isOpaque p = p = wall

  let visible = Fov.compute isOpaque cone

  // The wall itself is visible
  Assert.Contains(wall, visible)
  // The tile behind the wall is in shadow
  Assert.DoesNotContain(behindWall, visible)

[<Fact>]
let ``Directional cone restricts vision to the facing arc`` () =
  let origin = { X = 0; Y = 0 }
  // Facing East with a 90-degree arc (+-45 degrees)
  let cone =
    { Origin = origin
      Facing = Some East
      ArcDegrees = 90.0
      MaxRadius = 5 }

  let isOpaque _ = false // No obstacles
  let visible = Fov.compute isOpaque cone

  // Due East (0 deg) is in the cone
  Assert.Contains({ X = 3; Y = 0 }, visible)
  // North-East (45 deg) is on the boundary/in the cone
  Assert.Contains({ X = 2; Y = -2 }, visible)
  // Due West (180 deg, behind the player) is pruned by the cone
  Assert.DoesNotContain({ X = -3; Y = 0 }, visible)
  // Due North (270 / -90 deg) is pruned by the cone
  Assert.DoesNotContain({ X = 0; Y = -3 }, visible)
