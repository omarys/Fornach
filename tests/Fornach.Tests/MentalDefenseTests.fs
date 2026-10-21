module Fornach.Tests.MentalDefenseTests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// Deterministic roller: dice always come up low (3), percentiles sit at 50.
let private roller: DiceRoller =
  fun _ max -> if max = 100 then 50 else 3

let private noviceMage () = TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice

/// The mental-plane intents a caster can actually reach for.
let private mentalIntents =
  [ StandardAttack (AuthorityDecree false)
    StandardAttack (GuileDeception false)
    StandardAttack (AcumenInterrogation false)
    StandardAttack (ArcaneCataclysm false)
    StandardAttack (SynapticGlamour false)
    StandardAttack (MirrorIllusion false)
    StandardAttack (RunicWardTrap false)
    StandardAttack (DisorientingShockwave false)
    StandardAttack StaffSiphon ]

/// Worst Morale loss any single mental attack inflicts on `target`.
let private worstMoraleLoss (target: Combatant) =
  let mage = noviceMage ()

  mentalIntents
  |> List.map (fun intent ->
    let res = ActionResolver.resolve roller intent mage target
    intent, target.Morale.Current - res.Target.Morale.Current)

[<Theory>]
[<InlineData("Master")>]
[<InlineData("GrandMaster")>]
let ``a level one mage can barely scratch a Master or Grandmaster champion`` (tierName: string) =
  let tier =
    match tierName with
    | "Master" -> CombatTier.Master
    | "GrandMaster" -> CombatTier.GrandMaster
    | other -> failwithf "unexpected tier %s" other

  for cls in [ CharacterClass.Fighter; CharacterClass.Warrior; CharacterClass.Rogue; CharacterClass.Soldier ] do
    let champ = TierFactory.createClassTier cls tier

    for (intent, moraleLost) in worstMoraleLoss champ do
      Assert.True(
        moraleLost <= 1,
        sprintf "%A %s lost %d Morale to a Novice mage's %A" cls tierName moraleLost intent
      )

[<Fact>]
let ``physical classes carry respectable mental defence`` () =
  // The low-intelligence-for-strength trade-off is gone (no Berserker), so an off-plane mental
  // defence should sit close to the primary, not at half of it.
  let prim = 45 + (40 - 1) * 4 // physical primary at Veteran

  for cls in [ CharacterClass.Fighter; CharacterClass.Warrior; CharacterClass.Rogue; CharacterClass.Soldier ] do
    let champ = TierFactory.createClassTier cls CombatTier.Veteran

    let mentalDefence =
      [ champ.GetStat Resolve; champ.GetStat Intuition; champ.GetStat Composure ]
      |> List.min

    Assert.True(
      mentalDefence >= int (float prim * 0.75),
      sprintf "%A mental defence %d is below 75%% of primary %d" cls mentalDefence prim
    )
