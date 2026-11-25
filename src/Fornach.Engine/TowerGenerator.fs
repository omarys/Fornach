namespace Fornach.Engine

open System
open Fornach.Domain
open Fornach.Spatial

module TowerGenerator =

  /// Distinct architectural room archetypes for tactical variety
  type private RoomArchetype =
    | OpenPlaza
    | ColonnadeHall
    | GatehouseBastion
    | HazardCrucible
    | RotundaSanctuary
    | RuinedScatter

  /// Theme-driven layout parameters controlling chamber geometry, corridors, loops, and hazards
  type private BiomeLayoutProfile =
    { MinRoomW: int
      MaxRoomW: int
      MinRoomH: int
      MaxRoomH: int
      MainCorridorWidth: int
      MaxLoops: int
      PreferredArchetypes: RoomArchetype[]
      HazardClusterCount: int
      CrucibleUsesChasm: bool }

  let private layoutProfileForTheme (theme: FloorTheme) : BiomeLayoutProfile =
    match theme with
    | QuarryPlazas ->
      { MinRoomW = 10; MaxRoomW = 16; MinRoomH = 8; MaxRoomH = 12
        MainCorridorWidth = 2; MaxLoops = 3
        PreferredArchetypes = [| ColonnadeHall; GatehouseBastion; RuinedScatter |]
        HazardClusterCount = 1; CrucibleUsesChasm = false }
    | PineCloisters ->
      { MinRoomW = 11; MaxRoomW = 17; MinRoomH = 9; MaxRoomH = 13
        MainCorridorWidth = 2; MaxLoops = 3
        PreferredArchetypes = [| RuinedScatter; ColonnadeHall; RotundaSanctuary |]
        HazardClusterCount = 2; CrucibleUsesChasm = false }
    | BasaltCalderas ->
      { MinRoomW = 10; MaxRoomW = 15; MinRoomH = 8; MaxRoomH = 12
        MainCorridorWidth = 2; MaxLoops = 2
        PreferredArchetypes = [| HazardCrucible; GatehouseBastion; RuinedScatter |]
        HazardClusterCount = 2; CrucibleUsesChasm = false }
    | TempestTerraces ->
      { MinRoomW = 10; MaxRoomW = 16; MinRoomH = 8; MaxRoomH = 11
        MainCorridorWidth = 1; MaxLoops = 3
        PreferredArchetypes = [| HazardCrucible; ColonnadeHall; GatehouseBastion |]
        HazardClusterCount = 2; CrucibleUsesChasm = true }
    | SunkenBoulevards ->
      { MinRoomW = 11; MaxRoomW = 16; MinRoomH = 8; MaxRoomH = 12
        MainCorridorWidth = 2; MaxLoops = 3
        PreferredArchetypes = [| ColonnadeHall; HazardCrucible; RuinedScatter |]
        HazardClusterCount = 2; CrucibleUsesChasm = false }
    | ElysianSanctuaries ->
      { MinRoomW = 11; MaxRoomW = 16; MinRoomH = 9; MaxRoomH = 13
        MainCorridorWidth = 2; MaxLoops = 3
        PreferredArchetypes = [| RotundaSanctuary; ColonnadeHall; RuinedScatter |]
        HazardClusterCount = 1; CrucibleUsesChasm = false }
    | CelestialSpires ->
      { MinRoomW = 9; MaxRoomW = 14; MinRoomH = 8; MaxRoomH = 11
        MainCorridorWidth = 1; MaxLoops = 4
        PreferredArchetypes = [| RotundaSanctuary; HazardCrucible; GatehouseBastion |]
        HazardClusterCount = 1; CrucibleUsesChasm = true }

  /// Resolves the thematic surface type for a given floor theme
  let private surfaceForTheme (theme: FloorTheme) : SurfaceType =
    match theme with
    | QuarryPlazas -> PavedStone
    | PineCloisters -> ForestMoss
    | BasaltCalderas -> BasaltRock
    | TempestTerraces -> PavedStone
    | SunkenBoulevards -> ShallowWater
    | ElysianSanctuaries -> LilyPetals
    | CelestialSpires -> StarlitGlass

  /// Resolves the hazard type for a given floor theme
  let private hazardForTheme (theme: FloorTheme) : HazardType =
    match theme with
    | BasaltCalderas -> LavaRift
    | QuarryPlazas -> AcidSlag
    | TempestTerraces
    | SunkenBoulevards -> DeepCurrent
    | PineCloisters
    | ElysianSanctuaries
    | CelestialSpires -> CalmingSpores

  /// Generates an expansive architectural plaza
  type private Plaza =
    { Id: int
      X: int
      Y: int
      Width: int
      Height: int }

    member this.Center : Point =
      { X = this.X + this.Width / 2
        Y = this.Y + this.Height / 2 }

    member this.Contains (p: Point) : bool =
      p.X >= this.X && p.X < this.X + this.Width &&
      p.Y >= this.Y && p.Y < this.Y + this.Height

    member this.IsBorder (p: Point) : bool =
      this.Contains p &&
      (p.X = this.X || p.X = this.X + this.Width - 1 ||
       p.Y = this.Y || p.Y = this.Y + this.Height - 1)

    member this.IsStrictInterior (p: Point) : bool =
      p.X > this.X && p.X < this.X + this.Width - 1 &&
      p.Y > this.Y && p.Y < this.Y + this.Height - 1

  /// Generates a complete Tower floor layout with themed room archetypes, perimeter archways,
  /// tactical alcoves, and role-aware enemy formations.
  let generateFloorForPlane (seed: int) (floorNumber: int) (isStory: bool) (playerPlane: Plane) : TowerFloor =
    let rng = Random(seed + floorNumber * 1009)

    // 1. Determine Floor Theme & Biome Layout Profile
    let themeOfIndex (index: int) =
      match index with
      | 0 -> QuarryPlazas
      | 1 -> PineCloisters
      | 2 -> BasaltCalderas
      | 3 -> TempestTerraces
      | 4 -> SunkenBoulevards
      | 5 -> ElysianSanctuaries
      | _ -> CelestialSpires

    let theme =
      if isStory then
        // Story floor 1 is the rain-soaked forest tutorial; the six grief stages shift
        // down one slot (floor 2 = Guilt quarry ... floor 7 = Acceptance meadow).
        if floorNumber = 1 then PineCloisters
        else themeOfIndex ((floorNumber - 2) % 7)
      else
        themeOfIndex ((floorNumber - 1) % 7)

    let profile = layoutProfileForTheme theme
    let surface = surfaceForTheme theme
    let hazard = hazardForTheme theme

    let mapWidth = 88
    let mapHeight = 56

    // Flat mutable grid during generation (converted once to Map<Point, TowerTile> at the end)
    let grid = Array.create (mapWidth * mapHeight) Chasm
    let inline inBounds x y = x >= 0 && x < mapWidth && y >= 0 && y < mapHeight
    let inline getTile x y = if inBounds x y then grid.[y * mapWidth + x] else Chasm
    let inline setTile x y t = if inBounds x y then grid.[y * mapWidth + x] <- t
    let inline getPt (p: Point) = getTile p.X p.Y
    let inline setPt (p: Point) t = setTile p.X p.Y t

    let chebyshev (a: Point) (b: Point) =
      Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y))

    let euclidean (a: Point) (b: Point) =
      let dx = float (a.X - b.X)
      let dy = float (a.Y - b.Y)
      sqrt (dx * dx + dy * dy)

    // 2. Rejection-sample 9 to 12 chambers, each kept at least 2 tiles of chasm clear of the last
    let roomCount = 9 + rng.Next(0, 4)

    let inflated (r: Plaza) =
      { r with
          X = r.X - 2
          Y = r.Y - 2
          Width = r.Width + 4
          Height = r.Height + 4 }

    let overlaps (a: Plaza) (b: Plaza) =
      a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height

    let mutable rooms : Plaza list = []
    let mutable attempts = 0

    while rooms.Length < roomCount && attempts < 350 do
      attempts <- attempts + 1
      let w = rng.Next(profile.MinRoomW, profile.MaxRoomW + 1)
      let h = rng.Next(profile.MinRoomH, profile.MaxRoomH + 1)

      let candidate =
        { Id = rooms.Length
          X = rng.Next(2, mapWidth - w - 2)
          Y = rng.Next(2, mapHeight - h - 2)
          Width = w
          Height = h }

      if not (rooms |> List.exists (fun r -> overlaps (inflated candidate) (inflated r))) then
        rooms <- rooms @ [ candidate ]

    // A starved sample would leave the six encounter anchors without rooms to sit in
    let fallbackRooms =
      [ 4, 4, 14, 12
        26, 4, 14, 12
        44, 4, 16, 12
        6, 24, 14, 14
        26, 24, 14, 14
        44, 24, 16, 14 ]
      |> List.mapi (fun i (px, py, pw, ph) ->
        { Id = i
          X = px
          Y = py
          Width = pw
          Height = ph })

    let plazas = if rooms.Length >= 6 then rooms else fallbackRooms
    let plazaArr = List.toArray plazas
    let plazaCount = plazaArr.Length
    let plazaAt (index: int) : Plaza = plazaArr.[Math.Min(index, plazaCount - 1)]

    // 3. Build room connectivity graph (spanning tree + biome-capped loops) before carving
    let adj = Array.init plazaCount (fun _ -> ResizeArray<int>())
    let mainEdges = ResizeArray<int * int>()
    let loopEdges = ResizeArray<int * int>()
    let mutable linkSet = Set.empty<int * int>

    let addEdge (isLoop: bool) (i: int) (j: int) =
      let key = (Math.Min(i, j), Math.Max(i, j))
      if not (linkSet.Contains key) then
        linkSet <- linkSet.Add key
        adj.[i].Add j
        adj.[j].Add i
        if isLoop then loopEdges.Add(i, j) else mainEdges.Add(i, j)

    for i in 1 .. plazaCount - 1 do
      let nearest =
        [ 0 .. i - 1 ] |> List.minBy (fun j -> chebyshev plazaArr.[j].Center plazaArr.[i].Center)
      addEdge false i nearest

    let mutable loopCount = 0
    for i in 0 .. plazaCount - 1 do
      for j in i + 1 .. plazaCount - 1 do
        if loopCount < profile.MaxLoops
           && not (linkSet.Contains((i, j)))
           && chebyshev plazaArr.[i].Center plazaArr.[j].Center < 30
           && rng.Next(4) = 0 then
          addEdge true i j
          loopCount <- loopCount + 1

    // 4. Assign Spawn, Stairway, Guardian, Shrine, NPC, and Chest rooms using graph distances
    let origin = { X = 0; Y = 0 }
    let spawnIdx =
      [ 0 .. plazaCount - 1 ] |> List.minBy (fun i -> chebyshev plazaArr.[i].Center origin)
    let spawnPlaza = plazaArr.[spawnIdx]
    let spawn = spawnPlaza.Center

    // BFS graph distance from spawnIdx
    let graphDist = Array.create plazaCount Int32.MaxValue
    let bfsQ = System.Collections.Generic.Queue<int>()
    graphDist.[spawnIdx] <- 0
    bfsQ.Enqueue spawnIdx
    while bfsQ.Count > 0 do
      let u = bfsQ.Dequeue()
      for v in adj.[u] do
        if graphDist.[v] = Int32.MaxValue then
          graphDist.[v] <- graphDist.[u] + 1
          bfsQ.Enqueue v

    let stairwayIdx =
      [ 0 .. plazaCount - 1 ]
      |> List.filter (fun i -> i <> spawnIdx)
      |> List.maxBy (fun i -> graphDist.[i], chebyshev plazaArr.[i].Center spawn)
    let stairwayPlaza = plazaArr.[stairwayIdx]
    let stairway = stairwayPlaza.Center

    // Guardian/Boss: never in spawn or stairway; placed in the deepest chamber approaching the stairway
    let guardianIdx =
      let away = [ 0 .. plazaCount - 1 ] |> List.filter (fun i -> i <> spawnIdx && i <> stairwayIdx)
      match away with
      | [] -> spawnIdx
      | pool ->
        pool
        |> List.minBy (fun i -> chebyshev plazaArr.[i].Center stairway, -graphDist.[i])
    let guardianPlaza = plazaArr.[guardianIdx]

    // Two Shrines at mid-floor hubs (excluding spawn, stairway, and guardian when possible)
    let maxDist = graphDist |> Array.filter (fun d -> d < Int32.MaxValue) |> Array.max |> max 1
    let midDist = float maxDist / 2.0
    let shrineCandidates =
      [ 0 .. plazaCount - 1 ]
      |> List.filter (fun i -> i <> spawnIdx && i <> stairwayIdx && i <> guardianIdx)
      |> List.sortBy (fun i -> Math.Abs(float graphDist.[i] - midDist), -adj.[i].Count, i)

    let shrineIdx1, shrineIdx2 =
      match shrineCandidates with
      | a :: b :: _ -> a, b
      | [ a ] -> a, Math.Min(5, plazaCount - 1)
      | [] -> Math.Min(2, plazaCount - 1), Math.Min(5, plazaCount - 1)

    let npcIdx =
      let near =
        [ 0 .. plazaCount - 1 ]
        |> List.filter (fun i -> i <> spawnIdx && i <> stairwayIdx && i <> guardianIdx && i <> shrineIdx1 && i <> shrineIdx2)
        |> List.sortBy (fun i -> graphDist.[i], chebyshev plazaArr.[i].Center spawn)
      match near with
      | i :: _ -> i
      | [] -> Math.Min(1, plazaCount - 1)
    let npcPlaza = plazaArr.[npcIdx]

    let chestIdx =
      let midFar =
        [ 0 .. plazaCount - 1 ]
        |> List.filter (fun i -> i <> spawnIdx && i <> stairwayIdx && i <> shrineIdx1 && i <> shrineIdx2 && i <> npcIdx)
        |> List.sortByDescending (fun i -> graphDist.[i], chebyshev plazaArr.[i].Center spawn)
      match midFar with
      | i :: _ -> i
      | [] -> Math.Min(3, plazaCount - 1)
    let chestPlaza = plazaArr.[chestIdx]

    // 5. Assign Room Archetypes
    let roomArchetypes = Array.create plazaCount OpenPlaza
    for i in 0 .. plazaCount - 1 do
      if i = spawnIdx then
        roomArchetypes.[i] <- OpenPlaza
      elif i = guardianIdx || i = shrineIdx1 || i = shrineIdx2 || i = stairwayIdx then
        roomArchetypes.[i] <- RotundaSanctuary
      else
        let arch = profile.PreferredArchetypes.[(i + floorNumber) % profile.PreferredArchetypes.Length]
        // Keep NPC room from having a central chasm/hazard crucible
        if i = npcIdx && arch = HazardCrucible then
          roomArchetypes.[i] <- ColonnadeHall
        else
          roomArchetypes.[i] <- arch

    // Helper: carve a contiguous 3-5 tile hazard cluster inside a room without hitting center or borders
    let carveHazardCluster (plaza: Plaza) =
      if plaza.Width >= 7 && plaza.Height >= 6 then
        let cx = rng.Next(plaza.X + 2, plaza.X + plaza.Width - 2)
        let cy = rng.Next(plaza.Y + 2, plaza.Y + plaza.Height - 2)
        let clusterSize = rng.Next(3, 6)
        let mutable curX = cx
        let mutable curY = cy
        for _ in 1 .. clusterSize do
          let p = { X = curX; Y = curY }
          if plaza.IsStrictInterior p && p <> plaza.Center && getPt p = Floor surface then
            setPt p (Hazard hazard)
          let dir = rng.Next(4)
          let nx = curX + (if dir = 0 then 1 elif dir = 1 then -1 else 0)
          let ny = curY + (if dir = 2 then 1 elif dir = 3 then -1 else 0)
          if nx > plaza.X + 1 && nx < plaza.X + plaza.Width - 2 && ny > plaza.Y + 1 && ny < plaza.Y + plaza.Height - 2 then
            curX <- nx
            curY <- ny

    // 6. Carve Plaza Floors & Archetype Interior Geometry
    for i in 0 .. plazaCount - 1 do
      let plaza = plazaArr.[i]
      let arch = roomArchetypes.[i]
      let x0, x1 = plaza.X, plaza.X + plaza.Width - 1
      let y0, y1 = plaza.Y, plaza.Y + plaza.Height - 1
      let c = plaza.Center

      match arch with
      | OpenPlaza ->
        for py in y0 .. y1 do
          for px in x0 .. x1 do
            let isBorder = px = x0 || px = x1 || py = y0 || py = y1
            if isBorder && (px % 3 = 0 || py % 3 = 0) then
              setTile px py Pillar
            else
              setTile px py (Floor surface)

      | ColonnadeHall ->
        for py in y0 .. y1 do
          for px in x0 .. x1 do
            let isBorder = px = x0 || px = x1 || py = y0 || py = y1
            let isAisleRow = (py = y0 + 2 || py = y1 - 2) && px >= x0 + 2 && px <= x1 - 2 && (px - x0) % 2 = 0
            if isBorder && (px % 2 = 0 || py % 2 = 0) then
              setTile px py Pillar
            elif isAisleRow && (px <> c.X || py <> c.Y) then
              setTile px py Pillar
            else
              setTile px py (Floor surface)
        for _ in 1 .. Math.Min(1, profile.HazardClusterCount) do
          carveHazardCluster plaza

      | GatehouseBastion ->
        for py in y0 .. y1 do
          for px in x0 .. x1 do
            let isBorder = px = x0 || px = x1 || py = y0 || py = y1
            if isBorder then
              setTile px py Pillar
            else
              setTile px py (Floor surface)
        // Interior flank pillars framing the central choke
        if plaza.Width >= 10 && plaza.Height >= 8 then
          setTile (c.X - 2) (c.Y - 1) Pillar
          setTile (c.X + 2) (c.Y - 1) Pillar
          setTile (c.X - 2) (c.Y + 1) Pillar
          setTile (c.X + 2) (c.Y + 1) Pillar

      | HazardCrucible ->
        for py in y0 .. y1 do
          for px in x0 .. x1 do
            let isBorder = px = x0 || px = x1 || py = y0 || py = y1
            if isBorder && (px % 3 = 0 || py % 3 = 0) then
              setTile px py Pillar
            else
              setTile px py (Floor surface)
        // Central 3x3 (or 3x2) hazard/chasm island ringed by corner pillars + wide walkable perimeter
        let halfH = if plaza.Height >= 10 then 1 else 0
        for dy in -halfH .. 1 do
          for dx in -1 .. 1 do
            let hx, hy = c.X + dx, c.Y + dy
            if hx > x0 + 1 && hx < x1 - 1 && hy > y0 + 1 && hy < y1 - 1 then
              if profile.CrucibleUsesChasm && dx = 0 && dy = 0 then
                setTile hx hy Chasm
              else
                setTile hx hy (Hazard hazard)
        // Corner pillars framing the crucible (leaving 2-tile walkable ring outside)
        if plaza.Width >= 10 && plaza.Height >= 8 then
          setTile (c.X - 2) (c.Y - 2) Pillar
          setTile (c.X + 2) (c.Y - 2) Pillar
          setTile (c.X - 2) (c.Y + 2) Pillar
          setTile (c.X + 2) (c.Y + 2) Pillar

      | RotundaSanctuary ->
        for py in y0 .. y1 do
          for px in x0 .. x1 do
            let distCorner =
              Math.Min(px - x0, x1 - px) + Math.Min(py - y0, y1 - py)
            if distCorner < 2 then
              setTile px py Chasm
            elif distCorner = 2 || px = x0 || px = x1 || py = y0 || py = y1 then
              if distCorner = 2 || (px + py) % 2 = 0 then
                setTile px py Pillar
              else
                setTile px py (Floor surface)
            else
              setTile px py (Floor surface)
        // 4 symmetrical sanctuary pillars framing the open central arena
        if plaza.Width >= 10 && plaza.Height >= 8 then
          setTile (c.X - 2) (c.Y - 2) Pillar
          setTile (c.X + 2) (c.Y - 2) Pillar
          setTile (c.X - 2) (c.Y + 2) Pillar
          setTile (c.X + 2) (c.Y + 2) Pillar

      | RuinedScatter ->
        for py in y0 .. y1 do
          for px in x0 .. x1 do
            let isBorder = px = x0 || px = x1 || py = y0 || py = y1
            if isBorder && (px % 4 = 0 || py % 4 = 0) then
              setTile px py Pillar
            else
              setTile px py (Floor surface)
        // 2-3 staggered 2x1 or L-shaped pillar cover clusters
        let coverCount = 2 + rng.Next(0, 2)
        for _ in 1 .. coverCount do
          if plaza.Width >= 9 && plaza.Height >= 7 then
            let rx = rng.Next(x0 + 2, x1 - 2)
            let ry = rng.Next(y0 + 2, y1 - 2)
            let pts =
              if rng.Next(2) = 0 then [ { X = rx; Y = ry }; { X = rx + 1; Y = ry } ]
              else [ { X = rx; Y = ry }; { X = rx + 1; Y = ry }; { X = rx; Y = ry + 1 } ]
            if pts |> List.forall (fun p -> p <> c && chebyshev p c >= 2 && plaza.IsStrictInterior p) then
              for p in pts do setPt p Pillar
        for _ in 1 .. profile.HazardClusterCount do
          carveHazardCluster plaza

    // 7. Solidify room borders facing another room within aggroRadius (8) to prevent chasm-gap LOS aggro
    let aggroGap = 8
    for i in 0 .. plazaCount - 1 do
      for j in i + 1 .. plazaCount - 1 do
        let a = plazaArr.[i]
        let b = plazaArr.[j]
        // Horizontal gap check (vertical overlap)
        let vOverlapMin = Math.Max(a.Y, b.Y)
        let vOverlapMax = Math.Min(a.Y + a.Height - 1, b.Y + b.Height - 1)
        if vOverlapMin <= vOverlapMax then
          if b.X >= a.X + a.Width && b.X - (a.X + a.Width) <= aggroGap then
            for y in vOverlapMin .. vOverlapMax do
              if getTile (a.X + a.Width - 1) y <> Chasm then setTile (a.X + a.Width - 1) y Pillar
              if getTile b.X y <> Chasm then setTile b.X y Pillar
          elif a.X >= b.X + b.Width && a.X - (b.X + b.Width) <= aggroGap then
            for y in vOverlapMin .. vOverlapMax do
              if getTile (b.X + b.Width - 1) y <> Chasm then setTile (b.X + b.Width - 1) y Pillar
              if getTile a.X y <> Chasm then setTile a.X y Pillar
        // Vertical gap check (horizontal overlap)
        let hOverlapMin = Math.Max(a.X, b.X)
        let hOverlapMax = Math.Min(a.X + a.Width - 1, b.X + b.Width - 1)
        if hOverlapMin <= hOverlapMax then
          if b.Y >= a.Y + a.Height && b.Y - (a.Y + a.Height) <= aggroGap then
            for x in hOverlapMin .. hOverlapMax do
              if getTile x (a.Y + a.Height - 1) <> Chasm then setTile x (a.Y + a.Height - 1) Pillar
              if getTile x b.Y <> Chasm then setTile x b.Y Pillar
          elif a.Y >= b.Y + b.Height && a.Y - (b.Y + b.Height) <= aggroGap then
            for x in hOverlapMin .. hOverlapMax do
              if getTile x (b.Y + b.Height - 1) <> Chasm then setTile x (b.Y + b.Height - 1) Pillar
              if getTile x a.Y <> Chasm then setTile x a.Y Pillar

    // 8. Carve Causeways with True Perimeter Threshold Archways & Mixed Corridor Widths
    let roomThresholds = Array.init plazaCount (fun _ -> ResizeArray<Point>())

    let containingRoom (p: Point) : int =
      let mutable found = -1
      let mutable idx = 0
      while found = -1 && idx < plazaCount do
        if plazaArr.[idx].Contains p then found <- idx
        idx <- idx + 1
      found

    let stampCorridorTile (x: int) (y: int) (isArchwayCandidate: bool) : unit =
      if inBounds x y then
        let pt = { X = x; Y = y }
        let rIdx = containingRoom pt
        if rIdx = -1 then
          setTile x y (Floor surface)
        else
          let plaza = plazaArr.[rIdx]
          if plaza.IsBorder pt then
            if isArchwayCandidate then
              setTile x y Archway
              roomThresholds.[rIdx].Add pt
            else
              setTile x y (Floor surface)
            // Guarantee cardinal neighbors inside the room behind the threshold are clear of pillars/chasms
            // (so 4-way movement is never pinched even if a corridor enters near a room corner or rotunda cut)
            for (cdx, cdy) in [| (1, 0); (-1, 0); (0, 1); (0, -1) |] do
              let nx, ny = x + cdx, y + cdy
              let np = { X = nx; Y = ny }
              if plaza.Contains np && (getTile nx ny = Pillar || getTile nx ny = Chasm) then
                if not (plaza.IsBorder np) then
                  setTile nx ny (Floor surface)
          elif getTile x y = Pillar || getTile x y = Chasm then
            // Clear path through clipped rotunda corner or interior pillar if corridor crosses it
            setTile x y (Floor surface)

    let carveCauseway (p1: Point) (p2: Point) (width: int) =
      let stepX = if p2.X >= p1.X then 1 else -1
      let stepY = if p2.Y >= p1.Y then 1 else -1
      let spreadMax = Math.Max(0, width - 1)

      // Horizontal leg from (p1.X, p1.Y) to (p2.X, p1.Y)
      for dy in 0 .. spreadMax do
        let mutable x = p1.X
        let mutable doneX = false
        while not doneX do
          stampCorridorTile x (p1.Y + dy) (dy = 0)
          if x = p2.X then doneX <- true else x <- x + stepX

      // Vertical leg from (p2.X, p1.Y) to (p2.X, p2.Y)
      for dx in 0 .. spreadMax do
        let mutable y = p1.Y
        let mutable doneY = false
        while not doneY do
          stampCorridorTile (p2.X + dx) y (dx = 0)
          if y = p2.Y then doneY <- true else y <- y + stepY

    // Main spanning-tree arteries use profile.MainCorridorWidth (1 or 2); loops are always 1-wide chokepoints
    for (i, j) in mainEdges do
      carveCauseway plazaArr.[i].Center plazaArr.[j].Center profile.MainCorridorWidth

    for (i, j) in loopEdges do
      carveCauseway plazaArr.[i].Center plazaArr.[j].Center 1

    // 9. Tactical Alcove Dead-End Spurs (1-wide neck -> 4x4 mini-chamber with reward or oasis)
    let alcoves = ResizeArray<Point * Point * Point>() // (archwayPt, neckEndPt, chamberCenter)
    let alcoveBoxes = ResizeArray<Plaza>()

    let tryCarveAlcove (roomIdx: int) =
      let room = plazaArr.[roomIdx]
      let dirs = [| (1, 0); (-1, 0); (0, 1); (0, -1) |]
      let startDir = rng.Next(4)
      let mutable placed = false
      let mutable dIdx = 0
      while not placed && dIdx < 4 do
        let dx, dy = dirs.[(startDir + dIdx) % 4]
        dIdx <- dIdx + 1
        let neckLen = rng.Next(2, 5)
        let archX, archY =
          if dx > 0 then room.X + room.Width - 1, room.Center.Y
          elif dx < 0 then room.X, room.Center.Y
          elif dy > 0 then room.Center.X, room.Y + room.Height - 1
          else room.Center.X, room.Y

        let neckEndX = archX + dx * neckLen
        let neckEndY = archY + dy * neckLen

        // 4x4 chamber positioned right beyond the neck
        let chX = if dx > 0 then neckEndX + 1 elif dx < 0 then neckEndX - 4 else neckEndX - 2
        let chY = if dy > 0 then neckEndY + 1 elif dy < 0 then neckEndY - 4 else neckEndY - 2
        let chamberBox = { Id = -1; X = chX; Y = chY; Width = 4; Height = 4 }

        let boxValid =
          chX >= 2 && chY >= 2 && chX + 4 < mapWidth - 2 && chY + 4 < mapHeight - 2
          && not (plazas |> List.exists (fun r -> overlaps (inflated chamberBox) r))
          && not (alcoveBoxes |> Seq.exists (fun ab -> overlaps (inflated chamberBox) ab))

        if boxValid then
          // Punch doorway on room border + clear 1 tile inside
          let archPt = { X = archX; Y = archY }
          setPt archPt Archway
          roomThresholds.[roomIdx].Add archPt
          setTile (archX - dx) (archY - dy) (Floor surface)
          // Carve 1-wide neck
          for s in 1 .. neckLen do
            setTile (archX + dx * s) (archY + dy * s) (Floor surface)
          // Carve 4x4 alcove chamber (walkable interior + corner pillars; never overwrite existing corridors/archways)
          for py in chY .. chY + 3 do
            for px in chX .. chX + 3 do
              let isCorner = (px = chX || px = chX + 3) && (py = chY || py = chY + 3)
              if isCorner then
                if getTile px py = Chasm then setTile px py Pillar
              else
                setTile px py (Floor surface)
          // Connect neck into chamber edge
          setTile (neckEndX + dx) (neckEndY + dy) (Floor surface)
          let chamberCenter = { X = chX + 1; Y = chY + 1 }
          let neckEndPt = { X = neckEndX; Y = neckEndY }
          alcoves.Add(archPt, neckEndPt, chamberCenter)
          alcoveBoxes.Add chamberBox
          placed <- true

    let alcoveTarget = rng.Next(2, 5)
    let mutable alcoveTries = 0
    while alcoves.Count < alcoveTarget && alcoveTries < plazaCount * 2 do
      let rIdx = rng.Next(plazaCount)
      if rIdx <> spawnIdx then tryCarveAlcove rIdx
      alcoveTries <- alcoveTries + 1

    // Make spawn and stairway tiles guaranteed walkable
    setPt spawn (Floor surface)

    // Door State logic based on floor progression
    let keyIdForFloor = sprintf "key_floor_%d" floorNumber
    let questIdForFloor = sprintf "quest_floor_%d" floorNumber

    let doorState, requiredKey, requiredQuest =
      if isStory && floorNumber <= 7 then
        let kId = sprintf "keystone_stage_%d" floorNumber
        let bossName =
          match floorNumber with
          | 1 -> "Bandit Leader"
          | 2 -> "Aspect of Guilt"
          | 3 -> "Aspect of Denial"
          | 4 -> "Aspect of Anger"
          | 5 -> "Aspect of Bargaining"
          | 6 -> "Aspect of Depression"
          | _ -> "Aspect of Acceptance"
        let hint =
          if floorNumber = 1 then "Held by the Bandit Leader at the cave-mouth descending to the quarry"
          else sprintf "Held by the %s in the central arena" bossName
        DoorState.LockedByKey(kId, sprintf "%s's Keystone" bossName, hint), Some kId, None
      else
        match floorNumber % 3 with
        | 1 ->
          DoorState.Open, None, None
        | 2 ->
          let hint = sprintf "Held by the Floor %d Guardian in the far terrace" floorNumber
          DoorState.LockedByKey(keyIdForFloor, sprintf "Floor %d Keystone" floorNumber, hint), Some keyIdForFloor, None
        | _ ->
          let req = sprintf "Speak with the Floor %d Scholar and recover the lost ancient archive" floorNumber
          DoorState.LockedByQuest(questIdForFloor, sprintf "The Ascension Trial of Floor %d" floorNumber, req), None, Some questIdForFloor

    setPt stairway (StairwayPortal doorState)

    // 10. Verify Full 4-Way (Cardinal) Walkable Connectivity
    // Ensures every room (including HazardCrucible rooms with chasm centers), every alcove, the stairway,
    // and every walkable tile on the floor is 4-way reachable from spawn with no pillar/monolith blockages.
    let cardinalOffsets = [| (1, 0); (-1, 0); (0, 1); (0, -1) |]

    let computeCardinalReachable () =
      let visited = Array.zeroCreate<bool> (mapWidth * mapHeight)
      let q = System.Collections.Generic.Queue<Point>()
      visited.[spawn.Y * mapWidth + spawn.X] <- true
      q.Enqueue spawn
      while q.Count > 0 do
        let cur = q.Dequeue()
        for (dx, dy) in cardinalOffsets do
          let nx, ny = cur.X + dx, cur.Y + dy
          if inBounds nx ny then
            let idx = ny * mapWidth + nx
            if not visited.[idx] && grid.[idx].IsWalkable then
              visited.[idx] <- true
              q.Enqueue { X = nx; Y = ny }
      visited

    let carveCardinalBridge (fromPt: Point) (toPt: Point) =
      let stepX = if toPt.X >= fromPt.X then 1 else -1
      let stepY = if toPt.Y >= fromPt.Y then 1 else -1
      let mutable x = fromPt.X
      let mutable doneX = false
      while not doneX do
        if not (getTile x fromPt.Y).IsWalkable then
          setTile x fromPt.Y (Floor surface)
        if x = toPt.X then doneX <- true else x <- x + stepX
      let mutable y = fromPt.Y
      let mutable doneY = false
      while not doneY do
        if not (getTile toPt.X y).IsWalkable then
          setTile toPt.X y (Floor surface)
        if y = toPt.Y then doneY <- true else y <- y + stepY

    let findNearestReachable (visited: bool[]) (target: Point) : Point =
      let mutable best = spawn
      let mutable bestDist = Math.Abs(spawn.X - target.X) + Math.Abs(spawn.Y - target.Y)
      for y in 0 .. mapHeight - 1 do
        let rowOffset = y * mapWidth
        for x in 0 .. mapWidth - 1 do
          if visited.[rowOffset + x] then
            let d = Math.Abs(x - target.X) + Math.Abs(y - target.Y)
            if d < bestDist then
              bestDist <- d
              best <- { X = x; Y = y }
      best

    let ensureReachable (target: Point) =
      let visited = computeCardinalReachable ()
      if not visited.[target.Y * mapWidth + target.X] then
        let anchor = findNearestReachable visited target
        carveCardinalBridge target anchor
        if target = stairway then
          setPt target (StairwayPortal doorState)
        elif not (getPt target).IsWalkable then
          setPt target (Floor surface)

    let findWalkableTileIn (plaza: Plaza) : Point =
      if (getPt plaza.Center).IsWalkable then plaza.Center
      else
        let mutable found : Point option = None
        for py in plaza.Y + 1 .. plaza.Y + plaza.Height - 2 do
          for px in plaza.X + 1 .. plaza.X + plaza.Width - 2 do
            if found.IsNone && (getTile px py).IsWalkable then
              found <- Some { X = px; Y = py }
        match found with
        | Some p -> p
        | None ->
          setPt plaza.Center (Floor surface)
          plaza.Center

    ensureReachable stairway
    for i in 0 .. plazaCount - 1 do
      ensureReachable (findWalkableTileIn plazaArr.[i])
    for (_, _, alcoveCenter) in alcoves do
      ensureReachable alcoveCenter

    // Final sweep: repair any remaining disconnected walkable pocket (e.g. a side aisle or corner
    // isolated by interior pillars) so 100% of walkable tiles form one 4-way connected component.
    let mutable disconnectedRepair = true
    let mutable repairGuard = 0
    while disconnectedRepair && repairGuard < 32 do
      repairGuard <- repairGuard + 1
      let visited = computeCardinalReachable ()
      let mutable stray : Point option = None
      let mutable y = 0
      while stray.IsNone && y < mapHeight do
        let rowOffset = y * mapWidth
        let mutable x = 0
        while stray.IsNone && x < mapWidth do
          if not visited.[rowOffset + x] && grid.[rowOffset + x].IsWalkable then
            stray <- Some { X = x; Y = y }
          x <- x + 1
        y <- y + 1
      match stray with
      | Some pt ->
        let anchor = findNearestReachable visited pt
        carveCardinalBridge pt anchor
      | None ->
        disconnectedRepair <- false

    // 11. Populate Interactive Entities
    let mutable entities = Map.empty<Point, TowerEntity>

    let findCleanTileIn (plaza: Plaza) (preferred: Point) : Point =
      let isClean (p: Point) =
        p <> spawn
        && p <> stairway
        && plaza.Contains p
        && not (Map.containsKey p entities)
        && match getPt p with Floor _ | Archway -> true | _ -> false
      if isClean preferred then preferred
      else
        let mutable best : Point option = None
        let mutable bestDist = Int32.MaxValue
        for py in plaza.Y + 1 .. plaza.Y + plaza.Height - 2 do
          for px in plaza.X + 1 .. plaza.X + plaza.Width - 2 do
            let p = { X = px; Y = py }
            if isClean p then
              let d = chebyshev p preferred
              if d < bestDist then
                bestDist <- d
                best <- Some p
        match best with
        | Some p -> p
        | None ->
          setPt preferred (Floor surface)
          preferred

    // A. Battered Scavenger Chest (at spawn for Story Mode Stage 1)
    if isStory && floorNumber = 1 then
      let batteredChestPos = { X = spawn.X + 1; Y = spawn.Y }
      setPt batteredChestPos (Floor surface)
      let caltropsItem : EquipmentItem =
        { Name = "Sharpened Caltrops"
          Slot = EquipmentSlot.MentalRelic
          Description = "A pouch of jagged iron spikes scavenged from the battered chest."
          StatPercentBonuses = [ StatId.Reflex, 11 ]
          HealthBonus = 0
          MoraleBonus = 0
          StartingRecklessnessDelta = 0
          Triggers = [] }
      // The prologue already has the player loot this chest (the class/weapon selection happens
      // before the crawl), so the in-map chest starts plundered and cannot re-offer a class pick.
      let batteredChest =
        { Id = "battered_chest"
          Description = "A battered iron chest resting in the mud, its lock broken and hanging loose."
          LootKeyId = None
          ItemReward = Some caltropsItem
          IsOpen = true }
      entities <- Map.add batteredChestPos (EntityChest batteredChest) entities

    // B. Floor NPC & Story Quests
    // Souls + Morale + key is the shape of six of the seven authored rewards; the odd two (a health
    // grant, a full restore) are written out where they are declared.
    let questReward souls morale keyId : QuestReward =
      { QuestReward.Empty with
          Souls = souls
          Morale = morale
          KeyId = keyId }

    let npcPos = findCleanTileIn npcPlaza { X = npcPlaza.Center.X + 2; Y = npcPlaza.Center.Y }
    setPt npcPos (Floor surface)

    let npc =
      if isStory then
        match floorNumber with
        | 1 ->
          { Id = "npc_shivering_boy"
            Name = "The Shivering Boy"
            Role = "Lost Child"
            Dialogue =
              [ "M-mister... please! Bandits dragged my twin sister Lyra into the black pines! They said she was their bounty!"
                "Their leader camps by the cave-mouth in the woods, near the old quarry shaft. He wears iron and hits like a falling tree!"
                "Please... cut through them and bring my sister home." ]
            Quest =
              Some {
                Id = "quest_save_lyra"
                Title = "The Boy's Plea: Rescue Lyra"
                Description = "Cut through the bandit warband and defeat their leader to save the boy's twin sister."
                IsCompleted = false
                Reward = questReward 50 40 (Some "lyra_keepsake")
                RewardDescription = "Awards +40 Morale, 50 Souls, and a keepsake from Lyra."
                RequiredEnemyName = Some "Bandit Leader"
              }
            HasGivenReward = false }
        | 2 ->
          { Id = "npc_quarry_foreman"
            Name = "The Quarry Foreman"
            Role = "Iron Taskmaster"
            Dialogue =
              [ "So you are the one who pulled the girl from the bandits? Lyra, was it? The forest keeps what it takes... but you got her back."
                "These quarry colonnades crawl with feral slag hounds and stone gargoyles. Clear them and the deep roads will open." ]
            Quest =
              Some {
                Id = "quest_quarry_hounds"
                Title = "Trial of Guilt: Cull the Feral Slag Hounds"
                Description = "Defeat the feral beasts prowling the quarry colonnades."
                IsCompleted = false
                Reward = questReward 50 40 (Some "quarry_talisman")
                RewardDescription = "Awards +40 Morale, 50 Souls, and the Talisman of Poise."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | 3 ->
          { Id = "npc_hermit_mist"
            Name = "Hermit of the Mist"
            Role = "Keeper of Memories"
            Dialogue =
              [ "Dense fog swallows every footstep into unnerving silence... Whispers chorus: 'She's at home waiting for you.'"
                "Beware the blight weavers and mist stalkers weaving webs of denial." ]
            Quest =
              Some {
                Id = "quest_denial_weavers"
                Title = "Trial of Denial: Dispel the Mist Weavers"
                Description = "Defeat the arachnid blight weavers infesting the sacred cloister."
                IsCompleted = false
                Reward = questReward 60 40 (Some "cloak_clarity")
                RewardDescription = "Awards +40 Morale, 60 Souls, and the Cloak of Clarity."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | 4 ->
          { Id = "npc_slag_alchemist"
            Name = "The Slag Alchemist"
            Role = "Volcanic Hermit"
            Dialogue =
              [ "The caldera shakes with self-consuming fury! 'Whose fault was this? Why didn't anyone stop it?!'"
                "The molten beasts feed on burning rage." ]
            Quest =
              Some {
                Id = "quest_anger_magma"
                Title = "Trial of Anger: Subdue the Magma Wurms"
                Description = "Quell the burning beasts feeding the caldera's rage."
                IsCompleted = false
                Reward =
                  { QuestReward.Empty with
                      Souls = 75
                      Health = 50
                      KeyId = Some "cinder_sigil" }
                RewardDescription = "Awards +50 Health, 75 Souls, and the Cinder-Forged Sigil."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | 5 ->
          { Id = "npc_storm_navigator"
            Name = "The Storm Navigator"
            Role = "Promontory Lookout"
            Dialogue =
              [ "Gale-force rain lashes the cliff edge. 'If only I had left earlier... if only I turned the wheel...'"
                "The Arbiter demands unequal trades and desperate pacts." ]
            Quest =
              Some {
                Id = "quest_bargain_harpies"
                Title = "Trial of Bargaining: Silence the Gale Harpies"
                Description = "Overcome the howling wind harpies shrieking false promises above the abyss."
                IsCompleted = false
                Reward = questReward 90 40 (Some "gale_pendant")
                RewardDescription = "Awards +40 Morale, 90 Souls, and the Gale-Wind Pendant."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | 6 ->
          { Id = "npc_priestess_waters"
            Name = "Priestess of Still Waters"
            Role = "Oracle of the Drowned"
            Dialogue =
              [ "Crushing silence drowns the flooded ruins. Total numbness and cognitive doldrums sap all will to fight."
                "The Drowned Sovereign feeds on the silence of the empty bedroom." ]
            Quest =
              Some {
                Id = "quest_depression_leeches"
                Title = "Trial of Depression: Dredge the Abyssal Leeches"
                Description = "Clear the abyssal leeches draining the light from the flooded causeway."
                IsCompleted = false
                Reward = questReward 100 50 (Some "anchor_resolve")
                RewardDescription = "Awards +50 Morale, 100 Souls, and the Anchor of Resolve."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | 7 ->
          { Id = "npc_keeper_blossoms"
            Name = "Keeper of White Blossoms"
            Role = "Guardian of Acceptance"
            Dialogue =
              [ "White lilies bloom in serene, fragrant peace. 'The journey ends not with forgetting, but with peace.'"
                "Step into the meadow and face the final manifestation." ]
            Quest =
              Some {
                Id = "quest_acceptance_harmony"
                Title = "Trial of Acceptance: Harmony with the Meadow Seraphs"
                Description = "Commune with the serene guardians of the white meadow."
                IsCompleted = false
                Reward =
                  { QuestReward.Empty with
                      FullVitality = true
                      KeyId = Some "keepsake_pendant" }
                RewardDescription = "Awards full vitality restoration and the Keepsake Pendant."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | _ ->
          { Id = sprintf "npc_floor_%d" floorNumber
            Name = "The Cosmic Astrologer"
            Role = "Tower Inhabitant"
            Dialogue = [ "You stand upon Celestial Spires."; "Beyond the colonnade lies the infinite ascent." ]
            Quest = None
            HasGivenReward = false }
      else
        let npcQuestOpt =
          match requiredQuest with
          | Some qId ->
            Some {
              Id = qId
              Title = sprintf "Trial of Floor %d" floorNumber
              Description = "The ascension gates remain barred by ancient decree. Prove your resolve."
              IsCompleted = false
              Reward = QuestReward.Empty
              RewardDescription = "Unlocks the stairway portal to the next floor."
              RequiredEnemyName = None
            }
          | None -> None

        { Id = sprintf "npc_floor_%d" floorNumber
          Name =
            match theme with
            | QuarryPlazas -> "The Iron Foreman"
            | PineCloisters -> "Hermit of the Mist"
            | BasaltCalderas -> "The Slag Alchemist"
            | TempestTerraces -> "The Storm Navigator"
            | SunkenBoulevards -> "Priestess of Still Waters"
            | ElysianSanctuaries -> "Keeper of White Blossoms"
            | CelestialSpires -> "The Cosmic Astrologer"
          Role = "Tower Inhabitant"
          Dialogue =
            [ sprintf "Greetings, traveler. You stand upon %s." theme.Name
              theme.Description
              "Beyond the grand colonnade lies the stairway to higher tiers." ]
          Quest = npcQuestOpt
          HasGivenReward = false }

    entities <- Map.add npcPos (EntityNpc npc) entities

    // B2. Story Stage 1 places the captive sister beside the Bandit Leader's den. Her
    // quest only resolves once the leader falls, so the payoff reads as a reunion.
    if isStory && floorNumber = 1 then
      let lyraPos = findCleanTileIn guardianPlaza { X = guardianPlaza.Center.X + 2; Y = guardianPlaza.Center.Y }
      setPt lyraPos (Floor surface)
      let lyra =
        { Id = "npc_lyra"
          Name = "Lyra"
          Role = "The Boy's Twin Sister"
          Dialogue =
            [ "You... you came for me? The bandits are broken — you truly did it!"
              "I thought I would never see my brother again. Tell him I am coming home." ]
          Quest =
            Some {
              Id = "quest_save_lyra"
              Title = "The Boy's Plea: Rescue Lyra"
              Description = "Cut through the bandit warband and defeat their leader to save the boy's twin sister."
              IsCompleted = false
              Reward = questReward 50 40 (Some "lyra_keepsake")
              RewardDescription = "Awards +40 Morale, 50 Souls, and a keepsake from Lyra."
              RequiredEnemyName = Some "Bandit Leader"
            }
          HasGivenReward = false }
      entities <- Map.add lyraPos (EntityNpc lyra) entities

    // C. Two shrines per stage at mid-floor hubs, one per class-appropriate stat pair
    let shrinePositions = ResizeArray<Point>()
    let shrineRoomIndices = Set [ shrineIdx1; shrineIdx2 ]
    let shrineSites =
      [ shrineIdx1, PrimaryPair, sprintf "shrine_floor_%d" floorNumber
        shrineIdx2, SecondaryPair, sprintf "shrine_floor_%d_prime" floorNumber ]

    for shrineIndex, boon, shrineId in shrineSites do
      let shrinePlaza = plazaAt shrineIndex
      let shrinePos = findCleanTileIn shrinePlaza { X = shrinePlaza.Center.X - 2; Y = shrinePlaza.Center.Y }
      setPt shrinePos (Floor surface)

      let shrine =
        { Id = shrineId
          Name = sprintf "Monolith of %s" theme.Name
          BlessingDescription = "Fully restores Health and Morale, repairs damaged Armor, restores Weapon to Pristine, and calms accumulated Recklessness."
          Boon = boon
          IsBlessed = false
          IsUsed = false }

      entities <- Map.add shrinePos (EntityShrine shrine) entities
      shrinePositions.Add shrinePos

    // D. Treasure Chests (main chamber chest + tactical alcove rewards)
    let chestPos = findCleanTileIn chestPlaza { X = chestPlaza.Center.X; Y = chestPlaza.Center.Y + 2 }
    setPt chestPos (Floor surface)

    let relicItem : EquipmentItem =
      { Name = sprintf "Relic of %s" theme.Name
        Slot = EquipmentSlot.MentalRelic
        Description = sprintf "An ancient heirloom resonant with %s." theme.Name
        StatPercentBonuses = [ StatId.Resolve, 33; StatId.Intuition, 22 ]
        HealthBonus = 0
        MoraleBonus = 30
        StartingRecklessnessDelta = 0
        Triggers = [] }

    let chest =
      { Id = sprintf "chest_floor_%d" floorNumber
        Description = "An ornate iron-bound chest resting beneath an archway."
        LootKeyId = None
        ItemReward = Some relicItem
        IsOpen = false }

    entities <- Map.add chestPos (EntityChest chest) entities

    // Populate carved alcoves so spurs always hold a tactical reward (bonus cache or calming oasis)
    for aIdx in 0 .. alcoves.Count - 1 do
      let _, _, alcoveCenter = alcoves.[aIdx]
      if aIdx = 0 && not (Map.containsKey alcoveCenter entities) then
        setPt alcoveCenter (Floor surface)
        let alcoveChest =
          { Id = sprintf "alcove_chest_floor_%d" floorNumber
            Description = "A tucked-away iron coffer hidden inside a side alcove."
            LootKeyId = None
            ItemReward = Some { relicItem with Name = sprintf "Alcove Cache of %s" theme.Name }
            IsOpen = false }
        entities <- Map.add alcoveCenter (EntityChest alcoveChest) entities
      else
        // CalmingSpores oasis pocket inside secondary alcoves
        for dy in 0 .. 1 do
          for dx in 0 .. 1 do
            let p = { X = alcoveCenter.X + dx; Y = alcoveCenter.Y + dy }
            if getPt p = Floor surface && not (Map.containsKey p entities) then
              setPt p (Hazard CalmingSpores)

    // E. Floor Enemies & Guardians (or Story Bosses)
    let guardianPos = findCleanTileIn guardianPlaza guardianPlaza.Center
    setPt guardianPos (Floor surface)

    let biomeMonsters = Bestiary.byBiome theme

    let guardianEnemy =
      if isStory && floorNumber <= 7 then
        let bossCombatant =
          if floorNumber = 1 then
            let chieftain = TierFactory.createClassLevel CharacterClass.Warrior 32
            { chieftain with Name = "Bandit Leader" }
          else
            match floorNumber with
            | 2 -> StoryBosses.createGuiltAspect ()
            | 3 -> StoryBosses.createDenialAspect ()
            | 4 -> StoryBosses.createAngerAspect ()
            | 5 -> StoryBosses.createBargainingAspect ()
            | 6 -> StoryBosses.createDepressionAspect ()
            | _ -> StoryBosses.createAcceptanceAspect ()

        let bossCombatant = StoryBosses.scaleToStage floorNumber bossCombatant
        let bossName =
          if floorNumber = 1 then "Bandit Leader"
          else
            match floorNumber with
            | 2 -> "Aspect of Guilt"
            | 3 -> "Aspect of Denial"
            | 4 -> "Aspect of Anger"
            | 5 -> "Aspect of Bargaining"
            | 6 -> "Aspect of Depression"
            | _ -> "Aspect of Acceptance"
        { Id = sprintf "boss_stage_%d" floorNumber
          Name = bossName
          Combatant = bossCombatant
          DropsKeyId = requiredKey
          IsDefeated = false
          ChaseCharge = 0.0 }
      else
        let monsterTemplate =
          match biomeMonsters with
          | [] -> Bestiary.slagHound
          | list ->
            let idx = Math.Min(list.Length - 1, (floorNumber - 1) % list.Length)
            let rotated = list.[idx]
            let ceiling = ProgressionScale.stageTemplateCeiling floorNumber
            if rotated.Level <= ceiling then rotated
            else
              match list |> List.filter (fun m -> m.Level <= ceiling) with
              | [] -> list |> List.minBy (fun m -> m.Level)
              | eligible -> eligible |> List.maxBy (fun m -> m.Level)
        let guardianCombatant = Bestiary.createMonster monsterTemplate
        { Id = sprintf "enemy_guardian_%d" floorNumber
          Name = monsterTemplate.Name
          Combatant = guardianCombatant
          DropsKeyId = requiredKey
          IsDefeated = false
          ChaseCharge = 0.0 }

    entities <- Map.add guardianPos (EntityEnemy guardianEnemy) entities

    // F. Tactical Enemy Formations (Gate Sentries, Pillar Stalkers, Vanguard Packs)
    let isFreeMobTile (pos: Point) =
      pos <> spawn
      && pos <> stairway
      && ((isStory && floorNumber <= 1) || chebyshev pos spawn > 4)
      && (match getPt pos with Floor _ -> true | _ -> false)
      && not (Map.containsKey pos entities)

    let hasAdjacentTile (pred: TowerTile -> bool) (p: Point) =
      pred (getTile (p.X + 1) p.Y)
      || pred (getTile (p.X - 1) p.Y)
      || pred (getTile p.X (p.Y + 1))
      || pred (getTile p.X (p.Y - 1))

    let formationTilesFor (roomIdx: int) : Point list =
      let room = plazaArr.[roomIdx]
      let arch = roomArchetypes.[roomIdx]
      let thresholds = roomThresholds.[roomIdx]
      let interior = ResizeArray<Point>()
      for py in room.Y + 1 .. room.Y + room.Height - 2 do
        for px in room.X + 1 .. room.X + room.Width - 2 do
          let p = { X = px; Y = py }
          if isFreeMobTile p then interior.Add p

      let gateSentries =
        interior
        |> Seq.filter (fun p ->
          thresholds |> Seq.exists (fun th -> let d = chebyshev p th in d >= 1 && d <= 2))
        |> Seq.toList

      let pillarStalkers =
        interior
        |> Seq.filter (fun p -> hasAdjacentTile (function Pillar -> true | _ -> false) p)
        |> Seq.toList

      let vanguardPacks =
        interior
        |> Seq.filter (fun p ->
          hasAdjacentTile (function Hazard _ | Chasm -> true | _ -> false) p
          || chebyshev p room.Center <= 3)
        |> Seq.toList

      let prioritized =
        match arch with
        | GatehouseBastion -> gateSentries @ pillarStalkers @ vanguardPacks
        | ColonnadeHall | RuinedScatter -> pillarStalkers @ gateSentries @ vanguardPacks
        | HazardCrucible -> vanguardPacks @ pillarStalkers @ gateSentries
        | RotundaSanctuary | OpenPlaza -> vanguardPacks @ gateSentries @ pillarStalkers

      // Deduplicate while preserving priority order, then append any remaining interior tiles
      let seen = System.Collections.Generic.HashSet<Point>()
      let ordered = ResizeArray<Point>()
      for p in prioritized do
        if seen.Add p then ordered.Add p
      for p in interior do
        if seen.Add p then ordered.Add p
      List.ofSeq ordered

    let mobSpecies (i: int) =
      if isStory && floorNumber = 1 then
        let tutorialLevel = 3
        if i % 2 = 0 then
          let cls = if (i / 2) % 2 = 0 then CharacterClass.Warrior else CharacterClass.Rogue
          let name = if cls = CharacterClass.Warrior then "Bandit" else "Bandit Scout"
          let combatant = TierFactory.createClassLevel cls tutorialLevel
          name, { combatant with Name = name }
        else
          let cls = if (i / 2) % 2 = 0 then CharacterClass.Rogue else CharacterClass.Warrior
          let name = if cls = CharacterClass.Rogue then "Forest Wolf" else "Bramble Boar"
          let combatant = TierFactory.createClassLevel cls tutorialLevel
          let combatant =
            if cls = CharacterClass.Rogue then
              combatant
            else
              { combatant with
                  MonsterFamily = Some MonsterFamily.Beast
                  MonsterTraits = [ RelentlessFerocity 20 ] }
          name, { combatant with Name = name }
      else
        let mobTemplate =
          if biomeMonsters.IsEmpty then Bestiary.slagHound
          else
            let available =
              if isStory && floorNumber <= 3 then
                [ biomeMonsters.[0] ]
              elif floorNumber <= 1 then
                if i < 2 then [ biomeMonsters.[0] ]
                else [ biomeMonsters.[Math.Min(1, biomeMonsters.Length - 1)] ]
              elif floorNumber <= 3 then
                biomeMonsters |> List.truncate 2
              else
                let ceiling = ProgressionScale.stageTemplateCeiling floorNumber
                if not isStory then
                  biomeMonsters
                else
                  match biomeMonsters |> List.filter (fun m -> m.Level <= ceiling) with
                  | [] -> [ biomeMonsters |> List.minBy (fun m -> m.Level) ]
                  | legal -> legal
            let mIdx = (floorNumber + i) % available.Length
            available.[mIdx]
        mobTemplate.Name, Bestiary.createMonster mobTemplate

    let mutable mobIndex = 0

    for roomIndex in 0 .. plazaCount - 1 do
      let room = plazaArr.[roomIndex]
      let targetCount =
        if roomIndex = spawnIdx && (not isStory || floorNumber > 1) then 0
        elif isStory && floorNumber <= 1 then 2
        elif isStory && floorNumber <= 2 then 2 + rng.Next(0, 2)
        elif isStory && floorNumber <= 4 then 3 + rng.Next(0, 2)
        elif floorNumber <= 1 then 2
        else Math.Min(18, 4 + floorNumber / 2 + rng.Next(0, 2))

      let mutable placedCount = 0
      let candidates = formationTilesFor roomIndex
      for mobPos in candidates do
        if placedCount < targetCount && isFreeMobTile mobPos then
          let mobName, mobCombatant = mobSpecies mobIndex
          let mobEnemy =
            { Id = sprintf "grind_mob_%d_%d" floorNumber mobIndex
              Name = mobName
              Combatant = mobCombatant
              DropsKeyId = None
              IsDefeated = false
              ChaseCharge = 0.0 }
          setPt mobPos (Floor surface)
          entities <- Map.add mobPos (EntityEnemy mobEnemy) entities
          placedCount <- placedCount + 1
          mobIndex <- mobIndex + 1

    // Finalize immutable Tiles map from flat grid before encounter placement so
    // `WorldEvents.generateFloorEncountersInLayout` sees all pillars, hazards, and alcoves.
    let mutable tiles =
      [| for y in 0 .. mapHeight - 1 do
           for x in 0 .. mapWidth - 1 ->
             { X = x; Y = y }, grid.[y * mapWidth + x] |]
      |> Map.ofArray

    // G. Dynamic World & Tower Floor Encounters (ADR 0004 Phase 3 + graph/alcove-aware distribution)
    let plazaCenters = plazas |> List.map (fun p -> p.Center)
    let alcoveCenters = alcoves |> Seq.map (fun (_, _, c) -> c) |> Seq.toList
    let floorEncounters =
      WorldEvents.generateFloorEncountersInLayout
        theme floorNumber spawn stairway plazaCenters alcoveCenters tiles entities playerPlane

    let isSafeAltarTile (p: Point) =
      not (spawnPlaza.Contains p)
      && not (shrinePositions |> Seq.exists (fun s -> euclidean p s < 6.5))

    let nonShrineRooms =
      [ 0 .. plazaCount - 1 ]
      |> List.filter (fun i -> i <> spawnIdx && not (shrineRoomIndices.Contains i))

    let mutable altarFallbackIdx = 0

    for (rawPos, enc) in floorEncounters do
      let isAltar = match enc with FloorEncounter.SacrificialAltar _ -> true | _ -> false
      let rawClean =
        rawPos <> spawn
        && rawPos <> stairway
        && not (Map.containsKey rawPos entities)
        && (match getPt rawPos with Floor _ -> true | _ -> false)
        && (not isAltar || isSafeAltarTile rawPos)

      let finalPosOpt =
        if rawClean then Some rawPos
        elif isAltar then
          let rec tryRooms offset =
            if offset >= nonShrineRooms.Length then None
            else
              let rIdx = nonShrineRooms.[(altarFallbackIdx + offset) % nonShrineRooms.Length]
              let candidate = findCleanTileIn plazaArr.[rIdx] plazaArr.[rIdx].Center
              if isSafeAltarTile candidate && not (Map.containsKey candidate entities) then
                altarFallbackIdx <- altarFallbackIdx + 1
                Some candidate
              else
                tryRooms (offset + 1)
          tryRooms 0
        else
          let rIdx =
            match containingRoom rawPos with
            | -1 -> Math.Min(1, plazaCount - 1)
            | idx -> idx
          let snapped = findCleanTileIn plazaArr.[rIdx] rawPos
          if snapped <> spawn && snapped <> stairway && not (Map.containsKey snapped entities) then
            Some snapped
          else
            None

      match finalPosOpt with
      | Some encPos ->
        if getPt encPos <> Floor surface then
          tiles <- Map.add encPos (Floor surface) tiles
        entities <- Map.add encPos (EntityEncounter enc) entities
      | None -> ()

    { FloorNumber = floorNumber
      Theme = theme
      Width = mapWidth
      Height = mapHeight
      Tiles = tiles
      Entities = entities
      StairwayLocation = stairway
      SpawnLocation = spawn
      Explored = Set.empty.Add spawn
      Visible = Set.empty.Add spawn
      SeenEnemies = Set.empty
      ActiveQuests = npc.Quest |> Option.toList
      InitialPlayerLevel = 1 }

  /// Generates a complete Tower floor layout with open architectural plazas.
  /// Assumes a Physical player for the vault's stat check; live runs use `generateFloorForPlane`.
  let generateFloorWithMode (seed: int) (floorNumber: int) (isStory: bool) : TowerFloor =
    generateFloorForPlane seed floorNumber isStory Plane.Physical

  /// Generates a complete Tower floor layout with open architectural plazas
  let generateFloor (seed: int) (floorNumber: int) : TowerFloor =
    generateFloorWithMode seed floorNumber false
