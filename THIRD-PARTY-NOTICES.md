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

No OCR model binaries are currently committed or distributed from this source baseline. PP-OCRv5 model names, sources, checksums, model-specific licenses, and redistribution notices must be recorded after independent verification before an offline release is published.
