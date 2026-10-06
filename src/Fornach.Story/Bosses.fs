namespace Fornach.Story

module StoryBosses =
  let createGuiltAspect () = Fornach.Domain.StoryBosses.createGuiltAspect ()
  let createDenialAspect () = Fornach.Domain.StoryBosses.createDenialAspect ()
  let createAngerAspect () = Fornach.Domain.StoryBosses.createAngerAspect ()
  let createBargainingAspect () = Fornach.Domain.StoryBosses.createBargainingAspect ()
  let createDepressionAspect () = Fornach.Domain.StoryBosses.createDepressionAspect ()
  let createAcceptanceAspect () = Fornach.Domain.StoryBosses.createAcceptanceAspect ()
  let createEnemy (enemyId: string) = Fornach.Domain.StoryBosses.createEnemy enemyId
  let createProloguePlayer (className: string) = Fornach.Domain.StoryBosses.createProloguePlayer className
