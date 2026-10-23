# DEVLOG

Only append. Do not edit old sections. Two samples stay so students can see the format.

Only changes to the game itself are logged: the verb, win and loss, pressure, and what the player can feel.
Site language, diagram styling, and click-to-read-script do not go into an Interaction or a Stage Reflection.

Each gameplay change should add a section `## Interaction NNN — title` (the Agent can append this when it changes the game).
Add `## Stage Reflection NNN — title` only when you ask for one: Keep / Change / Cut, and which AI suggestions you accepted or rejected. That reflection stays on the game.

## Interaction 000 — EXAMPLE (sample, do not delete)

- Date: —
- Who asked: teacher (sample)
- Request: Tighten one tunable and see if it is still the same game.
- What changed: Sample only. The current code was not changed. A real change would touch only one `@tune` field.
- Result: The player's verb did not change. Win and loss did not change. Pressure changed, not the skeleton.

## Stage Reflection 000 — EXAMPLE (sample, do not delete)

### Keep
- A core verb you can say in one sentence, and a clear win/loss.

### Change
- To make it harder, turn an existing `@tune` first. Do not add a new system yet.

### Cut
- No shop, no deep inventory, no optimization that is off-lesson.

### AI
- Accept: tune feel with existing fields; avoid opening new scripts.
- Reject: GraphView, object pools, long hand-written scene YAML. This class is about script relationships and tunable numbers.


## Archive reference — native game development before CGDD

Imported on 2026-10-22. This is a retrospective index of existing project records, not a newly performed gameplay Interaction and not a student reflection. The EXAMPLE sections above remain examples.

- **2026-10-02 · Raw 82 · Native Time Edition.** Three nights, the eleven-step practice, object retrieval and return-home win condition were ported to C#. A four-phase household routine was implemented within the student's request to design time-based play. The 90-second phase length is an AI implementation choice, not a separately confirmed student parameter.
- **2026-10-12 · Raw 84, 85, 87–88.** The bedroom became an object-based menu; solid pause/result boards, controller navigation and input-release fixes followed. Footstep/snoring cues moved to the hearing map; text sound captions were hidden during regular play.
- **2026-10-13 · Raw 89–94.** Household furniture, walls and title objects were refined. The solid title remains in the scene during the getting-out-of-bed camera move.
- **2026-10-19 · Raw 99–104, 106–107.** Story walls and nearby instructions were refined for reading, with cabinet hints on the floor, a pencil hint on the desk and mute instructions on the phone. The newest gameplay evidence is Raw 107.

Original dated records are retained in the student's main project at `development-log/agent-development-log.md`. The extracted native-stage summaries in the Development evidence page (source: `docs/development-evidence.md`) identify the original record numbers, requests, results and validation limits. Historical passes are not tests rerun on this submission copy.

No new game mechanic, numerical difficulty change, play recording or student reflection was created for the CGDD integration.
