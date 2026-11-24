module Fornach.Tests.AiStanceTests

open Xunit
open Fornach.Domain
open Fornach.Engine

[<Fact>]
let ``a physical duelist with tied attributes strikes instead of shuffling stances`` () =
  // When Force = Finesse = Prowess, the combatant must strike instead of shifting stances forever.
  let baseFighter =
    { TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master with
        PlayerStyle = true
        Preparations = [] }
  let tied = baseFighter.GetStat Prowess
  let fighter =
    { baseFighter with
        Stats =
          baseFighter.Stats
            .Modify(Force, tied - baseFighter.GetStat Force)
            .Modify(Finesse, tied - baseFighter.GetStat Finesse) }

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

/// Dies to the plain strike: the overkill guard should not spend Recklessness on a corpse.
let private weakFoe () =
  { TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice with
      Health = Pool.Create 40
      Preparations = [] }

/// Outlasts the burst by three orders of magnitude while staying hittable, so the damage estimates
/// are real numbers to rank on rather than a pair of zeroes.
let private durableFoe () =
  { TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice with
      Health = Pool.Create 100_000
      Preparations = [] }

/// Outclasses the player's guard outright: every estimate ties at zero, so nothing is worth a gamble.
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
let ``a foe the plain strike already finishes is not gambled on`` () =
  // Recklessness spent on a corpse is exposure for nothing, so the overkill guard takes the cheap
  // strike even though the gamble would report more damage.
  Assert.False(gambitFlag (chosenAttack (player ()) (weakFoe ())))

[<Fact>]
let ``a foe that outlasts the burst draws the gambit`` () =
  // The hardest-hitting action wins whenever the plain strike cannot close the fight: the gamble's
  // 1.5x is worth its spike as long as the spike keeps us under the crit cap.
  let self = withRecklessness 20 (player ())
  Assert.True(gambitFlag (chosenAttack self (durableFoe ())))

[<Fact>]
let ``a strike that cannot land is not gambled on`` () =
  // Every form estimates zero against a foe that outclasses the guard, and a tie goes to the cheaper
  // action: paying Recklessness for no damage only walks us toward the crit cap.
  let self = withRecklessness 20 (player ())
  Assert.False(gambitFlag (chosenAttack self (dangerousFoe ())))

[<Fact>]
let ``past half the meter the gamble is withheld`` () =
  // Recklessness 75 penalises every defence stat and 100 forces a RecklessExposure collapse, so a
  // combatant already past the ceiling presses plainly even against a foe that will outlast it.
  let self = withRecklessness 60 (player ())
  Assert.False(gambitFlag (chosenAttack self (durableFoe ())))

[<Fact>]
let ``past the shield-hold band the shield is raised to shed Recklessness`` () =
  // Recklessness is one clean hit from the crit cap at 75, so the recommendation sheds instead of
  // pressing: a physical actor raises the shield, which bleeds the meter and raises plating.
  let self = withRecklessness 61 (player ())

  match AI.chooseIntent self (dangerousFoe ()) with
  | ActionIntent.RecoveryAction DefensiveReset.RaiseShield -> ()
  | other -> failwithf "expected the shield hold, got %A" other

[<Fact>]
let ``the gamble is withheld when even the cheapest spike would reach the crit cap`` () =
  // The budget is the projected meter plus a safety band for what a foe adds on top, not the current
  // meter, and the spike differs by strike: in AgilityStance, the cheapest physical gamble
  // (FinesseCadence) fits one point under the banded line and stops fitting at it.
  let cheap = ActionResolver.gambitReckSpike (FinesseCadence true)
  let line = AI.critCap - AI.recklessnessHeadroom
  let foe = durableFoe ()
  let agilePlayer = { player () with Stance = CombatStance.AgilityStance }
  let atCap = withRecklessness (line - cheap) agilePlayer
  let below = withRecklessness (line - cheap - 1) agilePlayer
  Assert.True(gambitFlag (chosenAttack below foe))
  Assert.False(gambitFlag (chosenAttack atCap foe))

[<Fact>]
let ``opposition is left to its own discipline`` () =
  // The estimator's press is the player's kit; an enemy keeps the generic AI, whose own gambit gate is
  // a flat 25 and whose species instincts the estimator would otherwise override.
  let foe = { weakFoe () with PlayerStyle = false }
  Assert.True((AI.chooseEstimatedAttack foe (player ())).IsNone)

