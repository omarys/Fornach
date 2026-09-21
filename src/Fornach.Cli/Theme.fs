namespace Fornach.Cli

open System
open Spectre.Console

/// Centralized Dracula color palette specification for Spectre.Console UI
module Theme =
  // Core Dracula Theme Hex Constants
  [<Literal>]
  let Background = "#282a36"
  [<Literal>]
  let CurrentLine = "#44475a"
  [<Literal>]
  let Foreground = "#f8f8f2"
  [<Literal>]
  let Comment = "#6272a4"
  [<Literal>]
  let Cyan = "#8be9fd"
  [<Literal>]
  let Green = "#50fa7b"
  [<Literal>]
  let Orange = "#ffb86c"
  [<Literal>]
  let Pink = "#ff79c6"
  [<Literal>]
  let Purple = "#bd93f9"
  [<Literal>]
  let Red = "#ff5555"
  [<Literal>]
  let Yellow = "#f1fa8c"

  // Strongly-typed Spectre.Console Color instances
  let ColorBackground = Color(0x28uy, 0x2auy, 0x36uy)
  let ColorCurrentLine = Color(0x44uy, 0x47uy, 0x5auy)
  let ColorForeground = Color(0xf8uy, 0xf8uy, 0xf2uy)
  let ColorComment = Color(0x62uy, 0x72uy, 0xa4uy)
  let ColorCyan = Color(0x8buy, 0xe9uy, 0xfduy)
  let ColorGreen = Color(0x50uy, 0xfauy, 0x7buy)
  let ColorOrange = Color(0xffuy, 0xb8uy, 0x6cuy)
  let ColorPink = Color(0xffuy, 0x79uy, 0xc6uy)
  let ColorPurple = Color(0xbduy, 0x93uy, 0xf9uy)
  let ColorRed = Color(0xffuy, 0x55uy, 0x55uy)
  let ColorYellow = Color(0xf1uy, 0xfauy, 0x8cuy)

  // Strongly-typed Spectre.Console Style instances
  let StyleCurrentLine = Style(foreground = Nullable ColorCurrentLine)
  let StylePurple = Style(foreground = Nullable ColorPurple)
  let StyleGreen = Style(foreground = Nullable ColorGreen)
  let StyleRed = Style(foreground = Nullable ColorRed)
  let StyleComment = Style(foreground = Nullable ColorComment)
  let StyleYellow = Style(foreground = Nullable ColorYellow)
