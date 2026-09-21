namespace Fornach.Domain

type CollapseReason =
  | SomaticAnoxia // 100% Exhaustion
  | Exsanguination // 100% Overwhelm
  | StanceFailure // 100% Frustration
  | CatatonicStupor // 100% Cognitive Fatigue
  | ContradictionLock // 100% Confusion
  | HystericalMeltdown // 100% Provoke
  | RecklessExposure // 100% Recklessness

[<RequireQualifiedAccess>]
type CollapseState =
  | Stable
  | Collapsed of reason: CollapseReason

module CollapseState =
  let isCollapsed =
    function
    | CollapseState.Collapsed _ -> true
    | CollapseState.Stable -> false
