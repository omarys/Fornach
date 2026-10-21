module Fornach.Tests.RepairTests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// A 100-point pool sitting at `current` of `maximum`, for exercising the rest caps directly.
let private pool current maximum : Pool =
  { Current = current
    Maximum = maximum }

let private armor current maximum : ArmorIntegrity =
  { Current = current
    Max = maximum }

[<Fact>]
let ``resting heals toward eighty percent of maximum and stops there`` () =
  let healed = TowerSession.restRecoverPool (pool 50 100) 33
  Assert.Equal(80, healed.Current)

  let alreadyThere = TowerSession.restRecoverPool (pool 80 100) 33
  Assert.Equal(80, alreadyThere.Current)

  // A shrine or any other full restore is not undone by resting afterwards.
  let aboveCap = TowerSession.restRecoverPool (pool 100 100) 33
  Assert.Equal(100, aboveCap.Current)

[<Fact>]
let ``resting repairs armor toward eighty percent and never past it`` () =
  let repaired = TowerSession.restRepairArmor (armor 40 100) 25
  Assert.Equal(65, repaired.Current)

  let capped = TowerSession.restRepairArmor (armor 70 100) 25
  Assert.Equal(80, capped.Current)

  let untouched = TowerSession.restRepairArmor (armor 90 100) 25
  Assert.Equal(90, untouched.Current)

[<Fact>]
let ``resting hones a weapon but never back to pristine`` () =
  Assert.Equal(WeaponCondition.Damaged, TowerSession.restHoneWeapon WeaponCondition.Broken)
  Assert.Equal(WeaponCondition.Notched, TowerSession.restHoneWeapon WeaponCondition.Damaged)
  Assert.Equal(WeaponCondition.Notched, TowerSession.restHoneWeapon WeaponCondition.Notched)
  Assert.Equal(WeaponCondition.Pristine, TowerSession.restHoneWeapon WeaponCondition.Pristine)

[<Fact>]
let ``waiting widens focus one step at a time and moving narrows it back at the same rate`` () =
  let afterWaits =
    [ LocomotionState.Walking
      TowerSession.focusOnWait LocomotionState.Walking
      TowerSession.focusOnWait (TowerSession.focusOnWait LocomotionState.Walking) ]

  Assert.Equal<LocomotionState list>(
    [ LocomotionState.Walking
      LocomotionState.Stationary 1
      LocomotionState.Stationary 2 ],
    afterWaits
  )

  // Each step back narrows exactly where a wait widened, then settles at the walking base.
  let deepest = LocomotionState.Stationary 2
  let once = TowerSession.focusOnMove deepest

  Assert.Equal(LocomotionState.Stationary 1, once)
  Assert.Equal(LocomotionState.Walking, TowerSession.focusOnMove once)
  Assert.Equal(LocomotionState.Walking, TowerSession.focusOnMove LocomotionState.Walking)
