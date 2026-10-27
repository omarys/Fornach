namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain

/// `Combatant.growFromEncounter` replaced the old Study Stack ramp: a won fight leaves a permanent
/// gain on the Discipline pair — `max(8 - (bearer level - foe level), 2)` each — and +1 on each mental
/// defence. No offensive attribute moves.
module EncounterGrowthTests =

  let private statOf (stat: StatId) (c: Combatant) = c.Stats.Get stat

  let private foe (cls: CharacterClass) (tier: CombatTier) = TierFactory.createClassTier cls tier

  /// The gain the pair is supposed to take for this bearer and this pack.
  let private expectedGain (before: Combatant) (pack: Combatant list) =
    let foeLevel = pack |> List.fold (fun best (f: Combatant) -> max best f.Level) 0
    max (8 - (before.Level - foeLevel)) 2

  [<Fact>]
  let ``a won fight raises Prowess and Poise by the level-gap gain`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let pack = [ foe CharacterClass.Warrior CombatTier.Novice ]
    let grown = Combatant.growFromEncounter pack before
    let gain = expectedGain before pack

    Assert.Equal(statOf StatId.Prowess before + gain, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + gain, statOf StatId.Poise grown)

  [<Fact>]
  let ``the gain falls as the foe falls behind, and never drops below two`` () =
    let veteran = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Veteran

    for tier in [ CombatTier.Novice; CombatTier.Veteran; CombatTier.Master; CombatTier.GrandMaster ] do
      let pack = [ foe CharacterClass.Warrior tier ]
      let grown = Combatant.growFromEncounter pack veteran
      let gain = expectedGain veteran pack

      Assert.True(gain >= 2, sprintf "a %A foe paid %d, below the floor of 2" tier gain)
      Assert.Equal(statOf StatId.Prowess veteran + gain, statOf StatId.Prowess grown)
      Assert.Equal(statOf StatId.Poise veteran + gain, statOf StatId.Poise grown)

    // A peer-level fight is the ceiling; a foe a hundred levels behind is the floor.
    let peer = [ foe CharacterClass.Warrior CombatTier.Veteran ]
    let trivial = [ foe CharacterClass.Warrior CombatTier.Novice ]

    Assert.True(
      expectedGain veteran peer > expectedGain veteran trivial,
      sprintf "a peer fight (%d) should teach more than a trivial one (%d)" (expectedGain veteran peer) (expectedGain veteran trivial)
    )

    Assert.Equal(2, expectedGain veteran [ foe CharacterClass.Warrior CombatTier.Novice ])

  [<Fact>]
  let ``beating a foe above your level pays more than eight, by design`` () =
    // The floor of 2 is the only clamp: an underdog who drops something well above their level is
    // learning the fastest way the game allows, so the gain rises past eight with the disparity.
    let underdog = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Novice

    let gains =
      [ for tier in [ CombatTier.Veteran; CombatTier.Master; CombatTier.GrandMaster ] ->
          let pack = [ foe CharacterClass.Warrior tier ]
          let grown = Combatant.growFromEncounter pack underdog

          Assert.True(pack |> List.forall (fun f -> f.Level > underdog.Level), "the foe should outrank the bearer")
          statOf StatId.Prowess grown - statOf StatId.Prowess underdog ]

    for gained in gains do
      Assert.True(gained > 8, sprintf "an underdog win paid only %d" gained)

    // Harder foe, bigger lesson: the gains climb with the disparity.
    Assert.True(gains[0] < gains[1] && gains[1] < gains[2], sprintf "gains did not climb with the gap: %A" gains)

  [<Fact>]
  let ``the pack size does not compound the gain`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master

    let pack =
      [ foe CharacterClass.Warrior CombatTier.GrandMaster
        foe CharacterClass.Rogue CombatTier.GrandMaster
        foe CharacterClass.Soldier CombatTier.GrandMaster ]

    let grown = Combatant.growFromEncounter pack before
    // A pack is read at its strongest member, and the gain does not compound with its size.
    Assert.Equal(statOf StatId.Prowess before + expectedGain before pack, statOf StatId.Prowess grown)

  [<Fact>]
  let ``both player classes harden Resolve, Intuition and Composure, and neither grows the other's tick`` () =
    let fighter = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.Master
    let novices = [ foe CharacterClass.Warrior CombatTier.Novice ]

    let grownFighter = Combatant.growFromEncounter novices fighter
    Assert.Equal(statOf StatId.Resolve fighter + 1, statOf StatId.Resolve grownFighter)
    Assert.Equal(statOf StatId.Intuition fighter + 1, statOf StatId.Intuition grownFighter)
    Assert.Equal(statOf StatId.Composure fighter + 1, statOf StatId.Composure grownFighter)
    Assert.Equal(statOf StatId.Acumen fighter, statOf StatId.Acumen grownFighter)
    Assert.Equal(statOf StatId.Force fighter, statOf StatId.Force grownFighter)
    Assert.Equal(statOf StatId.Finesse fighter, statOf StatId.Finesse grownFighter)

    let grownMage = Combatant.growFromEncounter novices mage
    Assert.Equal(statOf StatId.Acumen mage + expectedGain mage novices, statOf StatId.Acumen grownMage)
    // Composure is in the Discipline pair and again in the defensive trio, so it takes both.
    Assert.Equal(statOf StatId.Composure mage + expectedGain mage novices + 1, statOf StatId.Composure grownMage)
    Assert.Equal(statOf StatId.Resolve mage + 1, statOf StatId.Resolve grownMage)
    Assert.Equal(statOf StatId.Intuition mage + 1, statOf StatId.Intuition grownMage)
    // A caster gains no physical stat at all, and no Prowess.
    Assert.Equal(statOf StatId.Reflex mage, statOf StatId.Reflex grownMage)
    Assert.Equal(statOf StatId.Prowess mage, statOf StatId.Prowess grownMage)
    Assert.Equal(statOf StatId.Poise mage, statOf StatId.Poise grownMage)

  [<Fact>]
  let ``a generic class takes the same Discipline pair and defensive trio as a Fighter`` () =
    let before = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Master
    let grown = Combatant.growFromEncounter [ foe CharacterClass.Warrior CombatTier.Novice ] before

    Assert.Equal(statOf StatId.Prowess before + expectedGain before [ foe CharacterClass.Warrior CombatTier.Novice ], statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + expectedGain before [ foe CharacterClass.Warrior CombatTier.Novice ], statOf StatId.Poise grown)
    Assert.Equal(statOf StatId.Resolve before + 1, statOf StatId.Resolve grown)
    Assert.Equal(statOf StatId.Composure before + 1, statOf StatId.Composure grown)
    // Force and Finesse are outside the growth set now.
    Assert.Equal(statOf StatId.Force before, statOf StatId.Force grown)
    Assert.Equal(statOf StatId.Finesse before, statOf StatId.Finesse grown)

  [<Fact>]
  let ``the growth actually shows up on the returned combatant's stats`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Novice
    let grown = Combatant.growFromEncounter [ foe CharacterClass.Warrior CombatTier.Novice ] before

    Assert.True(grown.GetStat StatId.Prowess > before.GetStat StatId.Prowess, "Prowess must rise on the returned combatant")
    Assert.True(grown.GetStat StatId.Poise > before.GetStat StatId.Poise, "Poise must rise on the returned combatant")
    Assert.True(grown.GetStat StatId.Resolve > before.GetStat StatId.Resolve, "A Fighter's Resolve must rise on the returned combatant")
