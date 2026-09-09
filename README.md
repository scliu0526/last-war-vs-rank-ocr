# RankLens

RankLens is an unofficial, offline Windows desktop application for reviewing ranking screenshots and exporting confirmed data to Excel.

> This project is not affiliated with, endorsed by, or sponsored by the developer or publisher of Last War: Survival Game.

## Status

The project is under active implementation. The current baseline includes the .NET 10 WPF shell, fixed-candidate workflow, Open XML workbook creation/update, image discovery, review controls, cancellable batch processing, and a local ONNX OCR path (image preprocessing, text detection, crop recognition, CTC decoding, and ranking parsing). Runtime OCR requires a locally installed, SHA-256-pinned PP-OCRv5 ONNX bundle. Before that bundle is installed, the review flow supports deterministic offline preview through a same-name `.txt` sidecar next to each image (`rank<TAB>commander<TAB>alliance<TAB>score`).

See [README.zh-TW.md](README.zh-TW.md) for Traditional Chinese documentation.

## Model and release preparation

The repository does not redistribute OCR binaries. After independently verifying a permitted model source and license, run `scripts/install-ocr-models.ps1` with the source URLs and SHA-256 values. The script writes only verified files to `models/`; `scripts/publish-win-x64.ps1` refuses unresolved hashes or mismatched files before creating the self-contained ZIP.

## Offline release verification

The publish script emits `RankLens-win-x64.zip.sha256`. Verify the archive before extracting it (for example, with `Get-FileHash -Algorithm SHA256 RankLens-win-x64.zip`) and compare the result with that file. The ZIP is unsigned; Windows SmartScreen may show an “unknown publisher” warning. The project does not claim that the executable is code-signed.

For an automated offline check after downloading a release, run `scripts/verify-release.ps1 -Archive .\RankLens-win-x64.zip`. It compares the adjacent `.sha256` file and confirms that the archive contains the model files and a manifest without pending license or hash values.

## License

Source code is released under the MIT License. See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Continuous validation

GitHub Actions runs on a Windows runner for every push and pull request. It executes the Release build, the automated test suite, and the synthetic release-security gate. The workflow does not claim that real OCR models, private screenshots, or GPU hardware have been validated.

To run the same release-security gate locally:

```powershell
pwsh ./scripts/test-release-security.ps1
```
