# Get Your Night Back

A quiet trip to retrieve your phone — and get back to bed unnoticed.

This repository contains the native Tuanjie game and its CGDD coursework site. The gameplay baseline is the 2026-10-19 environment-instruction version; the 2026-10-22 work connects source annotations and genuine development evidence to the course site.

**Course delivery (2026-10-23):** this repository contains the complete native source project, populated CGDD site and student-provided recording. The temporary split-archive transport is retired; clone/download this repository directly. The [live course site](https://lshhhhhhhhhh10.github.io/get-your-night-back-unity/) is deployed and verified: 30 script nodes, seven parameters, source reading, bilingual navigation and the student recording. Deployment and verification details are recorded in [status](STATUS.md). The teacher invitation is pending acceptance; no course-platform submission is performed by the agent.

## Open the game

1. Use **Tuanjie 1.6.13 (2022.3.61t14)**, matching `ProjectSettings/ProjectVersion.txt`.
2. Add this repository folder in Hub and open it. Let asset import finish.
3. Open `Assets/NightBackPort/Scenes/GetYourNightBack.scene`, then press Play.

The older `Night3Whitebox` and `CourseCapture` scenes are retained for history. The saved Build Settings still refer to the old whitebox; select the full-game scene before making a new game build. No build was produced as part of the documentation integration.

`Assets`, `Packages` and `ProjectSettings` are included. Generated caches, personal saves, old app builds and local AI-editor packages are excluded. Optional machine-specific Codely packages were removed from the portable manifest; they are not used by the gameplay scripts. Original GUIDs and game logic are preserved.

## Coursework

- [Course site](https://lshhhhhhhhhh10.github.io/get-your-night-back-unity/)
- [Current status](STATUS.md)
- [Challenge baseline](docs/challenge-map.md)
- [Generated script relationships](docs/script-flow.mmd) and [parameters](docs/tunables.md)
- [Development record](DEVLOG.md) and [retrospective source evidence](docs/development-evidence.md)
- [Recording and publishing instructions](site/README.md)

For documentation-only work with Mono installed, `sh tools/export-docs-offline.sh` executes the same C# exporter with editor services stubbed. This does not import assets, build or test the game.

The graph represents source files, including partial files, with explicit primary dependencies. Use `CGDD → Export Trace Docs` after maintaining source headers; `CGDD → Open Student Site` opens the result. Do not use Force Overwrite on the populated documents.

The course site uses bundled Mermaid and system fonts, with no runtime CDN. The student's supplied recording is integrated at `site/media/play.mp4` as a 22.84 MB H.264 video; it has no audio, as noted on the page. See [recording instructions](site/README.md). No new gameplay footage or personal reflection was fabricated.

## Provenance

This is a clean submission copy of the existing native project, first prepared on 2026-10-22. It is not a claim that prior work happened in this new Git repository. Original development history is identified by dated Raw log numbers. Existing asset provenance and license files are retained beside their assets; public access does not grant additional rights to referenced or user-supplied artwork. [Original whitebox README](docs/whitebox-original-readme.md) is retained verbatim as a historical document; its old external delivery paths are not current entry points.
