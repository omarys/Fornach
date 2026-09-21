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
      || (desc.SocialAliases
          |> List.exists (fun a -> a.ToLowerInvariant() = clean)))

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

[<CustomEquality; NoComparison>]
type StatBlock =
  private
  | StatBlock of Map<StatId, int>

  /// Primary lookup: retrieves effective attribute value (defaults to 10 if missing)
  member this.Get(stat: StatId) =
    match this with
    | StatBlock m -> Map.tryFind stat m |> Option.defaultValue 10

  /// Returns all 12 attributes and their values as a sequence
  member this.All =
    match this with
    | StatBlock m -> Attributes.all |> Seq.map (fun s -> s, this.Get s)

  /// Exports the underlying immutable map for inspection or serialization
  member this.ToMap() =
    match this with
    | StatBlock m -> m

  /// Returns a new StatBlock with a single stat set to an absolute value (clamped to minimum 1)
  member this.With(stat: StatId, value: int) =
    match this with
    | StatBlock m -> StatBlock(Map.add stat (System.Math.Max(1, value)) m)

  /// Returns a new StatBlock with an offset delta applied to an existing stat (clamped to minimum 1)
  member this.Modify(stat: StatId, delta: int) = this.With(stat, this.Get stat + delta)

  /// Applies a batch of additive modifiers (e.g. from Equipment or Buffs)
  member this.ApplyModifiers(modifiers: (StatId * int) seq) =
    modifiers
    |> Seq.fold (fun (acc: StatBlock) (stat, delta) -> acc.Modify(stat, delta)) this

  /// Verifies if a specific stat was explicitly defined rather than defaulted
  member this.ContainsExplicit(stat: StatId) =
    match this with
    | StatBlock m -> Map.containsKey stat m

  /// Formats all 12 stats into a clean multiline debug or CLI string
  override this.ToString() =
    this.All
    |> Seq.map (fun (s, v) -> sprintf "%A: %d" s v)
    |> String.concat ", "
    |> sprintf "StatBlock [%s]"

  override this.Equals(other) =
    match other with
    | :? StatBlock as o -> this.ToMap() = o.ToMap()
    | _ -> false

  override this.GetHashCode() = this.ToMap().GetHashCode()

  /// Creates a complete StatBlock, pre-seeding all 12 stats with a default of 10
  static member Create(values: (StatId * int) seq) =
    let initial = Attributes.all |> List.map (fun s -> s, 10) |> Map.ofList

    let populated =
      values
      |> Seq.fold (fun m (stat, v) -> Map.add stat (System.Math.Max(1, v)) m) initial

    StatBlock populated

  /// Empty / Baseline StatBlock where every stat is exactly 10
  static member Baseline = StatBlock.Create Seq.empty
