namespace Fornach.Domain.Tests

open FsCheck
open Fornach.Domain

module Generators =
  let meterValueGen: Gen<int> =
    Gen.frequency
      [ (2, Gen.constant 0)
        (2, Gen.constant 100)
        (1, Gen.choose (-500, -1))
        (4, Gen.choose (1, 99))
        (1, Gen.choose (101, 500)) ]

  let meterGen: Gen<Meter> = Gen.choose (0, 100) |> Gen.map Meter.Create

  let statusMetersGen: Gen<StatusMeters> =
    gen {
      let! r = meterGen
      let! ex = meterGen
      let! ov = meterGen
      let! fr = meterGen
      let! cf = meterGen
      let! cn = meterGen
      let! pr = meterGen

      return
        { Recklessness = r
          Exhaustion = ex
          Overwhelm = ov
          Frustration = fr
          CognitiveFatigue = cf
          Confusion = cn
          Provoke = pr }
    }

  let statBlockGen: Gen<StatBlock> =
    gen {
      let! values =
        Attributes.all
        |> List.map (fun stat ->
          gen {
            let! v = Gen.choose (1, 30)
            return stat, v
          })
        |> Gen.sequence

      return StatBlock.Create values
    }

  let combatantGen: Gen<Combatant> =
    gen {
      let id = CombatantId.New()
      let! nameLen = Gen.choose (3, 10)
      let name = sprintf "Fighter_%d" nameLen
      let! hp = Gen.choose (10, 200)
      let! morale = Gen.choose (10, 200)
      let! stats = statBlockGen
      let! meters = statusMetersGen
      let baseFighter = Combatant.create id name hp morale stats
      return Combatant.updateMeters (fun _ -> meters) baseFighter
    }

type DomainArbitraries =
  static member MeterValue() = Arb.fromGen Generators.meterValueGen
  static member Meter() = Arb.fromGen Generators.meterGen
  static member StatusMeters() = Arb.fromGen Generators.statusMetersGen
  static member Combatant() = Arb.fromGen Generators.combatantGen