[<Fact>]
let ``the auto path takes the estimator's strike`` () =
  // End to end through the public entry point: a fresh player against a foe that outlasts it gambles.
  match AI.chooseIntent (player ()) (durableFoe ()) with
  | ActionIntent.StandardAttack attack -> Assert.True(gambitFlag attack)
  | other -> failwithf "expected a strike, got %A" other

// --- a successful gamble pays a Prowess-tempered spike -------------------------------------------

/// A duelist of a chosen Prowess, from the Vor, on Recklessness 10.
let private withProwess prowess =
  let stats =
    StatBlock.Create [
      Force, 200; Fortitude, 50
      Finesse, 50; Reflex, 50
      Prowess, prowess; Poise, 50
      Intellect, 50; Resolve, 50
      Acuity, 50; Intuition, 50
    ]

  Combatant.create (CombatantId.New()) "Gambler" 9000 9000 stats
  |> Combatant.seizeVor
  |> fun c -> { c with PlayerStyle = true }
  |> withRecklessness 10

/// Guard so heavy the strike cannot penetrate it: the whiff branch.
let private wall () =
  let stats =
    StatBlock.Create [
      Force, 1; Fortitude, 400
      Finesse, 1; Reflex, 400
      Prowess, 1; Poise, 400
      Intellect, 1; Resolve, 400
      Acuity, 1; Intuition, 400
      Prowess, 1; Poise, 400
    ]

  Combatant.create (CombatantId.New()) "Wall" 9000 9000 stats |> Combatant.yieldVor

let private recklessnessAfter (roller: DiceRoller) (actor: Combatant) (target: Combatant) =
  let res =
    ActionResolver.resolve roller (ActionIntent.StandardAttack(ForceStrike true)) actor target

  res.Actor.Meters.Recklessness.Value - actor.Meters.Recklessness.Value

/// A soft target of its own: a tier template brings its own clones and situs, which would answer the
/// strike for it and muddy what the spike actually cost.
let private softFoe () =
  let stats =
    StatBlock.Create [
      Force, 1; Fortitude, 10
      Finesse, 1; Reflex, 10
      Prowess, 1; Poise, 10
      Intellect, 1; Resolve, 10
      Acuity, 1; Intuition, 10
      Prowess, 1; Poise, 10
    ]

  Combatant.create (CombatantId.New()) "Soft" 9000 9000 stats |> Combatant.yieldVor

[<Fact>]
let ``a landed gamble pays a Prowess-tempered spike`` () =
  // Prowess trims what a landed gamble costs: 30 at Prowess 0, half at Prowess 100.
  let topRoll: DiceRoller = fun _ _ -> 6
  let spike = ActionResolver.gambitReckSpike (ForceStrike true)
  let foe = softFoe ()
  Assert.Equal(spike, recklessnessAfter topRoll (withProwess 0) foe)
  Assert.Equal(spike / 2, recklessnessAfter topRoll (withProwess 100) foe)

[<Fact>]
let ``a whiffed gamble pays the declared spike in full`` () =
  // Control is only owed to the strike that landed: the miss is the full price, whatever the Prowess.
  let topRoll: DiceRoller = fun _ _ -> 6
  let spike = ActionResolver.gambitReckSpike (ForceStrike true)
  Assert.Equal(spike, recklessnessAfter topRoll (withProwess 100) (wall ()))

// --- Poise guards the condition tracks; Prowess guards what an action costs its author --------------

/// A duelist whose Poise the test chooses, everything else fixed.
let private withPoise poise =
  let stats =
    StatBlock.Create [
      Force, 120; Fortitude, 60
      Finesse, 60; Reflex, 60
      Prowess, 60; Poise, poise
      Intellect, 60; Resolve, 60
      Acuity, 60; Intuition, 60
    ]

  Combatant.create (CombatantId.New()) "Guard" 9000 9000 stats
  |> fun c -> { c with Armor = ArmorIntegrity.Create 60000 }
  |> Combatant.yieldVor

[<Fact>]
let ``Poise holds back the meter pressure a strike leaves behind`` () =
  // The same strike against the same sheet but a different Poise: the burden it leaves on the
  // condition tracks is what Poise lets through, so the high-Poise guard ends the exchange lighter.
  let topRoll: DiceRoller = fun _ _ -> 6

  let probe poise =
    let target = withPoise poise

    let res =
      ActionResolver.resolve topRoll (ActionIntent.StandardAttack(ForceStrike false)) (withProwess 60) target

    res.Target.Meters.Exhaustion.Value - target.Meters.Exhaustion.Value

  let soft = probe 1
  let hard = probe 100

  Assert.True(soft > 0)
  Assert.True(soft > hard, sprintf "Poise 100 should hold back Exhaustion (raw %d, held %d)" soft hard)

