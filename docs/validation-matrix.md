# RankLens validation matrix

This matrix records implementation evidence separately from environment-dependent acceptance evidence. A checked implementation item does not imply that real OCR, GPU, or release-package validation is complete.

| Issue | Implementation evidence | Reproducible local evidence | Remaining acceptance boundary |
| --- | --- | --- | --- |
| #4 | Workbook creation, selective update, backup rotation, unknown-workbook rejection, invalid-target handling, lock-safe replacement/retry, and updated/skipped/failed summary | `dotnet test RankLens.slnx --configuration Release` | No known local code gap; real Excel lock/reopen session remains unperformed; issue is closed |
| #5 | JPG/JPEG/PNG selection, folder discovery, 100-image limit, cancellation, progress, and per-file path/reason details | Same test command; batch and failure-display tests | No known local code gap; real 100-image operator run remains unperformed; issue is closed |
| #6 | ONNX Runtime CPU pipeline, verified model manifest/hash/licence gates, PP-OCRv5 preprocessing/shape handling, original-resolution recognition crops, YAML dictionary normalization, Korean/Thai recognition variants, parser and sidecar fallback, rank-column retry, annotated ROI/column filtering, card-row grouping, multi-width numeral-band retry, punctuation-tolerant numeric parsing | Unit and fake-runtime tests plus local CPU smoke on one ignored private Monday screenshot: all complete visible ranks and scores were reproduced, and 5 of 7 commander names were exact | Some Korean glyphs and alliance punctuation/case remain imperfect and should stay reviewable; other screenshots and Thai ground truth are not yet validated |
| #7 | Category/parser scaffolding, supported aspect-ratio validation, empty-structure rejection, row bounds and source geometry; exact synthetic Japanese/Thai/Korean text preservation; installed models decode generated deidentified Japanese and Thai raster text exactly | Parser, validator, model-capability and synthetic multilingual raster tests | Real six-sample classification and per-row ground truth; real Japanese/Thai screenshots remain unavailable and unverified |
| #8 | Candidate grid, selection policy, conflict controls, category correction, source switching, zoom, and dirty-state restoration after post-write mutation | WPF tests cover date, edit, originally checked/unchecked confirmation, category propagation, no-alliance confirmation, source switching, conflict buttons and unsaved-state restoration; user confirmed unchecking an initially selected row works | Close-dialog Yes/No interaction and final user recheck of the originally-unchecked row on the rebuilt executable |
| #9 | Duplicate merge, source retention, conflict and name-collision domain resolution | Domain/workflow tests plus WPF keep, ignore and move-rank button paths | Full real-image conflict session remains unperformed |
| #10 | CPU default, DXGI-ordered adapter enumeration, DirectX 12 compatibility probing, unavailable-adapter explanations and no silent fallback | Current machine enumerated Intel Iris Xe as DXGI adapter 0 and RTX 4050 as adapter 1 and marked both compatible; Microsoft Basic Render Driver was disabled with a Traditional Chinese reason; one private full screenshot produced identical CPU and explicitly selected RTX 4050 category, rank, commander, alliance and score candidates | One real screenshot is not broad OCR parity; additional samples remain under #7 |
| #11 | Persisted settings, safe CPU fallback, privacy-safe structured logs and retention limits | Settings and log retention tests | No known local code gap; full WPF settings interaction and real-machine log review remain unperformed |
| #12 | MIT/licence notices, bilingual docs, immutable model URLs/hashes, bundled PaddleOCR Apache-2.0 text, ZIP verifier, image exclusion and Windows CI | Release build, model-containing ZIP, `verify-release.ps1`, `test-release-security.ps1`, and launch from a freshly extracted ZIP on the current Windows 11 machine | Clean-machine offline launch remains unperformed |
| #13 | Workflow, single-image-in-flight 100-image batch, workbook safety, WPF review paths, model capability, security gate and CI automation | Release build, 84 tests, WPF `App.OnStartup` test, security script, freshly extracted ZIP startup smoke, and current-machine CPU/Intel/RTX evidence | Clean-machine offline launch; Windows 10 and real Japanese/Thai screenshots remain unverified |

## Current local commands

```powershell
dotnet test RankLens.slnx --no-restore -p:NuGetAudit=false --configuration Release --verbosity:minimal
dotnet build RankLens.slnx --no-restore -p:NuGetAudit=false --configuration Release
pwsh ./scripts/test-release-security.ps1
```

The current manifest contains verified metadata for the official PP-OCRv5 server bundle and its Korean/Thai mobile recognition variants. Do not describe clean-machine, Windows 10, broad Korean, or real Japanese/Thai screenshot validation as complete until those environments and samples have been exercised.
