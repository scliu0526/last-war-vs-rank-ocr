# Third-Party Notices

This notice covers the package versions resolved by the current `project.assets.json` files. Runtime dependencies are redistributed only when they are included by the self-contained publish output. Test-only packages are used for development and are not part of the application release.

## Runtime dependencies

| Package | Resolved version | License / notice |
| --- | ---: | --- |
| DocumentFormat.OpenXml | 3.3.0 | MIT; package notice and source: <https://github.com/dotnet/Open-XML-SDK> |
| DocumentFormat.OpenXml.Framework | 3.3.0 | MIT; package notice and source: <https://github.com/dotnet/Open-XML-SDK> |
| Microsoft.ML.OnnxRuntime | 1.24.1 | MIT; package `LICENSE` and `ThirdPartyNotices.txt`; source: <https://github.com/microsoft/onnxruntime> |
| Microsoft.ML.OnnxRuntime.Managed | 1.24.1 | MIT; package `LICENSE.txt` and `ThirdPartyNotices.txt`; source: <https://github.com/microsoft/onnxruntime> |
| Microsoft.ML.OnnxRuntime.DirectML | 1.24.1 | MIT for the ONNX Runtime component; retain the package `LICENSE` and `ThirdPartyNotices.txt` |
| Microsoft.AI.DirectML | 1.15.4 | Microsoft DirectML Software License Terms; retain the package `LICENSE.txt`, `LICENSE-CODE.txt`, and `ThirdPartyNotices.txt`; source: <https://aka.ms/DirectML> |
| System.Management | 9.0.9 | MIT; package `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`; source: <https://dot.net/> |
| System.Numerics.Tensors | 9.0.0 | MIT; package `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`; source: <https://dot.net/> |

## Development and test dependencies

| Package | Resolved version | License / notice |
| --- | ---: | --- |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| Microsoft.TestPlatform.ObjectModel | 17.14.1 | MIT |
| Microsoft.TestPlatform.TestHost | 17.14.1 | MIT; retain package `ThirdPartyNotices.txt` |
| Microsoft.CodeCoverage | 17.14.1 | MIT; retain package `ThirdPartyNotices.txt` |
| coverlet.collector | 6.0.4 | MIT |
| xunit, xunit.assert, xunit.core, xunit.extensibility.core, xunit.extensibility.execution | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 |
| xunit.abstractions | 2.0.3 | Apache-2.0; package points to the xUnit license |
| xunit.analyzers | 1.18.0 | Apache-2.0 |
| Newtonsoft.Json | 13.0.3 | MIT; package `LICENSE.md`; source: <https://github.com/JamesNK/Newtonsoft.Json> |
| System.CodeDom | 9.0.9 | MIT; package `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` |
| System.IO.Packaging | 8.0.1 | MIT; package `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` |

The package archives contain the full license and third-party notice text. A release process must preserve those files in the published output and re-check the resolved package graph when versions change.

## OCR models

The release ZIP may include the following official PaddlePaddle ONNX models. Model binaries remain outside Git history; the URLs, revisions, and SHA-256 values below identify the exact release inputs.

| Model | Source / revision | SHA-256 | License / notice |
| --- | --- | --- | --- |
| PP-OCRv5 server detection ONNX | [PaddlePaddle/PP-OCRv5_server_det_onnx](https://huggingface.co/PaddlePaddle/PP-OCRv5_server_det_onnx), `dcf248c` | `10803475A591F7DC623E24670FB5752EC94D39A1F8CF069AAC1B6F0CE19CFC85` | Apache-2.0; retain the model card and PaddleOCR notices |
| PP-OCRv5 server recognition ONNX | [PaddlePaddle/PP-OCRv5_server_rec_onnx](https://huggingface.co/PaddlePaddle/PP-OCRv5_server_rec_onnx), `b70df21` | `D9DC333C9C7B042C6DFFB8E33D72B6F65C9C1D463D0A3C2F78174FEA55E94752` | Apache-2.0; retain the model card and PaddleOCR notices |
| PP-OCR character dictionary | [PaddleOCR `ppocr_keys_v1.txt`](https://github.com/PaddlePaddle/PaddleOCR/blob/main/ppocr/utils/ppocr_keys_v1.txt), `main` at acquisition | `A1C84D9BDB9AB29043C58896224D32941783EB821629618416DCB08F12886492` | PaddleOCR repository license and notices apply |

The current bundle is validated for the PP-OCRv5 server model's documented Chinese, Traditional Chinese, English, and Japanese scope. Korean and Thai require their documented language-specific model variants and separate accuracy validation before that broader claim is made.
