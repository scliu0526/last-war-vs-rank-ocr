# 採用 C#、.NET 10 LTS、WPF 與 x64

桌面程式採用 C#、.NET 10 LTS、WPF 與 x64 作為技術基線，目標框架最低平台為 Windows 10 2004（build 19041）x64，讓 Windows 10 22H2 與 Windows 11 都能執行。WPF 能直接支援圖片預覽、可編輯結果表格、核取狀態及低可信提示，並可配合 Windows 自包含發佈；.NET 10 LTS 的支援期至 2028 年 11 月，避免新專案建立在即將結束支援的 .NET 8 上。Windows 10 Home／Pro 已超出 Microsoft 一般支援，README 必須揭露此限制；本階段只在現有 Windows 11 電腦驗證，Windows 10 相容性必須標示為尚未實機或 VM 驗證。開發環境須安裝免費的 .NET 10 SDK。
