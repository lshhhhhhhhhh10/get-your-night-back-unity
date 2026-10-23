# Course site

The deployed directory is `site/`. `data/bundle.js` contains the generated source files, flow, parameter table, challenge baseline and DEVLOG. Mermaid is bundled in `mermaid.min.js`; fonts fall back to system fonts. No runtime CDN is needed.

## Update

1. Maintain gameplay headers and genuine gameplay interactions in the repository root.
2. In the editor, choose `CGDD → Export Trace Docs`.
3. Open the site with `CGDD → Open Student Site`, or serve this directory locally.
4. Push changes to `main`; `.github/workflows/pages.yml` deploys this directory when GitHub Pages is set to GitHub Actions.

The graph uses source-file IDs (dots become underscores), so partial files do not overwrite one another. Header arrows are primary documented dependencies, not automatic exhaustive call analysis. Equal min/max parameter ranges are fixed documented baselines.

## Student recording — published and verified

The student supplied `cgdd 1023.mov` on 2026-10-23. The complete 155.6-second recording is included at `media/play.mp4` and enabled by `recording.js`. The original file has no audio stream; the site labels the recording as silent. The agent converted the supplied recording and did not generate new gameplay footage.

The web copy is H.264 / yuv420p, 1920 × 1248 at 30 fps, 22,840,721 bytes, with fast-start metadata. The player preserves the whole frame and exposes native playback controls. Its poster is a frame from the same recording. Full-file decode and metadata checks passed during preparation. On 2026-10-23 the live file returned HTTP 200 and matched the local SHA-256. Chrome playback, pause and keyboard seeking were verified; the original recording has no audio. Fullscreen was not confirmed. See `../docs/publication-validation.json`.

To replace the recording later, update `media/play.mp4`; alternatively set an existing YouTube video's 11-character ID in `recording.js`. Keep the bilingual description accurate about the recording's duration and audio. Validation details are in `../docs/recording-validation.json`.

## Historical evidence

`evidence.html` is a retrospective index of the earlier project records. The original two DEVLOG samples remain unchanged. The agent has not fabricated a student reflection or backdated a CGDD Interaction.
