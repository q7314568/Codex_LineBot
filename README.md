# LineBotScheduler

This project schedules LINE Bot reminders for a specified class date using Quartz.NET.

## Usage

1. Set `ChannelAccessToken` and `GroupId` in `src/LineBotScheduler/appsettings.json`.
2. Build the project:
   ```bash
   dotnet build src/LineBotScheduler
   ```
3. Run the program:
   ```bash
   dotnet run --project src/LineBotScheduler
   ```
4. Enter the class date in `yyyy-MM-dd` format when prompted.
