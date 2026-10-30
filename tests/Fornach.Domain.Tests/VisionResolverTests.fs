module Fornach.Domain.Tests.VisionResolverTests

open Xunit
open Fornach.Domain
open Fornach.Spatial

let private createFighter (stats: (StatId * int) list) =
  let statBlock = StatBlock.Create stats
  Combatant.create (CombatantId.New()) "TestFighter" 100 100 statBlock

[<Fact>]
let ``Every combatant widens ninety degrees per turn and reaches a full circle after three waits``
  ()
  =
  let master = createFighter [ Poise, 150; Prowess, 100 ]
  let novice = createFighter [ Poise, 20; Prowess, 20 ]
  let pos = { X = 0; Y = 0 }
  let facing = Some Direction.East
  let env = EnvironmentalFactors.Default

  let arcOf combatant locomotion =
    (VisionResolver.resolve combatant pos facing locomotion env).ArcDegrees

  // Narrowed to the walking base before any waiting.
  let masterWalking = arcOf master LocomotionState.Walking
  let noviceWalking = arcOf novice LocomotionState.Walking

  // The rate is flat: Poise no longer speeds the dilation up, so both widen by exactly 90.
  Assert.Equal(90.0, arcOf master (LocomotionState.Stationary 1) - masterWalking)
  Assert.Equal(90.0, arcOf novice (LocomotionState.Stationary 1) - noviceWalking)
  Assert.Equal(180.0, arcOf master (LocomotionState.Stationary 2) - masterWalking)

  // Two waits still leave a gap, however disciplined the watcher.
  Assert.True(arcOf master (LocomotionState.Stationary 2) < 360.0)
  Assert.True(arcOf novice (LocomotionState.Stationary 2) < 360.0)

  // Three waits is a full circle for everyone, and facing stops mattering.
  let masterFull = VisionResolver.resolve master pos facing (LocomotionState.Stationary 3) env
  Assert.Equal(360.0, masterFull.ArcDegrees)
  Assert.Equal(None, masterFull.Facing)
  Assert.Equal(360.0, arcOf novice (LocomotionState.Stationary 3))

[<Fact>]
let ``Agile combatant retains wider peripheral vision during sprinting`` () =
  let agile = createFighter [ Reflex, 120; Finesse, 120 ]
  let clumsy = createFighter [ Reflex, 15; Finesse, 15 ]
  let pos = { X = 0; Y = 0 }
  let facing = Some Direction.North
  let env = EnvironmentalFactors.Default

  let agileCone =
    VisionResolver.resolve agile pos facing LocomotionState.Sprinting env

  let clumsyCone =
    VisionResolver.resolve clumsy pos facing LocomotionState.Sprinting env

  Assert.True(agileCone.ArcDegrees > clumsyCone.ArcDegrees)
  Assert.True(agileCone.ArcDegrees >= 90.0) // Softened constriction
  Assert.True(clumsyCone.ArcDegrees <= 70.0) // Severe tunnel vision

[<Fact>]
let ``High Frustration and Recklessness force severe tunnel vision`` () =
  let calm = createFighter [ Poise, 50; Prowess, 50 ]
  let pos = { X = 0; Y = 0 }
  let facing = Some Direction.East
  let env = EnvironmentalFactors.Default

  let panicked =
    { calm with
        Meters =
          { calm.Meters with
              Frustration = Meter.Create 80
              Recklessness = Meter.Create 70 } }

  let calmCone =
    VisionResolver.resolve calm pos facing LocomotionState.Walking env

  let panickedCone =
    VisionResolver.resolve panicked pos facing LocomotionState.Walking env

  Assert.True(panickedCone.ArcDegrees < calmCone.ArcDegrees)
  Assert.True(panickedCone.ArcDegrees <= 90.0)

[<Fact>]
let ``Exhaustion and adverse weather reduce maximum sight radius`` () =
  let fresh = createFighter []
  let pos = { X = 0; Y = 0 }
  let envClear = EnvironmentalFactors.Default

  let envFoggy =
    { AmbientLight = 0.5
      WeatherVisibility = 0.4 } // Thick mist at dusk

  let exhausted =
    { fresh with
        Meters = { fresh.Meters with Exhaustion = Meter.Create 90 } }

  let freshCone =
    VisionResolver.resolve fresh pos None LocomotionState.Walking envClear

  let foggyCone =
    VisionResolver.resolve fresh pos None LocomotionState.Walking envFoggy

  let exhaustedFoggyCone =
    VisionResolver.resolve exhausted pos None LocomotionState.Walking envFoggy

  Assert.Equal(12, freshCone.MaxRadius)
  Assert.True(foggyCone.MaxRadius < freshCone.MaxRadius)
  Assert.True(exhaustedFoggyCone.MaxRadius <= foggyCone.MaxRadius)
