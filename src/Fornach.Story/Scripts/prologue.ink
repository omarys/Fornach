EXTERNAL start_combat(enemy_id)
EXTERNAL award_memory(memory_id)
EXTERNAL check_preparation(prep_name)
EXTERNAL choose_class(class_name)

VAR checkpoint = "waking"

-> waking

=== waking ===
The roar of screaming metal and the blinding flash of white light violently recede, leaving only the sound of cold, rhythmic rain drumming against wet shale.
You peel your face out of the mud. Your head throbs with blinding static; your past is an empty, dark vault.
Beside you in the muck lies a battered chest, its iron lock already broken and hanging loose.
+ [Draw the two-handed greatsword (Berserker)]
    You lift the massive greatsword from the chest. Its weight demands ferocious, unyielding momentum—the instinct of a Berserker.
    Inside the lid, you also recover a bundle of sharpened caltrops wrapped in oiled cloth.
    ~ choose_class("Berserker")
    ~ award_memory("prepared_mind")
    -> boy_encounter
+ [Take the paired stiletto and rapier (Duelist)]
    You draw the twin blades from their rain-soaked sheaths. Light, sharp, balanced for rapid fencing cadences—the instinct of a Duelist.
    Inside the lid, you also recover a bundle of sharpened caltrops wrapped in oiled cloth.
    ~ choose_class("Duelist")
    ~ award_memory("prepared_mind")
    -> boy_encounter
+ [Equip the arming sword and reinforced shield (Warden)]
    You strap the notched arming sword to your side and brace the iron-rimmed shield. Grounded, unyielding, fortified against any blow—the instinct of a Warden.
    Inside the lid, you also recover a bundle of sharpened caltrops wrapped in oiled cloth.
    ~ choose_class("Warden")
    ~ award_memory("prepared_mind")
    -> boy_encounter
+ [Reach for the carved ash staff (Inquisitor)]
    Your fingers curl around the polished runic wood. A surge of arcane static ripples through your thoughts, igniting mental resonance—the instinct of an Inquisitor.
    Inside the lid, you also recover a bundle of sharpened caltrops wrapped in oiled cloth.
    ~ choose_class("Inquisitor")
    ~ award_memory("prepared_mind")
    -> boy_encounter

=== boy_encounter ===
A sharp, ragged sob cuts through the downpour.
Curled under a leaning slab of quarry slate sits a young boy, shivering violently in soaked homespun. His tear-streaked face mirrors a half-remembered visage from your forgotten past—a pang of agonizing familiarity hits your chest.
~ award_memory("rain_and_headlights")
"M-mister... please," the boy stammers, teeth chattering. "They took Lyra... my twin sister. The quarry deserters dragged her toward the iron pit. They said she was a tithe!"
+ ["Stay hidden here. I will bring her back."]
    The boy looks up with wide, desperate eyes. "Be careful... their leader carries a hammer from the mine forge. He doesn't speak. He just crushes everything."
    -> quarry_ambush
+ ["Why were you out in this storm?"]
    "We were running... following the headlights... wait, what are headlights?" The boy clutches his head in confusion, then cries out. "Hurry, please! I can hear the hammer strikes!"
    -> quarry_ambush

=== quarry_ambush ===
You ascend the muddy incline into the amphitheater of the abandoned iron quarry.
Steam rises from rainwater sizzling against red-hot slag. At the center of the pit, towering over an iron cage, stands a hulking brute in rusted armor.
He raises an enormous quarry sledgehammer with methodical, unhurried malice.
As he turns toward you, his helmet reveals no face—only an abyss of swirling, suffocating shadows, and the suffocating weight of an oath you failed to keep.
The air grows heavy with stifling guilt.
~ start_combat("Guilt_Aspect")
-> post_combat

