module Fornach.Tests.StalemateTests

open Xunit
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine

let private stats force guard =
  StatBlock.Create
    [ StatId.Force, force
      StatId.Fortitude, guard
      StatId.Finesse, 40
      StatId.Reflex, 40
      StatId.Prowess, 40
      StatId.Poise, guard
      StatId.Intellect, 40
      StatId.Resolve, guard
      StatId.Acuity, 40
      StatId.Intuition, 40
      StatId.Acumen, 40
      StatId.Composure, guard ]

let private fighter name health = Combatant.create (CombatantId.New()) name health health (stats 100 45)

/// A combatant worn down exactly as the stalemate describes: broken weapon, armor shredded to zero,
/// and a carapace that soaks every landed blow down to the 1-damage floor.
let private wornFighter force guard name health =
  let c = Combatant.create (CombatantId.New()) name health health (stats force guard)

  { c with
      WeaponCondition = WeaponCondition.Broken
      Armor = (ArmorIntegrity.Create 50).Shred 50
      MonsterTraits = [ EtherealCarapace 0.99 ] }

/// Rolls no dice hits (Power scores on 5+) and no percentile procs, so a contest is decided by the
/// deterministic floor hits alone and only the designed mechanics move the numbers.
let private quietRoller: DiceRoller = fun _ _ -> 100

let private openFloor (entities: Map<Point, TowerEntity>) : TowerFloor =
  { FloorNumber = 1
    Theme = FloorTheme.QuarryPlazas
    Width = 10
    Height = 8
    Tiles = [ for x in 0 .. 9 do for y in 0 .. 7 -> { X = x; Y = y }, Floor SurfaceType.PavedStone ] |> Map.ofList
    Entities = entities
    StairwayLocation = { X = 0; Y = 0 }
    SpawnLocation = { X = 5; Y = 4 }
    Explored = Set.empty
    Visible = Set.empty
    ActiveQuests = [] }

let private stateWith (player: Combatant) (entities: Map<Point, TowerEntity>) : TowerRunState =
  { Player = player
    CurrentFloor = openFloor entities
    PlayerPosition = { X = 5; Y = 4 }
    Facing = Some Direction.North
    Locomotion = LocomotionState.Walking
    CollectedKeys = Set.empty
    CompletedQuests = Set.empty
    InventoryItems = []
    MessageLog = []
    Seed = 42
    Souls = 0
    Trophies = []
    Consumables = []
    UnidentifiedConsumables = Set.empty
    DiscoveredEchoes = Set.empty
    IsStoryMode = false
    SafeStepsSinceHone = 0 }

let private steps n state = [ 1 .. n ] |> List.fold (fun s _ -> TowerSession.honeOnSafeStep s) state

[<Fact>]
let ``a safe step hones a weapon every fourth step and never past notched`` () =
  let broken =
    let s = stateWith (fighter "Delver" 200) Map.empty
    { s with Player = { s.Player with WeaponCondition = WeaponCondition.Broken } }

  // Three steps in, nothing has been honed yet.
  let afterThree = steps 3 broken
  Assert.Equal(WeaponCondition.Broken, afterThree.Player.WeaponCondition)
  Assert.Equal(3, afterThree.SafeStepsSinceHone)

  // The fourth safe step is the one that hones, and it spends the counter.
  let afterFour = TowerSession.honeOnSafeStep afterThree
  Assert.Equal(WeaponCondition.Damaged, afterFour.Player.WeaponCondition)
  Assert.Equal(0, afterFour.SafeStepsSinceHone)

  // Four more take Damaged to the personal-repair cap; travel never reaches Pristine.
  let afterEight = steps 4 afterFour
  Assert.Equal(WeaponCondition.Notched, afterEight.Player.WeaponCondition)

  // The counter keeps counting at the cap — the fourth step is spent, not banked — and the cap holds.
  let afterTwelve = steps 4 afterEight
  Assert.Equal(WeaponCondition.Notched, afterTwelve.Player.WeaponCondition)
  Assert.Equal(0, afterTwelve.SafeStepsSinceHone)

[<Fact>]
let ``an engaged step is not safe and leaves the weapon untouched`` () =
  let hound: TowerEnemy =
    { Id = "hound_1"
      Name = "Pack Hound"
      Combatant = fighter "Pack Hound" 200
      DropsKeyId = None
      IsDefeated = false
      ChaseCharge = 0.0 }

  let engaged =
    let s = stateWith (fighter "Delver" 200) (Map.ofList [ { X = 6; Y = 4 }, EntityEnemy hound ])
    { s with Player = { s.Player with WeaponCondition = WeaponCondition.Broken } }

  Assert.False(TowerSession.isSafe engaged)

  // The move handler only hones inside its safe-step branch, so a contested step never ticks the counter.
  let stepped =
    if TowerSession.isSafe engaged then
      TowerSession.honeOnSafeStep engaged
    else
      engaged

  Assert.Equal(WeaponCondition.Broken, stepped.Player.WeaponCondition)
  Assert.Equal(0, stepped.SafeStepsSinceHone)

