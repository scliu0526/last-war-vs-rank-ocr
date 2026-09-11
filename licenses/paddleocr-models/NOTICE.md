# PaddleOCR model attribution

RankLens may redistribute the following official PaddlePaddle PP-OCRv5 ONNX model files in the release ZIP:

- `PP-OCRv5_det.onnx` — [PP-OCRv5 server detection model](https://huggingface.co/PaddlePaddle/PP-OCRv5_server_det_onnx)
- `PP-OCRv5_rec.onnx` — [PP-OCRv5 server recognition model](https://huggingface.co/PaddlePaddle/PP-OCRv5_server_rec_onnx)
- `korean_PP-OCRv5_rec.onnx` and `korean_PP-OCRv5_rec.yml` — [Korean PP-OCRv5 mobile recognition model](https://huggingface.co/PaddlePaddle/korean_PP-OCRv5_mobile_rec_onnx)
- `th_PP-OCRv5_rec.onnx` and `th_PP-OCRv5_rec.yml` — [Thai PP-OCRv5 mobile recognition model](https://huggingface.co/PaddlePaddle/th_PP-OCRv5_mobile_rec_onnx)

The model repositories identify the model license as Apache-2.0. Character dictionaries come from each recognition model repository's `inference.yml` at the pinned revisions recorded in `models/manifest.json`.

`LICENSE.txt` in this directory is the Apache License 2.0 text copied from the official PaddleOCR repository. This notice records the model provenance and is distributed with that license text.
