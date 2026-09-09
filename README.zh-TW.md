# RankLens（排名截圖辨識器）

RankLens 是非官方、離線執行的 Windows 桌面應用程式，用於校對排名截圖並將確認後的資料匯出至 Excel。

> 本專案與 Last War: Survival Game 的開發商或發行商沒有隸屬、認可或贊助關係。

## 目前狀態

目前已具備 .NET 10 WPF 基線、圖片選取／批次取消、候選列勾選與編輯、Open XML 活頁簿建立／安全更新、影像前處理、文字偵測、裁切辨識、CTC 解碼與排名解析的本機 ONNX OCR 路徑、設定與隱私安全 Log。官方 PP-OCRv5 ONNX 模型已固定來源、授權與 SHA-256，並可由發布腳本打包；CPU 是預設模式，DirectML 可選擇相容顯示卡。實際截圖準確率、GPU 一致性、乾淨機離線啟動、Windows 10、韓文與泰文仍須依驗證矩陣另行確認。在沒有模型包時，仍可用同名 `.txt` 側錄預覽流程。

英文主要文件：[README.md](README.md)。

發佈腳本會產生 `RankLens-win-x64.zip.sha256`，解壓前請使用 `Get-FileHash -Algorithm SHA256 RankLens-win-x64.zip` 比對雜湊。ZIP 未經付費憑證簽章，Windows SmartScreen 可能顯示「未知的發行者」警告；本專案不宣稱程式已簽署。也可以執行 `scripts/verify-release.ps1 -Archive .\RankLens-win-x64.zip`，離線檢查雜湊、模型檔案及 manifest 的授權／雜湊狀態。

## 持續驗證

每次 push 與 pull request 都會在 Windows runner 執行 Release build、自動化測試及合成發佈安全閘門。此工作流程不代表真實 OCR 模型、私人截圖或 GPU 硬體已完成驗證。

要在本機執行相同的發佈安全閘門：

```powershell
pwsh ./scripts/test-release-security.ps1
```

各 Issue 的證據與尚未驗證邊界請參閱[驗證矩陣](docs/validation-matrix.md)。
