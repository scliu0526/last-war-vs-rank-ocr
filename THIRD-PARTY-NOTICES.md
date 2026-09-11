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
| System.Numerics.Tensors | 9.0.0 | MIT; package `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`; source: <https://dot.net/> |
| Vortice.DXGI | 3.8.3 | MIT; source: <https://github.com/amerkoleci/Vortice.Windows> |
| Vortice.DirectX | 3.8.3 | MIT; source: <https://github.com/amerkoleci/Vortice.Windows> |
| Vortice.Mathematics | 2.1.0 | MIT; source: <https://github.com/amerkoleci/Vortice.Mathematics> |
| SharpGen.Runtime | 2.4.2-beta | MIT; source: <https://github.com/SharpGenTools/SharpGenTools> |
| SharpGen.Runtime.COM | 2.4.2-beta | MIT; source: <https://github.com/SharpGenTools/SharpGenTools> |

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
| PP-OCRv5 server detection ONNX | [PaddlePaddle/PP-OCRv5_server_det_onnx](https://huggingface.co/PaddlePaddle/PP-OCRv5_server_det_onnx), `dcf248c4dcc064c6d030db241255f9e8fbc6733a` | `10803475A591F7DC623E24670FB5752EC94D39A1F8CF069AAC1B6F0CE19CFC85` | Apache-2.0; full license and attribution ship in `licenses/paddleocr-models` |
| PP-OCRv5 server recognition ONNX | [PaddlePaddle/PP-OCRv5_server_rec_onnx](https://huggingface.co/PaddlePaddle/PP-OCRv5_server_rec_onnx), `b70df217f4fd99d14f970bad092cebe7d74cc4d1` | `D9DC333C9C7B042C6DFFB8E33D72B6F65C9C1D463D0A3C2F78174FEA55E94752` | Apache-2.0; full license and attribution ship in `licenses/paddleocr-models` |
| PP-OCRv5 server character dictionary | [PaddleOCR `ppocrv5_dict.txt`](https://github.com/PaddlePaddle/PaddleOCR/blob/2661c7c0ef5c613e8f93c6e93b2e052399f0f854/ppocr/utils/dict/ppocrv5_dict.txt), `2661c7c0ef5c613e8f93c6e93b2e052399f0f854` | `D1979E9F794C464C0D2E0B70A7FE14DD978E9DC644C0E71F14158CDF8342AF1B` | Apache-2.0; full license and attribution ship in `licenses/paddleocr-models` |
| Korean PP-OCRv5 mobile recognition ONNX | [PaddlePaddle/korean_PP-OCRv5_mobile_rec_onnx](https://huggingface.co/PaddlePaddle/korean_PP-OCRv5_mobile_rec_onnx), `5c6f574b8e2230adf4287b33e736d71b9fabd28e` | `92F0B7785E64FC9090106A241CF4C1EB97472824558272751B88A2A4476D3A08` | Apache-2.0; retain the model card and PaddleOCR notices |
| Korean PP-OCRv5 character dictionary YAML | [Pinned `inference.yml`](https://huggingface.co/PaddlePaddle/korean_PP-OCRv5_mobile_rec_onnx/resolve/5c6f574b8e2230adf4287b33e736d71b9fabd28e/inference.yml) | `F757FA1C40E99EDCF27E9CCE879B93EB2A51FA46F5EF39095689B8C37DD75998` | Apache-2.0 model card and PaddleOCR notices apply |
| Thai PP-OCRv5 mobile recognition ONNX | [PaddlePaddle/th_PP-OCRv5_mobile_rec_onnx](https://huggingface.co/PaddlePaddle/th_PP-OCRv5_mobile_rec_onnx), `1d4adbbafb1034a2fd6618498575b81ea7b69f69` | `27618BE66018F8598AC0A526A593F9F1CEBF794E7EDED93428E8FB016E537F5F` | Apache-2.0; retain the model card and PaddleOCR notices |
| Thai PP-OCRv5 character dictionary YAML | [Pinned `inference.yml`](https://huggingface.co/PaddlePaddle/th_PP-OCRv5_mobile_rec_onnx/resolve/1d4adbbafb1034a2fd6618498575b81ea7b69f69/inference.yml) | `F6BA7FEFC38CA1FF398DDAFA75D67D16E0B3757C4E6C833ADFFEE98A981766C9` | Apache-2.0 model card and PaddleOCR notices apply |

The bundle includes the official Korean and Thai language-specific variants. Korean has partial real-screenshot evidence; broader Korean coverage and Thai screenshots still require separate accuracy validation.
