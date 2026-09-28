module Fornach.Domain.Tests.VisionResolverTests

open Xunit
open Fornach.Domain
open Fornach.Spatial

let private createFighter (stats: (StatId * int) list) =
  let statBlock = StatBlock.Create stats
  Combatant.create (CombatantId.New()) "TestFighter" 100 100 statBlock

[<Fact>]
let ``Stationary master with high Poise dilates vision to 360 faster than novice``
  ()
  =
  let master = createFighter [ Poise, 150; Prowess, 100 ]
  let novice = createFighter [ Poise, 20; Prowess, 20 ]
  let pos = { X = 0; Y = 0 }
  let facing = Some Direction.East
  let env = EnvironmentalFactors.Default

  // Turn 1 resting:
  let masterCone1 =
    VisionResolver.resolve master pos facing (LocomotionState.Stationary 1) env

  let noviceCone1 =
    VisionResolver.resolve novice pos facing (LocomotionState.Stationary 1) env

  Assert.True(masterCone1.ArcDegrees > noviceCone1.ArcDegrees)

  // Turn 2 resting:
  let masterCone2 =
    VisionResolver.resolve master pos facing (LocomotionState.Stationary 2) env

  let noviceCone2 =
    VisionResolver.resolve novice pos facing (LocomotionState.Stationary 2) env

  // Master reaches 360 full awareness in 2 turns and facing becomes None
  Assert.Equal(360.0, masterCone2.ArcDegrees)
  Assert.Equal(None, masterCone2.Facing)

  // Novice is still directional
  Assert.True(noviceCone2.ArcDegrees < 360.0)
  Assert.Equal(facing, noviceCone2.Facing)

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
let ``High Overwhelm and Recklessness force severe tunnel vision`` () =
  let calm = createFighter [ Poise, 50; Prowess, 50 ]
  let pos = { X = 0; Y = 0 }
  let facing = Some Direction.East
  let env = EnvironmentalFactors.Default

  let panicked =
    { calm with
        Meters =
          { calm.Meters with
              Overwhelm = Meter.Create 80
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
