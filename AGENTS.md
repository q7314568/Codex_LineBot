# AGENTS.md

## 1. 概述 (Overview)

本技術規格檔定義了一個以 C# 與 Quartz.NET 實作之排程提醒 Agent，用於在指定「上課日期」前一日 21:00 及當日 07:00，自動向 LINE 群組推送文字提醒訊息。方案採用免費開源元件，並依照 OpenAI Codex 撰寫風格，以利自動化代碼產生。

## 2. 功能需求 (Functional Requirements)

1. 使用者可於 Console 輸入上課日期（格式 yyyy-MM-dd）。
2. 系統自動計算兩個提醒時點：
   - 上課前一天晚上 21:00
   - 上課當天早上 07:00
3. 將兩個任務使用 Quartz.NET 排程引擎註冊為 Job。
4. 到達觸發時間時，呼叫 LINE Messaging API Push Message 向指定群組 (`groupId`) 發送文字提醒。

## 3. 非功能需求 (Non-functional Requirements)

- **免費方案**：採用免費開源套件（Quartz.NET）與 LINE 免費額度。
- **輕量級**：Console 應用或 Worker Service，無須額外商業授權。
- **可維護性**：採用清晰模組分層與 DI/配置管理。
- **可擴充性**：未來可加入多筆日期、Flex Message、持久化 JobStore。

## 4. 系統架構 (Architecture)

```
+--------------+      +----------------+      +--------------------------+
|  ConsoleApp  | ---> |  Scheduler     | ---> | NotifyJob + LINE Client  |
+--------------+      | (Quartz.NET)   |      +--------------------------+
     ^ Config         +----------------+
     |                                |
     +-- appsettings.json            v
                                      +-----------------------------+
                                      |   LINE Messaging API (HTTP) |
                                      +-----------------------------+
```

- **ConsoleApp**：讀取設定／輸入，初始化 Scheduler。
- **Scheduler**：Quartz.NET 負責管理 Trigger 與 Job。
- **NotifyJob**：實作 IJob，執行時呼用 LINE Client 發送訊息。
- **LINE Client**：透過 `HttpClient` 呼叫 `https://api.line.me/v2/bot/message/push`。

## 5. 相依套件 (Dependencies)

- `Quartz` (Quartz.NET)
- `Microsoft.Extensions.Hosting`
- `Microsoft.Extensions.Configuration.Json`
- `System.Net.Http`

## 6. 設定檔 (Configuration)

在專案根目錄新增 `appsettings.json`：

```json
{
  "Line": {
    "ChannelAccessToken": "<你的 Channel Access Token>",
    "GroupId": "<目標群組 ID>"
  }
}
```

**說明**：

- `ChannelAccessToken`：從 LINE Developers 取得。
- `GroupId`：Webhook 事件 `source.groupId` 或手動取得。

## 7. 模組說明 (Modules)

### 7.1 Program.cs

- **功能**：
  1. 載入 `appsettings.json`。
  2. 讀取使用者輸入的上課日期。 3.計算提醒時點列表。
  3. 建立並啟動 Quartz Scheduler。
  4. 註冊 `NotifyJob` 與觸發器。

### 7.2 NotifyJob.cs

- **實作**：實作 `IJob.Execute`，
  - 從 `JobDataMap` 取得 `message`。
  - 使用 `HttpClient` 呼叫 LINE Push API。

### 7.3 LINE Client

- **責任**：
  - 建立 HTTP 請求，
  - 設定 Bearer Token、Header、Body。
  - 發送並處理回應。

## 8. 排程流程 (Workflow)

1. **使用者互動**：Console 輸入 `yyyy-MM-dd`。
2. **日期驗證**：若格式錯誤，訊息提醒並結束。
3. **計算提醒時間**：
   - `remind1 = classDate.AddDays(-1).AddHours(21)`
   - `remind2 = classDate.AddHours(7)`
4. **建立 Job & Trigger**：
   ```csharp
   var job = JobBuilder.Create<NotifyJob>()
                   .UsingJobData("message", msg)
                   .Build();
   var trigger = TriggerBuilder.Create()
                      .StartAt(DateBuilder.FutureDate(seconds, IntervalUnit.Second))
                      .ForJob(job)
                      .Build();
   ```
5. **Scheduler 啟動**：`await sched.Start();`
6. **觸發執行**：到達時間，自動執行 `NotifyJob.Execute`

## 9. 錯誤處理 (Error Handling)

- **日期解析失敗**：提示格式錯誤，退出程序。
- **HTTP 呼叫失敗**：
  - 檢查狀態碼，若非成功，記錄 Error。
  - 可選：加入重試策略（Polly）。
- **Scheduler 啟動異常**：記錄並終止。

## 10. 部署與執行 (Deployment & Execution)

- **開發測試**：在本機 Console 執行。
- **生產環境**：
  - **Windows 服務 & Linux systemd**：
    - Windows：`sc create` 或 PowerShell `New-Service` 註冊。
    - Linux：撰寫 systemd `.service` 並 `systemctl enable --now`。
  - **.NET Worker Service 部署**：
    1. 建立 Worker 專案：
       ```bash
       dotnet new worker -n LineClassReminderService
       ```
    2. 移植 Quartz 與 NotifyJob 至 Worker Service：\
       在 `Program.cs` 使用 Generic Host：
       ```csharp
       var host = Host.CreateDefaultBuilder(args)
           .UseWindowsService()  // Windows
           .UseSystemd()         // Linux
           .ConfigureServices((context, services) =>
           {
               services.AddQuartz(...);
               services.AddQuartzHostedService(...);
           })
           .Build();
       await host.RunAsync();
       ```
    3. 更新專案檔 (`.csproj`)：
       ```xml
       <Project Sdk=\"Microsoft.NET.Sdk.Worker\">
         <PropertyGroup>
           <TargetFramework>net8.0</TargetFramework>
           <EnableWindowsService>true</EnableWindowsService>
           <EnableSystemdSupport>true</EnableSystemdSupport>
         </PropertyGroup>
       </Project>
       ```
    4. **容器化**：撰寫 Dockerfile 並 `docker build/run`。

## 11. 日誌與監控 (Logging & Monitoring)

- 建議整合 `Microsoft.Extensions.Logging`。
- 輸出至檔案／控制台。
- 可結合 ELK、Application Insights 等。

## 12. 可擴充功能 (Extensibility)

- **多筆日期支援**：解析多行輸入，依序排程。
- **Flex Message**：使用 JSON 模板推送更豐富的訊息樣板。
- **JobStore Persistence**：使用 ADO.NET、MongoDB、Redis 等持久化 Quartz 狀態。
- **錯誤重試**：整合 Polly 實作重試與斷路器。

---

*此規格旨在提供給 OpenAI Codex 或其他自動化工具，作為生成程式碼的技術依據與流程參考。*

