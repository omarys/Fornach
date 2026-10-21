/// Bar rows must line up: `Display.renderBar` and `Display.renderMeter` pad every label to one
/// shared width, so the first bar cell lands in the same column regardless of label length.
module Fornach.Tests.HudAlignmentTests

open Xunit
open Fornach.Cli
open Fornach.Domain

/// What the terminal actually prints: markup tags removed, escape codes and padding kept.
let private visible (rendered: string) =
  System.Text.RegularExpressions.Regex.Replace(rendered, @"\[[^\[\]]*\]", "")

let private barColumn (rendered: string) =
  let s = visible rendered
  Assert.True(s.IndexOf('█', System.StringComparison.Ordinal) >= 0, "expected a filled bar cell")
  s.IndexOf('█', System.StringComparison.Ordinal)

[<Fact>]
let ``HP and Morale bars start in the same column`` () =
  let hp = Display.renderBar "󰋑 HP (Physical)" 750 750 Theme.Red
  let morale = Display.renderBar "󰧑 Morale (Mental)" 600 600 Theme.Cyan
  Assert.Equal(barColumn hp, barColumn morale)

[<Fact>]
let ``strain meters align despite very different label lengths`` () =
  let short = Display.renderMeter "󰈸 Provoke" (Meter.Create 100) Theme.Red
  let long = Display.renderMeter "󰧑 Cognitive Fatigue" (Meter.Create 100) Theme.Red
  Assert.Equal(barColumn short, barColumn long)

[<Fact>]
let ``bars and meters share one column so the whole HUD is a grid`` () =
  // The vitals block and the strain block are separate grids; they line up only because both use
  // the same label width.
  let morale = Display.renderBar "󰧑 Morale (Mental)" 600 600 Theme.Cyan
  let fatigue = Display.renderMeter "󰧑 Cognitive Fatigue" (Meter.Create 100) Theme.Purple
  Assert.Equal(barColumn morale, barColumn fatigue)

[<Fact>]
let ``a label at the shared width keeps its separating space`` () =
  // PadRight is a floor, not a truncation: one space, then the bar — never label-touching-bar.
  let label = "󰧑 Cognitive Fatigue"
  let stripped = visible (Display.renderBar label 50 100 Theme.Red)
  Assert.StartsWith(label + " █", stripped, System.StringComparison.Ordinal)
