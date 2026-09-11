# OCR model directory

Model binaries are ignored by Git and are supplied only to a local build or release ZIP. RankLens loads a model only when `manifest.json` has a verified license statement, fixed SHA-256 values, and matching local files.

The release bundle uses the SHA-256-pinned primary, Korean, and Thai PP-OCRv5 files declared in `manifest.json`. `scripts/install-ocr-models.ps1` can install a primary model set; language variants must also match the filenames, pinned sources, and hashes in the checked-in manifest before publishing. Do not commit model binaries or unverifiable model metadata.

The application refuses pending hashes, path traversal, missing files, and hash mismatches. CPU is the default execution mode. DirectML is enabled only after the same verified model bundle is present.
