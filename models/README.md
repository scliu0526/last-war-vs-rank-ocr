# OCR model directory

This directory intentionally contains no model binaries. RankLens loads a model only when `manifest.json` has a verified license statement, fixed SHA-256 values, and matching local files.

To install a model bundle, obtain PP-OCRv5 detection, recognition, and dictionary files from a source whose terms permit local redistribution/use. Run `scripts/install-ocr-models.ps1` with the source URLs and SHA-256 values, then update `manifest.json` with the exact license notice and hashes. Do not commit binaries or unverifiable model metadata.

The application refuses pending hashes, path traversal, missing files, and hash mismatches. CPU is the default execution mode. DirectML is enabled only after the same verified model bundle is present.
