namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

module GuardRampTests =

  // These fixtures stand in for the player: the ramp and the guard meters are the player's kit, so a
  // template enemy would (correctly) refuse to compound.
  let private fighter () =
    TierFactory.createClassLevel CharacterClass.Fighter 5
    |> fun c -> { c with PlayerStyle = true }

  [<Fact>]
  let ``a whiff in the Vor and a swing from the Nach both buy a guard stack`` () =
    let roller: DiceRoller = fun _ _ -> 6
    let make poise force =
      let stats =
        StatBlock.Create [
          Force, force; Fortitude, poise
          Finesse, 50; Reflex, 50
          Prowess, 50; Poise, poise
          Intellect, 50; Resolve, poise
          Acuity, 50; Intuition, 50
          Acumen, 50; Composure, poise
        ]
      Combatant.create (CombatantId.New()) "Mirrored" 9000 9000 stats
      |> fun c -> { c with PlayerStyle = true }
    // A wall of Poise the strike cannot penetrate: the whiff branch, from the Vor.
    let whiffed = ActionResolver.resolve roller (ActionIntent.StandardAttack(ForceStrike false)) (make 50 50) (make 900 50)
    Assert.Equal(1, whiffed.Actor.Inertia)
    Assert.Equal(1, whiffed.Actor.Focus)
    // Same swing from the Nach: the answering strike is blunted, and the guard meters pay for it.
    let fromNach =
      ActionResolver.resolve roller (ActionIntent.StandardAttack(ForceStrike false)) (make 50 50) (make 900 50)
    let answered = ActionResolver.resolve roller (ActionIntent.StandardAttack(ForceStrike false)) fromNach.Actor (make 900 50)
    Assert.True(answered.Actor.Inertia >= 1)

  [<Fact>]
  let ``bracing from the Nach buys a guard stack without swinging`` () =
    let roller: DiceRoller = fun _ _ -> 6
    let defender = fighter () |> Combatant.yieldVor
    let attacker = fighter ()
    let braced =
      ActionResolver.resolve roller (ActionIntent.ActiveDefense(FortitudeGuard true)) defender attacker
    Assert.True(braced.Actor.IsInNach || braced.Actor.IsInVor)
    Assert.Equal(1, braced.Actor.Inertia)
    Assert.Equal(1, braced.Actor.Focus)
    // A brace must not load the offense ramp -- that was the ambush bug where slipping showed Flow/Momentum.
    Assert.Equal(0, braced.Actor.Momentum)
    Assert.Equal(0, braced.Actor.Flow)

  [<Fact>]
  let ``the ramp and the guard meters rise independently`` () =
    let c = fighter ()
    let ramped = c |> Combatant.addStacks 3
    Assert.Equal(3, ramped.Momentum)
    Assert.Equal(0, ramped.Inertia)
    Assert.Equal(0, ramped.Focus)
    let guarded = c |> Combatant.addGuardStacks 3
    Assert.Equal(0, guarded.Momentum)
    Assert.Equal(3, guarded.Inertia)
    Assert.Equal(3, guarded.Focus)

  [<Fact>]
  let ``guard stacks sharpen Poise faster than Momentum does`` () =
    let baseC = fighter () |> Combatant.yieldVor
    let withGuard = { baseC with Inertia = Combatant.StackCap; Focus = Combatant.StackCap }
    let withRamp = { baseC with Momentum = Combatant.StackCap }
    Assert.True(withGuard.GetCombatStat StatId.Poise > withRamp.GetCombatStat StatId.Poise)

  [<Fact>]
  let ``recovery wipes the guard meters and rest decays them`` () =
    let guarded = fighter () |> Combatant.addGuardStacks Combatant.StackCap
    Assert.Equal(Combatant.StackCap - 1, (guarded |> Combatant.decayStacks).Inertia)
    Assert.Equal(0, (guarded |> Combatant.clearStacks).Focus)

  [<Fact>]
  let ``opposition never compounds either ramp, however long the fight runs`` () =
    // The ramps and the guard are the main character's kit. A plain template enemy must stay at zero no
    // matter how many stacks are pushed at it -- otherwise a long fight hands the opposition the same
    // compounding the player earns, and class-matrix comparisons start measuring a mechanic no enemy has.
    let mob = TierFactory.createClassLevel CharacterClass.Fighter 5

    Assert.False(mob.PlayerStyle, "plain opposition is not player-style")

    let pushed =
      mob
      |> Combatant.addStacks 40
      |> Combatant.addGuardStacks 40

    Assert.Equal(0, pushed.Flow)
    Assert.Equal(0, pushed.Momentum)
    Assert.Equal(0, pushed.Inertia)
    Assert.Equal(0, pushed.Focus)

    // The player, given the same, banks both -- and each keeps its own ceiling.
    let player = fighter () |> Combatant.addStacks 40 |> Combatant.addGuardStacks 40
    Assert.Equal(Combatant.StackCap, player.Flow)
    Assert.Equal(Combatant.StackCap, player.Momentum)
    Assert.Equal(Combatant.GuardStackCap, player.Inertia)
    Assert.Equal(Combatant.GuardStackCap, player.Focus)