[<Fact>]
let ``a hit reduced to the one damage floor applies exactly one bleed stack once`` () =
  let attacker = wornFighter 60 40 "Attacker" 200
  let target = wornFighter 60 40 "Target" 200

  let first = ActionResolver.resolve quietRoller (StandardAttack (ForceStrike false)) attacker target

  let damageDealt =
    first.Events
    |> List.pick (function
      | CombatEvent.DamageApplied d when d.Plane = Physical -> Some d.Amount
      | _ -> None)

  Assert.Equal(1, damageDealt)
  Assert.Equal(1, first.Target.BleedStacks)

  Assert.True(
    first.Events
    |> List.exists (function
      | CombatEvent.BleedApplied (_, 1, 1) -> true
      | _ -> false),
    "the floored hit must log a single bleed stack"
  )

  // A second floored hit does not stack again while the wound is already bleeding.
  let second =
    ActionResolver.resolve quietRoller (StandardAttack (ForceStrike false)) attacker first.Target

  Assert.Equal(1, second.Target.BleedStacks)

  Assert.False(
    second.Events
    |> List.exists (function
      | CombatEvent.BleedApplied _ -> true
      | _ -> false),
    "no second bleed while the target is already bleeding"
  )

[<Fact>]
let ``shredded armor turns a standard cut into an arterial one`` () =
  // A guaranteed opening (VitalOpeningBonus 60, percentile always 1) against a target that cannot
  // dodge (dice roll 3), so every blow procs: the stacks on a single hit are what is being measured.
  let roller: DiceRoller =
    fun _ max -> if max = 100 then 1 else 3

  let attacker =
    let baseCombatant = fighter "Attacker" 2000

    { baseCombatant with
        ComboTracker = { baseCombatant.ComboTracker with VitalOpeningBonus = 60 } }

  // Armor is deliberately thicker than a single blow can sunder: the armor-sunder passive runs in the
  // same resolve pass as the vital opening, so thin armor would be shredded to zero before the slash is
  // sized and both targets would read as shredded. That in-pass sunder is the extra bleed source the
  // first version of this test measured; the disparity bleed (Arterial Rupture) is Finesse-only and the
  // 1-damage floor needs a blow soaked down to 1, neither of which happens here.
  let intact = { fighter "Plated" 2000 with Armor = ArmorIntegrity.Create 400 }
  let shredded = { intact with Armor = (ArmorIntegrity.Create 400).Shred 400 }

  let bleedPerHit (target: Combatant) =
    let res = ActionResolver.resolve roller (StandardAttack (ForceStrike false)) attacker target

    let procs =
      res.Events
      |> List.filter (function
        | CombatEvent.PassiveProcTriggered (_, _, VitalOpeningTriggered _) -> true
        | _ -> false)

    let bleedEvents =
      res.Events
      |> List.choose (function
        | CombatEvent.BleedApplied (_, stacks, _) -> Some stacks
        | _ -> None)

    // Exactly one vital opening, and it is the only thing opening the wound.
    Assert.Equal(1, procs.Length)
    Assert.Single(bleedEvents) |> ignore
    bleedEvents.Head

  // VitalOpeningBonus 60 sizes the slash at 2 + 60/50 = 3 on intact armor.
  Assert.Equal(3, bleedPerHit intact)
  // The same cut is held open by shredded armor: 3 x 2 = 6 stacks from a single proc.
  Assert.Equal(6, bleedPerHit shredded)

[<Fact>]
let ``an ordinary landed blow on shredded armor opens no bleed on its own`` () =
  // No opening to exploit (percentile 100 against VitalOpeningBonus 0), so nothing procs: bleeding
  // comes from Vital Openings and the 1-damage floor, never from simply landing a hit.
  let intact = fighter "Plated" 2000
  let shredded = { intact with Armor = (ArmorIntegrity.Create 50).Shred 50 }
  let attacker = fighter "Attacker" 2000

  for target in [ intact; shredded ] do
    let res = ActionResolver.resolve quietRoller (StandardAttack (ForceStrike false)) attacker target
    Assert.Equal(0, res.Target.BleedStacks)

[<Fact>]
let ``a broken weapon grind against shredded armor ends through bleed instead of grinding to the turn cap`` () =
  let mutable alpha = wornFighter 100 45 "Alpha" 120
  let mutable beta = wornFighter 100 45 "Beta" 120
  let mutable turns = 0

  while turns < 200 && alpha.Health.Current > 0 && beta.Health.Current > 0 do
    let res = ActionResolver.resolve quietRoller (StandardAttack (ForceStrike false)) alpha beta
    alpha <- res.Actor
    beta <- res.Target
    turns <- turns + 1

    let held = beta
    beta <- alpha
    alpha <- held

  // Without the floor breaker every landed blow is 1 damage, so 120 Health apiece needs ~240 turns;
  // the bleed tick the floor now opens ends the grind well inside a normal fight's length.
  Assert.True(turns < 60, sprintf "the stalemate should break under 60 turns, took %d" turns)
