namespace Fornach.Domain

type Orientation =
  | Offense
  | Defense

type StatId =
  // Physical Axis
  | Force // Power Offense
  | Fortitude // Power Defense
  | Finesse // Agility Offense
  | Reflex // Agility Defense
  | Prowess // Discipline Offense
  | Poise // Discipline Defense
  // Mental Axis
  | Intellect // Power Offense
  | Resolve // Power Defense
  | Acuity // Agility Offense
  | Intuition // Agility Defense
  | Acumen // Discipline Offense
  | Composure // Discipline Defense

type StatDescriptor =
  { Id: StatId
    Plane: Plane
    Vector: Vector
    Orientation: Orientation
    CanonicalName: string
    SocialName: string
    SocialAliases: string list }

module Attributes =
  let descriptorOf =
    function
    // Physical
    | Force ->
      { Id = Force
        Plane = Physical
        Vector = Power
        Orientation = Offense
        CanonicalName = "Force"
        SocialName = "Force"
        SocialAliases = [] }
    | Fortitude ->
      { Id = Fortitude
        Plane = Physical
        Vector = Power
        Orientation = Defense
        CanonicalName = "Fortitude"
        SocialName = "Fortitude"
        SocialAliases = [] }
    | Finesse ->
      { Id = Finesse
        Plane = Physical
        Vector = Agility
        Orientation = Offense
        CanonicalName = "Finesse"
        SocialName = "Finesse"
        SocialAliases = [] }
    | Reflex ->
      { Id = Reflex
        Plane = Physical
        Vector = Agility
        Orientation = Defense
        CanonicalName = "Reflex"
        SocialName = "Reflex"
        SocialAliases = [] }
    | Prowess ->
      { Id = Prowess
        Plane = Physical
        Vector = Discipline
        Orientation = Offense
        CanonicalName = "Prowess"
        SocialName = "Prowess"
        SocialAliases = [] }
    | Poise ->
      { Id = Poise
        Plane = Physical
        Vector = Discipline
        Orientation = Defense
        CanonicalName = "Poise"
        SocialName = "Poise"
        SocialAliases = [] }
    // Mental (mapped to Social context: Presence/Command, Will/Defiance, Guile/Charm, Insight/Scrutiny, Leverage/Wit, Poise/Composure)
    | Intellect ->
      { Id = Intellect
        Plane = Mental
        Vector = Power
        Orientation = Offense
        CanonicalName = "Intellect"
        SocialName = "Presence / Command"
        SocialAliases = [ "Presence"; "Command" ] }
    | Resolve ->
      { Id = Resolve
        Plane = Mental
        Vector = Power
        Orientation = Defense
        CanonicalName = "Resolve"
        SocialName = "Will / Defiance"
        SocialAliases = [ "Will"; "Defiance" ] }
    | Acuity ->
      { Id = Acuity
        Plane = Mental
        Vector = Agility
        Orientation = Offense
        CanonicalName = "Acuity"
        SocialName = "Guile / Charm"
        SocialAliases = [ "Guile"; "Charm" ] }
    | Intuition ->
      { Id = Intuition
        Plane = Mental
        Vector = Agility
        Orientation = Defense
        CanonicalName = "Intuition"
        SocialName = "Insight / Scrutiny"
        SocialAliases = [ "Insight"; "Scrutiny" ] }
    | Acumen ->
      { Id = Acumen
        Plane = Mental
        Vector = Discipline
        Orientation = Offense
        CanonicalName = "Acumen"
        SocialName = "Leverage / Wit"
        SocialAliases = [ "Leverage"; "Wit" ] }
    | Composure ->
      { Id = Composure
        Plane = Mental
        Vector = Discipline
        Orientation = Defense
        CanonicalName = "Composure"
        SocialName = "Poise / Composure"
        SocialAliases = [ "Poise"; "Composure" ] }

  let all =
    [ Force
      Fortitude
      Finesse
      Reflex
      Prowess
      Poise
      Intellect
      Resolve
      Acuity
      Intuition
      Acumen
      Composure ]

  let byPlane plane =
    all |> List.filter (fun s -> (descriptorOf s).Plane = plane)

  let byVector vector =
    all |> List.filter (fun s -> (descriptorOf s).Vector = vector)

  let socialNameOf stat = (descriptorOf stat).SocialName

  let displayNameFor (mode: CombatMode) stat =
    match mode with
    | CombatMode.Social -> (descriptorOf stat).SocialName
    | CombatMode.Physical
    | CombatMode.Arcane -> (descriptorOf stat).CanonicalName

  let tryParse (s: string) : StatId option =
    let clean = s.Trim().ToLowerInvariant()

    all
    |> List.tryFind (fun stat ->
      let desc = descriptorOf stat

      desc.CanonicalName.ToLowerInvariant() = clean
      || desc.SocialName.ToLowerInvariant() = clean
      || (desc.SocialAliases |> List.exists (fun a -> a.ToLowerInvariant() = clean)))

  let counterparts =
    function
    | Force -> Intellect
    | Fortitude -> Resolve
    | Finesse -> Acuity
    | Reflex -> Intuition
    | Prowess -> Acumen
    | Poise -> Composure
    | Intellect -> Force
    | Resolve -> Fortitude
    | Acuity -> Finesse
    | Intuition -> Reflex
    | Acumen -> Prowess
    | Composure -> Poise

  let inline statIndex stat =
    match stat with
    | Force -> 0
    | Fortitude -> 1
    | Finesse -> 2
    | Reflex -> 3
    | Prowess -> 4
    | Poise -> 5
    | Intellect -> 6
    | Resolve -> 7
    | Acuity -> 8
    | Intuition -> 9
    | Acumen -> 10
    | Composure -> 11

