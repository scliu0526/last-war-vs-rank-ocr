# RankLens（排名截圖辨識器）

RankLens 是非官方、離線執行的 Windows 桌面應用程式，用於校對排名截圖並將確認後的資料匯出至 Excel。

> 本專案與 Last War: Survival Game 的開發商或發行商沒有隸屬、認可或贊助關係。

## 目前狀態

專案仍在開發中。目前已具備 .NET 10 WPF 基線、固定候選 workflow、Open XML 活頁簿建立／更新、圖片發現與可取消批次處理邊界。OCR 模型、完整校對介面、GPU 執行、Log 與正式發佈套件仍在實作。

英文主要文件：[README.md](README.md)。
目前專案仍在持續實作中。桌面流程已包含 .NET 10 WPF、圖片選取／批次取消、候選列勾選、Excel 建立／安全更新，以及 CPU／DirectML 設定。離線 OCR 會優先使用 Windows 已安裝的 OCR 語言套件；若尚未安裝，可在圖片旁放同名 `.txt` 以預覽流程。PP-OCRv5 ONNX 模型仍須先放入 `models/` 並填入固定 SHA-256，未鎖定雜湊的模型會被拒絕載入。