=== post_combat ===
The shadowy colossus shatters into jagged crystalline fragments like a broken windshield.
The crushing weight on your chest momentarily lifts.
In the puddle at your feet, the reflection isn't a quarry—for a brief second, you see the reflection of a crumpled sedan, a shattered crosswalk signal, and a girl's hand slipping from your grasp.
~ award_memory("shattered_windshield")
The cage door groans open, but Lyra is gone—leaving only a trail of muddy footsteps leading deeper into the dark, silent forest.
"This wasn't an accident," you whisper into the rain. "I have to find her."
+ [Follow Lyra's tracks into the dark forest]
    ~ checkpoint = "forest"
    -> forest_entry

=== forest_entry ===
The dense canopy of the Shrouded Grove swallows the rain into an eerie, suffocating hush.
Grey mist winds like smoke through the towering pines. Whispering voices drift from the trees: "It didn't happen. It was just a nightmare. She is waiting at home right now."
Flickering illusions of a warm living room, an untouched dinner table, and an intact car pulse in the gloom, mocking your senses.
+ [Steel your mind against the illusions and push forward]
    -> denial_ambush
+ [Search the damp needle bed for tracks]
    You find a discarded yellow raincoat torn by tree roots. A scrap of paper reads: 'Don't look at the headlights.'
    -> denial_ambush

=== denial_ambush ===
The shifting mist condenses into two ethereal figures wielding translucent, flickering daggers.
"Why bleed for someone who isn't even gone?" their voices chorus from every direction. "Just close your eyes. If you don't acknowledge the truth, it never happened."
~ start_combat("Denial_Aspect")
-> post_denial

=== post_denial ===
The phantom daggers shatter into harmless glass droplets that vanish in the needles.
The thick fog recedes, revealing a crumpled yellow umbrella crushed beneath tire treads.
~ award_memory("broken_umbrella")
A sudden violent tremor shakes the earth. Ahead, the trees wither into scorched stumps as the path opens into a blazing volcanic caldera.
+ [Ascend into the scorching heat of the Basalt Caldera]
    ~ checkpoint = "caldera"
    -> caldera_ascent

=== caldera_ascent ===
Choking sulfur stings your throat. Waves of blistering heat radiate from cracked plates of black basalt.
Beneath the roar of subterranean lava runs an unmistakable phantom sound: the furious roar of an accelerating engine, metal violently straining against torque.
A burning wave of destructive rage floods your veins: 'Whose fault was this? The driver? The storm? Why didn't anyone stop it?!'
+ [Channel the blistering fury and advance]
    -> anger_ambush
+ [Look for a path through the cooling slag]
    There is no detour. The fire demands to be faced head-on.
    -> anger_ambush

=== anger_ambush ===
A towering juggernaut forged of black volcanic basalt and incandescent magma emerges from the rift.
It swings an enormous greatsword of red-hot slag, showers of molten embers erupting with every vicious arc.
It unleashes a deafening, wordless roar of unbridled fury that shatters the basalt pillars around you.
~ start_combat("Anger_Aspect")
-> post_anger

=== post_anger ===
The blazing behemoth fractures down its core, extinguishing into cold, brittle charcoal.
The blinding rage in your chest burns out, leaving behind a cold, hollow void.
You remember: the slammed door, the bitter screaming match in the hallway, the awful words you hurled into the room just minutes before she ran outside into the storm.
~ award_memory("shouting_in_the_hallway")
A freezing gale sweeps across the rim, blowing away the soot and revealing a narrow coastal ridge.
+ [Follow the freezing sea wind toward the cliffs]
    ~ checkpoint = "promontory"
    -> promontory_ascent

=== promontory_ascent ===
You step out onto the razor edge of the Shattered Promontory.
A tempestuous black ocean roars hundreds of feet below, crashing against jagged sea needles.
Sideways rain pelts you like buckshot. A chilling whisper rides the gale: "Everything has a price. You can have her back. Give me your eyes. Give me your hands. Give me your remaining years."
+ ["Take whatever it costs! Just bring her back!"]
    -> bargaining_ambush
+ ["No more desperate trades. Step aside."]
    -> bargaining_ambush

=== bargaining_ambush ===
A tall, hooded figure cloaked in sodden oilskin hovers over the cliff's edge.
In one pale hand, it holds an ornate pair of brass scales that creak in the gale; in the other, a whistling rapier.
"Let us negotiate the currency of regret," the Arbiter whispers as lightning tears the sky.
~ start_combat("Bargaining_Aspect")
-> post_bargaining

=== post_bargaining ===
The brass scales snap at the fulcrum and plummet into the abyss.
The cold, devastating truth settles into your bones: Death does not barter.
The memory surges forth with excruciating clarity: the sterile hum of the intensive care unit, the rhythmic beep of heart monitors, and your whispered, desperate bargains made to the cold ceiling in the dark.
~ award_memory("hospital_monitors")
Across the bay, the tide retreats to expose the submerged ruins of a flooded metropolis.
+ [Descend into the sunken city]
    ~ checkpoint = "sunken"
    -> sunken_descent

=== sunken_descent ===
You wade knee-deep into stagnant floodwaters winding through crumbling concrete and dead stone facades.
A terrifying, absolute silence blankets the ruins. Your limbs feel heavy as lead, each step through the murky water requiring monumental effort.
Apathy presses down like the ocean itself. What was the point of surviving? Why fight? Nothing can change what happened.
+ [Force your leaden legs through the dead water]
    -> depression_ambush
+ [Rest against a damp stone column]
    The water laps higher, cold and numbing. You pull yourself upright through sheer stubborn momentum.
    -> depression_ambush

=== depression_ambush ===
Rising slowly from the black water is a hunched, weeping colossus draped in soaked burial shrouds, dragging an immense iron anchor.
It speaks no words and makes no sound. Its hollow, sorrowful eyes fix upon you, casting an aura of crushing, inescapable fatigue over your spirit.
~ start_combat("Depression_Aspect")
-> post_depression

=== post_depression ===
The sodden burial shroud drifts away on the receding tide; the heavy anchor dissolves into fine silt.
The leaden weight that paralyzed your lungs begins to dissolve.
You remember: returning home alone to an empty house. Walking past her unopened bedroom door. The deafening silence of a life unlived. And finally, allowing yourself to weep.
~ award_memory("empty_bedroom")
For the first time since you awoke, the storm clouds part. Warm, golden sunlight illuminates a radiant hillside carpeted in white blossoms.
+ [Ascend into the sunlit meadow]
    ~ checkpoint = "meadow"
    -> meadow_arrival

=== meadow_arrival ===
The rain has ceased entirely. The sky above is a vast, calm canopy of sapphire blue.
You walk into an endless meadow of white lilies swaying gently in the summer breeze.
Standing in the center of the flowers is a serene figure dressed in unblemished white linen, leaning upon a smooth birch staff.
As you approach, the guardian turns. It is your own face, unmarred by sorrow, standing poised and peaceful.
+ ["Who are you?"]
    "I am the part of you that is ready to let her go," the reflection answers with a gentle smile.
    -> acceptance_ambush
+ ["Is Lyra here?"]
    "Lyra has been at peace all along. She has only been waiting for you to stop punishing yourself."
    -> acceptance_ambush

=== acceptance_ambush ===
The reflection offers a respectful nod, raising the birch walking staff.
"One final encounter. Not to conquer in anger, but to find peace."
~ start_combat("Acceptance_Aspect")
-> post_acceptance

=== post_acceptance ===
You lower your sword into the white blossoms.
The reflection steps forward, clasping your shoulder with warm, reassuring familiarity.
The final memory opens like morning light: Lyra's bright, unclouded smile on a sunny afternoon, her laughter ringing clear, telling you that everything will be alright.
~ award_memory("twin_smile")
-> intersection_transition

=== intersection_transition ===
The radiant meadow of white lilies begins to shimmer and dissolve like mist.
The sweet fragrance of blossoms fades into the sharp smell of gasoline, exhaust fumes, and rain falling on warm asphalt.
The gentle breeze turns into the rhythmic thrum and low idle of dozens of automobile engines.
You blink against the sudden harshness. You are standing in the center of a pedestrian crosswalk under a drizzling evening sky.
To your left and to your right, endless lines of gridlocked cars sit bumper-to-bumper with headlights blazing, forming an impenetrable wall of metal and halogen.
There is nowhere to turn; neither curb offers an escape.
Directly ahead across the white crosswalk lines, rising from the pavement where the collision once occurred, stands an immense, ancient monolith of dark stone piercing the clouds: **The Tower**.
Its monumental bronze doors stand slightly ajar, warm torchlight spilling out across the wet asphalt.
+ [Cross the street and push through the bronze doors of The Tower]
    -> enter_tower

=== enter_tower ===
You take a deep, calm breath and step across the final painted line of the crosswalk.
You place your hands against the colossal bronze gates. They swing inward with a deep, echoing chime.
Behind you, the street, the headlights, and the cars vanish into the mist.
Ahead lie endless vaulted stone halls, uncharted chambers, and the boundless ascent of The Tower.
-> END

=== game_over_respawn ===
The fatal blow lands with the deafening force of an oncoming truck.
Vision whites out. A phantom horn wails across the void.
The screech of tires echoes in your skull, tearing through time and space...
{
    - checkpoint == "forest":
        -> forest_entry
    - checkpoint == "caldera":
        -> caldera_ascent
    - checkpoint == "promontory":
        -> promontory_ascent
    - checkpoint == "sunken":
        -> sunken_descent
    - checkpoint == "meadow":
        -> meadow_arrival
    - else:
        -> waking
}