[<CustomEquality; NoComparison>]
type StatBlock =
  private
  | StatBlock of values: int[] * explicitMask: int

  /// Primary lookup: retrieves effective attribute value (defaults to 10 if missing)
  member this.Get(stat: StatId) =
    match this with
    | StatBlock (arr, _) -> arr[Attributes.statIndex stat]

  /// Returns all 12 attributes and their values as a sequence
  member this.All =
    match this with
    | StatBlock (arr, _) -> Attributes.all |> Seq.mapi (fun i s -> s, arr[i])

  /// Exports the underlying immutable map for inspection or serialization
  member this.ToMap() =
    match this with
    | StatBlock (arr, _) ->
      Attributes.all
      |> List.mapi (fun i s -> s, arr[i])
      |> Map.ofList

  /// Returns a new StatBlock with a single stat set to an absolute value (clamped to minimum 1)
  member this.With(stat: StatId, value: int) =
    match this with
    | StatBlock (arr, mask) ->
      let copy = Array.copy arr
      let idx = Attributes.statIndex stat
      copy[idx] <- System.Math.Max(1, value)
      StatBlock(copy, mask ||| (1 <<< idx))

  /// Returns a new StatBlock with an offset delta applied to an existing stat (clamped to minimum 1)
  member this.Modify(stat: StatId, delta: int) = this.With(stat, this.Get stat + delta)

  /// Applies a batch of additive modifiers (e.g. from Equipment or Buffs)
  member this.ApplyModifiers(modifiers: (StatId * int) seq) =
    match this with
    | StatBlock (arr, mask) ->
      let copy = Array.copy arr
      let mutable newMask = mask
      for (stat, delta) in modifiers do
        let idx = Attributes.statIndex stat
        copy[idx] <- System.Math.Max(1, copy[idx] + delta)
        newMask <- newMask ||| (1 <<< idx)
      StatBlock(copy, newMask)

  /// Verifies if a specific stat was explicitly defined rather than defaulted
  member this.ContainsExplicit(stat: StatId) =
    match this with
    | StatBlock (_, mask) -> (mask &&& (1 <<< Attributes.statIndex stat)) <> 0

  /// Formats all 12 stats into a clean multiline debug or CLI string
  override this.ToString() =
    this.All
    |> Seq.map (fun (s, v) -> sprintf "%A: %d" s v)
    |> String.concat ", "
    |> sprintf "StatBlock [%s]"

  override this.Equals(other) =
    match other with
    | :? StatBlock as o ->
      match this, o with
      | StatBlock (a, _), StatBlock (b, _) ->
        let mutable eq = true
        let mutable i = 0
        while eq && i < 12 do
          if a[i] <> b[i] then eq <- false
          i <- i + 1
        eq
    | _ -> false

  override this.GetHashCode() =
    match this with
    | StatBlock (arr, _) ->
      let mutable hash = 17
      for i in 0 .. 11 do
        hash <- hash * 31 + arr[i]
      hash

  /// Creates a complete StatBlock, pre-seeding all 12 stats with a default of 10
  static member Create(values: (StatId * int) seq) =
    let arr = Array.create 12 10
    let mutable mask = 0
    for (stat, v) in values do
      let idx = Attributes.statIndex stat
      arr[idx] <- System.Math.Max(1, v)
      mask <- mask ||| (1 <<< idx)

    StatBlock(arr, mask)

  /// Empty / Baseline StatBlock where every stat is exactly 10 (cached static instance)
  static member val Baseline = StatBlock.Create Seq.empty
