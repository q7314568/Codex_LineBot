# LineReminder

This project schedules LINE reminders for a specified class date using Quartz.NET.

## Usage

1. Set `ChannelAccessToken` and `GroupId` in `LineReminder/appsettings.json`.
2. Build the project:
   ```bash
   dotnet build LineReminder
   ```
3. Run the program:
   ```bash
   dotnet run --project LineReminder
   ```
4. Enter the class date in `yyyy-MM-dd` format when prompted.

