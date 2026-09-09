# 採用 Open XML SDK 寫入 Excel

排名活頁簿使用 Microsoft 維護的 DocumentFormat.OpenXml 建立與更新 `.xlsx`，不依賴已安裝的 Microsoft Excel。相較高階 Excel 函式庫，這會增加少量封裝程式碼，但本專案只有固定七張工作表與四欄資料；選擇較直接且採 MIT License 的 SDK，可以減少轉接層、字型處理套件及授權盤點複雜度。
