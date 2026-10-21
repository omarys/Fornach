module Fornach.Tests.BleedRecoveryTests

open Xunit
open Fornach.Domain
open Fornach.Engine

let private stats =
  StatBlock.Create
    [ StatId.Force, 100
      StatId.Fortitude, 45
      StatId.Finesse, 40
      StatId.Reflex, 40
      StatId.Prowess, 40
      StatId.Poise, 45
      StatId.Intellect, 40
      StatId.Resolve, 45
      StatId.Acuity, 40
      StatId.Intuition, 40
      StatId.Acumen, 40
      StatId.Composure, 45 ]

/// A wounded delver: half Health, half Morale, half armor, and a wound still bleeding.
let private wounded (bleedStacks: int) =
  let c = Combatant.create (CombatantId.New()) "Delver" 200 200 stats

  { c with
      Health = { c.Health with Current = 100 }
      Morale = { c.Morale with Current = 100 }
      Armor = { c.Armor with Current = 25 }
      BleedStacks = bleedStacks }

/// The out-of-combat recovery the safe-step branch runs: 10% pools, 5% armor, one bleed stack closed.
let private safeStep (state: TowerRunState) =
  let player = TowerSession.recoverOutOfCombat state.Player TowerSession.bleedClosedPerStep 20 20 3
  TowerSession.honeOnSafeStep { state with Player = player }

[<Fact>]
let ``a turn of movement closes one bleed stack and never below zero`` () =
  Assert.Equal(1, TowerSession.bleedClosedPerStep)
  Assert.Equal(3, TowerSession.closeBleed 4 1)
  Assert.Equal(0, TowerSession.closeBleed 1 1)
  Assert.Equal(0, TowerSession.closeBleed 0 1)

[<Fact>]
let ``a turn of waiting closes five bleed stacks and never below zero`` () =
  Assert.Equal(5, TowerSession.bleedClosedPerRest)
  Assert.Equal(1, TowerSession.closeBleed 6 5)
  Assert.Equal(0, TowerSession.closeBleed 5 5)
  Assert.Equal(0, TowerSession.closeBleed 2 5)

[<Fact>]
let ``a wound still bleeding holds Health and Morale shut while armor is tended anyway`` () =
  let player = wounded 6

  let stepped = TowerSession.recoverOutOfCombat player TowerSession.bleedClosedPerStep 20 20 3
  Assert.Equal(5, stepped.BleedStacks)
  Assert.Equal(100, stepped.Health.Current)
  Assert.Equal(100, stepped.Morale.Current)
  Assert.Equal(28, stepped.Armor.Current)

  let rested = TowerSession.recoverOutOfCombat player TowerSession.bleedClosedPerRest 66 66 10
  Assert.Equal(1, rested.BleedStacks)
  Assert.Equal(100, rested.Health.Current)
  Assert.Equal(100, rested.Morale.Current)
  Assert.Equal(35, rested.Armor.Current)

[<Fact>]
let ``four bleeding safe steps still hone the weapon on the fourth`` () =
  let player = { wounded 12 with WeaponCondition = WeaponCondition.Broken }
  let start = { TowerSession.initSession player 7 1 with Player = player }

  // Three steps in: the wound has not closed and nothing has been honed yet.
  let afterThree = [ 1 .. 3 ] |> List.fold (fun s _ -> safeStep s) start
  Assert.Equal(9, afterThree.Player.BleedStacks)
  Assert.Equal(100, afterThree.Player.Health.Current)
  Assert.Equal(34, afterThree.Player.Armor.Current)
  Assert.Equal(WeaponCondition.Broken, afterThree.Player.WeaponCondition)
  Assert.Equal(3, afterThree.SafeStepsSinceHone)

  let afterFour = safeStep afterThree
  Assert.Equal(8, afterFour.Player.BleedStacks)
  Assert.Equal(100, afterFour.Player.Health.Current)
  Assert.Equal(WeaponCondition.Damaged, afterFour.Player.WeaponCondition)
  Assert.Equal(0, afterFour.SafeStepsSinceHone)

[<Fact>]
let ``the turn that closes the wound is the turn the regen resumes`` () =
  // A step closes the last stack.
  let stepped = TowerSession.recoverOutOfCombat (wounded 1) TowerSession.bleedClosedPerStep 20 20 3
  Assert.Equal(0, stepped.BleedStacks)
  Assert.Equal(120, stepped.Health.Current)
  Assert.Equal(120, stepped.Morale.Current)

  // A wait closes five, so five stacks are the last of the wound too.
  let rested = TowerSession.recoverOutOfCombat (wounded 5) TowerSession.bleedClosedPerRest 66 66 10
  Assert.Equal(0, rested.BleedStacks)
  Assert.Equal(160, rested.Health.Current)
  Assert.Equal(160, rested.Morale.Current)

[<Fact>]
let ``a bleeding delver who keeps waiting clears the wound and then starts healing`` () =
  let mutable player = wounded 12
  let mutable turns = 0

  while player.BleedStacks > 0 && turns < 20 do
    let before = player
    player <- TowerSession.recoverOutOfCombat player TowerSession.bleedClosedPerRest 66 66 16
    turns <- turns + 1

    if player.BleedStacks > 0 then
      // Wound still open: the pools did not budge, but the armor was still repaired.
      Assert.Equal(before.Health.Current, player.Health.Current)
      Assert.Equal(before.Morale.Current, player.Morale.Current)
      Assert.True(player.Armor.Current > before.Armor.Current || player.Armor.Current = 40)
    else
      // Waiting closed the wound this turn, so the regen resumed on the same turn.
      Assert.Equal(160, player.Health.Current)
      Assert.Equal(160, player.Morale.Current)

  Assert.Equal(3, turns) // 12 -> 7 -> 2 -> 0
  Assert.Equal(0, player.BleedStacks)
  Assert.Equal(160, player.Health.Current) // capped at 80% of 200 by restRecoverPool
  Assert.Equal(160, player.Morale.Current)
  Assert.Equal(40, player.Armor.Current) // repaired every single turn, capped at 80% of 50

  // Healthy and unbloodied, the patient keeps healing up toward the rest cap.
  let damaged = { player with Health = { player.Health with Current = 100 } }
  let rested = TowerSession.recoverOutOfCombat damaged TowerSession.bleedClosedPerRest 66 66 16
  Assert.Equal(160, rested.Health.Current)
  Assert.Equal(0, rested.BleedStacks)
