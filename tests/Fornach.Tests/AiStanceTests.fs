module Fornach.Tests.AiStanceTests

open Xunit
open Fornach.Domain
open Fornach.Engine

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

// --- the player's own attack: the estimator's pick, and the Recklessness budget --------------------

let private player () =
  { TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master with
      PlayerStyle = true
      Preparations = [] }

/// Dies to the opener: expected rounds-to-kill stays inside the burst.
let private weakFoe () =
  { TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice with
      Health = Pool.Create 40
      Preparations = [] }

/// Outlasts the burst by three orders of magnitude, which is the whole definition of dangerous here.
let private dangerousFoe () =
  { TierFactory.createClassTier CharacterClass.Mage CombatTier.GrandMaster with
      Health = Pool.Create 100_000
      Preparations = [] }

let private gambitFlag (attack: AttackClassification) =
  match attack with
  | ForceStrike flag
  | FinesseCadence flag
  | ProwessStrike flag
  | ShieldBash flag
  | SynapticGlamour flag
  | ArcaneCataclysm flag
  | AcumenInterrogation flag -> flag
  | other -> failwithf "expected a flagged basic strike, got %A" other

let private chosenAttack (self: Combatant) (opponent: Combatant) =
  match AI.chooseEstimatedAttack self opponent with
  | Some(ActionIntent.StandardAttack attack) -> attack
  | other -> failwithf "expected an estimated strike, got %A" other

let private withRecklessness value (self: Combatant) =
  { self with
      Meters = { self.Meters with Recklessness = Meter.Create value } }

[<Fact>]
let ``a weak foe is finished with the plain strike`` () =
  // The budget is unspent because nothing is at stake: a foe that dies to the opener gets the cheap
  // +4 strike, not the +8 gamble.
  Assert.False(gambitFlag (chosenAttack (player ()) (weakFoe ())))

[<Fact>]
let ``a foe that outlasts the burst draws the gambit`` () =
  // Dangerous is measured in expected rounds-to-kill, not level or health: this foe is still standing
  // when the player's pool runs dry, so the 1.35x press is worth its Recklessness.
  let self = withRecklessness 20 (player ())
  Assert.True(gambitFlag (chosenAttack self (dangerousFoe ())))

[<Fact>]
let ``past half the meter the gamble is withheld`` () =
  // Recklessness 75 penalises every defence stat and 100 forces a RecklessExposure collapse, so a
  // combatant already past the ceiling presses plainly even against a foe that will outlast it.
  let self = withRecklessness 60 (player ())
  Assert.False(gambitFlag (chosenAttack self (dangerousFoe ())))

[<Fact>]
let ``the gamble is withheld exactly at the ceiling`` () =
  // The gate is `Recklessness < 50`, so 50 itself is already too far.
  let self = withRecklessness AI.gambleCeiling (player ())
  Assert.False(gambitFlag (chosenAttack self (dangerousFoe ())))

[<Fact>]
let ``opposition is left to its own discipline`` () =
  // The estimator's press is the player's kit; an enemy keeps the generic AI, whose own gambit gate is
  // a flat 25 and whose species instincts the estimator would otherwise override.
  let foe = { weakFoe () with PlayerStyle = false }
  Assert.True((AI.chooseEstimatedAttack foe (player ())).IsNone)

[<Fact>]
let ``the auto path takes the estimator's strike`` () =
  // End to end through the public entry point: a fresh player against a foe that outlasts it gambles.
  match AI.chooseIntent (player ()) (dangerousFoe ()) with
  | ActionIntent.StandardAttack attack -> Assert.True(gambitFlag attack)
  | other -> failwithf "expected a strike, got %A" other
