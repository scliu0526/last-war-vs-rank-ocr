# RankLens（排名截圖辨識器）

RankLens 是非官方、離線執行的 Windows 桌面應用程式，用於校對排名截圖並將確認後的資料匯出至 Excel。

> 本專案與 Last War: Survival Game 的開發商或發行商沒有隸屬、認可或贊助關係。

## 目前狀態

目前已具備 .NET 10 WPF 基線、圖片選取／批次取消、候選列勾選與編輯、Open XML 活頁簿建立／安全更新、影像前處理、文字偵測、裁切辨識、CTC 解碼與排名解析的本機 ONNX OCR 路徑、設定與隱私安全 Log。正式 OCR 仍需要合法授權且已固定雜湊的 PP-OCRv5 模型包；DirectML GPU 與正式 ZIP 發佈會在模型來源與雜湊完成後啟用。在此之前可用同名 `.txt` 側錄預覽流程。

英文主要文件：[README.md](README.md)。
