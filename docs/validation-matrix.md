# RankLens validation matrix

This matrix records implementation evidence separately from environment-dependent acceptance evidence. A checked implementation item does not imply that real OCR, GPU, or release-package validation is complete.

| Issue | Implementation evidence | Reproducible local evidence | Remaining acceptance boundary |
| --- | --- | --- | --- |
| #4 | Workbook creation, selective update, backup rotation, lock-safe replacement | `dotnet test RankLens.slnx --configuration Release` | No known local code gap; real Excel lock/reopen session remains unperformed; issue is closed |
| #5 | JPG/JPEG/PNG selection, folder discovery, 100-image limit, cancellation and progress | Same test command; batch tests | No known local code gap; real 100-image operator run remains unperformed; issue is closed |
| #6 | ONNX Runtime CPU pipeline scaffolding, model manifest/hash/licence gates, parser and sidecar fallback | Unit tests and fake-runtime OCR integration tests | Real pinned PP-OCRv5 binaries, legal provenance, CPU OCR, and private screenshot ground truth |
| #7 | Category/parser scaffolding, portrait validation, row bounds and source geometry | Parser, validator, and synthetic image tests | Real six-sample classification, complete-row filtering, rotation/crop/拼接/landscape rejection, and Japanese/Thai OCR samples |
| #8 | Candidate grid, selection policy, conflict controls, category correction, source switching and zoom | WPF smoke/source-preview/date/conflict tests plus domain policy tests | Direct WPF DataGrid edit/check-box/category/no-alliance binding, write guard, and close-dialog Yes/No interaction coverage |
| #9 | Duplicate merge, source retention, conflict and name-collision domain resolution | Domain/workflow tests, including WPF keep/ignore conflict actions | Real WPF MoveRank/KeepBoth/Ignore name-collision interaction and full real-image session |
| #10 | CPU default, DirectML adapter enumeration, unavailable-adapter explanations and no silent fallback | Adapter/catalog unit coverage and startup logic | WMI enumeration is heuristic; RTX 4050 DirectML OCR, Intel status, and CPU/GPU output parity remain unverified |
| #11 | Persisted settings, safe CPU fallback, privacy-safe structured logs and retention limits | Settings and log retention tests | No known local code gap; full WPF settings interaction and real-machine log review remain unperformed |
| #12 | MIT/licence notices, bilingual docs, model provenance gates, ZIP verifier, image exclusion and Windows CI | `pwsh ./scripts/test-release-security.ps1` | Verified model-containing self-contained ZIP and clean-machine offline launch |
| #13 | Workflow, batch, workbook, security-gate and CI automation | Release build, 46 tests, security script, Windows CI definition | Real Windows 11 CPU/GPU/model run; Windows 10, Japanese and Thai remain unverified |

## Current local commands

```powershell
dotnet test RankLens.slnx --no-restore -p:NuGetAudit=false --configuration Release --verbosity:minimal
dotnet build RankLens.slnx --no-restore -p:NuGetAudit=false --configuration Release
pwsh ./scripts/test-release-security.ps1
```

The current manifest intentionally contains pending model provenance and hashes. Do not run the formal publisher until those values are replaced with verified, redistributable model metadata.
