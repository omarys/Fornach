namespace Fornach.Story

open Fornach.Domain
open Fornach.Engine

/// The Grief Aspects' signature presses. Each Aspect is one way of arguing with grief, and each argues in
/// its own grammar, so those grammars live here -- with the story that invented them -- rather than in the
/// engine's offensive chooser. The engine only asks its table for the press named on the combatant, which
/// is what lets a different setting ship its own without the engine learning what any of them mean.
module Policies =
  /// Risk threshold for gambits: only fire when reckless entropy is low.
  let private isGambit (self: Combatant) = self.Meters.Recklessness.Value < 25

  /// Guilt is the heavy of the six: 210 Force behind a sledgehammer and almost no reach into the mind.
  /// With no press of its own it fell through to the GriefManifestation profile, which hands out a Mental
  /// attack -- and `AttackClassification.IsAllowedFor` refuses that outright for a Physical actor. The
  /// resolver then burned the turn on "cannot execute magic attacks", so the Aspect never landed a blow.
  let private guilt (self: Combatant) (_: Combatant) : ActionIntent =
    StandardAttack (ForceStrike (isGambit self))

  /// Denial phases out of the world while it can still afford to, and once its clones are up it argues on
  /// the mental plane: Acuity 210 and Intellect 180 outweigh Finesse 235 once the Warrior plane flip
  /// applies, so the FinesseCadence press it used to make was refused outright and the Aspect simply
  /// stopped attacking. Glamour is the mental plane's basic strike, the way the wraiths use it, and
  /// unlike Decoy Swarm it does not stack Recklessness onto a target already being buried in it.
  let private denial (self: Combatant) (_: Combatant) : ActionIntent =
    if self.MirrorClones < 2 && self.Meters.Recklessness.Value < 70 then
      StandardAttack (TraumaAttack FinesseGambit)
    else
      StandardAttack (SynapticGlamour (isGambit self))

  /// Anger erupts while it still has composure to spend, then swings wild. The `true` is a gambit taken
  /// deliberately rather than a low-risk press: a reckless Aspect has nothing left to protect.
  let private anger (self: Combatant) (_: Combatant) : ActionIntent =
    if self.Meters.Recklessness.Value < 65 then
      StandardAttack (TraumaAttack ForceGambit)
    else
      StandardAttack (ForceStrike true)

  /// Bargaining only has something to trade while the target still has morale left to lose.
  let private bargaining (self: Combatant) (opponent: Combatant) : ActionIntent =
    if opponent.Morale.Current > 40 && self.Meters.Recklessness.Value < 65 then
      StandardAttack (TraumaAttack AcumenGambit)
    else
      StandardAttack (AcumenInterrogation (isGambit self))

  /// Depression settles on a mind that has not already been worn down.
  let private depression (self: Combatant) (opponent: Combatant) : ActionIntent =
    if opponent.Meters.CognitiveFatigue.Value < 80 then
      StandardAttack (TraumaAttack FortitudeGambit)
    else
      StandardAttack (ForceStrike false)

  /// Acceptance has no branch to make. It simply resolves.
  let private acceptance (_: Combatant) (_: Combatant) : ActionIntent =
    StandardAttack (TraumaAttack ComposureGambit)

  /// The table the CLI hands to `AI.chooseIntentUsing`.
  let all : Map<PolicyId, AI.AttackPolicy> =
    Map.ofList
      [ PolicyIds.griefGuilt, { RecoveryThreshold = 40; ChooseAttack = guilt }
        PolicyIds.griefDenial, { RecoveryThreshold = 40; ChooseAttack = denial }
        // Anger holds the line to the edge of its own meter. At the generic 40 it would break off to
        // recover every time its own eruption left it winded, which is the opposite of what it is for.
        PolicyIds.griefAnger, { RecoveryThreshold = 95; ChooseAttack = anger }
        PolicyIds.griefBargaining, { RecoveryThreshold = 40; ChooseAttack = bargaining }
        PolicyIds.griefDepression, { RecoveryThreshold = 40; ChooseAttack = depression }
        PolicyIds.griefAcceptance, { RecoveryThreshold = 40; ChooseAttack = acceptance } ]