[<Fact>]
let ``Poise at 100 halves the pressure and never holds back more than three quarters`` () =
  // A raw sheet has no Poise to speak of (the stat floors at 1), so the guard is whole.
  Assert.True(ActionResolver.poiseGuardFraction (withPoise 0) > 0.99)
  Assert.Equal(0.5, ActionResolver.poiseGuardFraction (withPoise 100), 3)
  Assert.Equal(0.25, ActionResolver.poiseGuardFraction (withPoise 400), 3)
  Assert.Equal(25, ActionResolver.temperConditionAmount (withPoise 0) 25)
  Assert.Equal(12, ActionResolver.temperConditionAmount (withPoise 100) 25)

[<Fact>]
let ``a Berserk Tincture's fever pitch is tempered by Prowess`` () =
  // The tincture is an action that adds Recklessness, so its spike is Prowess-tempered like a
  // landed gamble's: half at Prowess 100, full at 0.
  let deploy prowess =
    let slot = PreparationSlot.create PreparationType.BerserkTincture 1
    let base' = withProwess prowess

    let actor =
      { base' with
          PlayerStyle = true
          Preparations = [ slot ]
          Progression = { base'.Progression with Preparations = [ slot ] } }

    let before = actor.Meters.Recklessness.Value

    let res =
      ActionResolver.resolve (fun _ _ -> 6) (ActionIntent.DeployPreparation(PreparationType.BerserkTincture, None)) actor (withPoise 0)

    res.Actor.Meters.Recklessness.Value - before

  Assert.Equal(15, deploy 0)
  Assert.Equal(8, deploy 100)

// --- the trained discipline pair: who has it, and who grows it -------------------------------------

[<Fact>]
let ``a monster has no trained discipline pair; humanoid opposition and the player do`` () =
  // Prowess/Poise is trained control, not instinct. Any combatant with a `MonsterFamily` reads zero on
  // both, whatever its sheet says; a humanoid — the player classes, a Knight, a Wizard — reads its sheet.
  let hound = Bestiary.slagHound.ToCombatant (CombatantId.New())
  Assert.True(hound.MonsterFamily.IsSome)
  Assert.Equal(0, hound.GetStat StatId.Prowess)
  Assert.Equal(0, hound.GetStat StatId.Poise)

  // Humanoid opposition is authored without a `MonsterFamily`, so it keeps what it was trained to have.
  let knightStats =
    StatBlock.Create [
      Force, 200; Fortitude, 200
      Finesse, 150; Reflex, 150
      Prowess, 220; Poise, 210
      Intellect, 120; Resolve, 160
      Acuity, 120; Intuition, 160
    ]

  let knight = Combatant.create (CombatantId.New()) "Knight" 2000 2000 knightStats
  Assert.True(knight.IsHumanoid)
  Assert.Equal(220, knight.GetStat StatId.Prowess)
  Assert.Equal(210, knight.GetStat StatId.Poise)

  let player = TierFactory.createClassLevel CharacterClass.Fighter 40
  Assert.True(player.GetStat StatId.Prowess > 0)
  Assert.True(player.GetStat StatId.Poise > 0)

[<Fact>]
let ``only a stressed victory grows the pair, and a monster cannot grow what it does not have`` () =
  // The player learns from a fight that cost blood, morale or status; +1 more if it was won above half
  // Recklessness. A monster has no pair to grow, so the gate keeps it at zero even through the same call.
  let bloodied =
    { TierFactory.createClassLevel CharacterClass.Fighter 40 with
        Health = (TierFactory.createClassLevel CharacterClass.Fighter 40).Health.ApplyDelta -50 }

  let grown = Combatant.growFromStressedVictory bloodied
  Assert.Equal(bloodied.GetStat StatId.Prowess + 5, grown.GetStat StatId.Prowess)
  Assert.Equal(bloodied.GetStat StatId.Poise + 5, grown.GetStat StatId.Poise)

  let reckless =
    { bloodied with
        Meters = { bloodied.Meters with Recklessness = Meter.Create 60 } }

  let grownReckless = Combatant.growFromStressedVictory reckless
  Assert.Equal(bloodied.GetStat StatId.Prowess + 6, grownReckless.GetStat StatId.Prowess)

  let hound = Bestiary.slagHound.ToCombatant (CombatantId.New())
  let fedHound = Combatant.growFromStressedVictory { hound with Health = hound.Health.ApplyDelta -50 }
  Assert.Equal(0, fedHound.GetStat StatId.Prowess)
  Assert.Equal(0, fedHound.GetStat StatId.Poise)

// --- Stance-respecting AttackEstimator, stack spenders, recovery threshold, humanoid AI, adaptation ---

[<Fact>]
let ``AttackEstimator evaluates strikes in the actor's currently held stance`` () =
  let p = { player () with Stance = CombatStance.PowerStance }
  let contested = AttackEstimator.contestedAs p (FinesseCadence false)
  Assert.Equal(CombatStance.PowerStance, contested.Stance)

[<Fact>]
let ``AI chooses a stack spender when holding three or more stacks against a durable foe`` () =
  let stacked = { player () with Momentum = 4; Flow = 4 }
  match chosenAttack stacked (durableFoe ()) with
  | ForceInsightStrike | FinesseInsightStrike | CalculatedFlawStrike | MasterfulDisarm _ -> ()
  | other -> failwithf "expected a stack spender at 4 stacks, got %A" other

[<Fact>]
let ``AI presses through Recklessness 45 and recovers at 55`` () =
  let pressing = withRecklessness 45 (player ())
  match AI.chooseIntent pressing (durableFoe ()) with
  | ActionIntent.StandardAttack _ -> ()
  | other -> failwithf "expected attack at Recklessness 45, got %A" other

  let strained = withRecklessness 55 (player ())
  match AI.chooseIntent strained (durableFoe ()) with
  | ActionIntent.RecoveryAction _ -> ()
  | other -> failwithf "expected recovery at Recklessness 55, got %A" other

[<Fact>]
let ``a mental combatant under severe physical Exhaustion recovers with SteadyForm instead of looping CenterMind`` () =
  let baseMage =
    { TierFactory.createClassTier CharacterClass.Mage CombatTier.Master with
        PlayerStyle = true
        Preparations = [] }
  let exhaustedMage =
    { baseMage with
        Meters = { baseMage.Meters with Exhaustion = Meter.Create 65 } }
  match AI.chooseIntent exhaustedMage (durableFoe ()) with
  | ActionIntent.RecoveryAction DefensiveReset.SteadyForm -> ()
  | other -> failwithf "expected SteadyForm to drain Exhaustion on a Mental combatant, got %A" other

[<Fact>]
let ``Veteran humanoid opposition uses gambits at low Recklessness and ShieldBash vs reckless foes`` () =
  let veteran =
    { TierFactory.createClassTier CharacterClass.Fighter CombatTier.Veteran with
        PlayerStyle = false
        MonsterFamily = None
        Preparations = [] }
  let calmTarget = durableFoe ()
  match AI.chooseIntent veteran calmTarget with
  | ActionIntent.StandardAttack atk -> Assert.True(gambitFlag atk)
  | other -> failwithf "expected gambit strike from Veteran humanoid, got %A" other

  let recklessTarget = withRecklessness 50 (durableFoe ())
  match AI.chooseIntent veteran recklessTarget with
  | ActionIntent.StandardAttack (ShieldBash _) -> ()
  | other -> failwithf "expected ShieldBash against reckless target, got %A" other

[<Fact>]
let ``adaptIntentToRepetition feints repeated active guards and bashes consecutive RaiseShield turtles`` () =
  let enemy =
    { TierFactory.createClassTier CharacterClass.Fighter CombatTier.Veteran with
        PlayerStyle = false
        MonsterFamily = None }
  let guard = Some(Guard(FortitudeGuard false))
  let feinted = AI.adaptIntentToRepetition enemy (StandardAttack(ForceStrike false)) guard guard 0
  Assert.Equal(StandardAttack(FinesseCadence false), feinted)

  let bashed = AI.adaptIntentToRepetition enemy (StandardAttack(ForceStrike false)) None None 2
  Assert.Equal(StandardAttack(ShieldBash false), bashed)

[<Fact>]
let ``TowerSession.isCleanMasteryVictory detects Collapse Execution and Indes Seizure`` () =
  let actorId = CombatantId.New()
  let targetId = CombatantId.New()
  Assert.True(TowerSession.isCleanMasteryVictory [ CombatEvent.Executed(actorId, targetId, Plane.Physical) ])
  Assert.True(TowerSession.isCleanMasteryVictory [ CombatEvent.TempoShifted(actorId, targetId, "Counter seized the Indes!") ])
  Assert.False(TowerSession.isCleanMasteryVictory [])
