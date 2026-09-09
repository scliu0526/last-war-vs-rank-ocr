# RankLens

RankLens is an unofficial, offline Windows desktop application for reviewing ranking screenshots and exporting confirmed data to Excel.

> This project is not affiliated with, endorsed by, or sponsored by the developer or publisher of Last War: Survival Game.

## Status

The project is under active implementation. The current baseline includes the .NET 10 WPF shell, fixed-candidate workflow, Open XML workbook creation/update, image discovery, review controls, and cancellable batch-processing boundaries. Runtime OCR still requires a locally installed, SHA-256-pinned PP-OCRv5 ONNX bundle. Before that bundle is installed, the review flow supports deterministic offline preview through a same-name `.txt` sidecar next to each image (`rank<TAB>commander<TAB>alliance<TAB>score`).

See [README.zh-TW.md](README.zh-TW.md) for Traditional Chinese documentation.

## License

Source code is released under the MIT License. See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
