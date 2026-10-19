module Fornach.Tests.AiStanceTests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

[<Fact>]
let ``a physical duelist with tied attributes strikes instead of shuffling stances`` () =
  // Every class tier is authored with Force = Finesse = Prowess, which made the old stance ladder
  // self-contradictory: in Power stance the Finesse test matched, in Agility the Force test
  // matched, so the combatant shifted between them forever and dealt zero damage all duel.
  let fighter =
    { TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master with
        PlayerStyle = true
        Preparations = [] }

  let mage = { TierFactory.createClassTier CharacterClass.Mage CombatTier.Master with PlayerStyle = true }

  Assert.Equal(CombatStance.PowerStance, fighter.Stance)
  Assert.Equal(fighter.GetStat Force, fighter.GetStat Finesse)

  match AI.chooseIntent fighter mage with
  | ActionIntent.StandardAttack _ -> ()
  | other -> failwithf "expected a strike, got %A" other

[<Fact>]
let ``a duelist whose best vector is off-stance repositions once, then strikes`` () =
  // Agility-led attributes must still be reachable: the combatant should shift, not stall.
  let duelist =
    { TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master with
        PlayerStyle = true
        Preparations = [] }

  let shifted =
    { duelist with
        Stance = CombatStance.DisciplineStance }

  match AI.chooseIntent shifted duelist with
  | ActionIntent.ShiftStance _ -> ()
  | ActionIntent.StandardAttack _ -> ()
  | other -> failwithf "expected a stance shift or a strike, got %A" other
