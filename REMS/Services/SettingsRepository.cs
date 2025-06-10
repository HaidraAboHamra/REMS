using Microsoft.EntityFrameworkCore;
using REMS.Abstractions;
using REMS.Data;
using REMS.Enititys;
using REMS.Interfaces;

namespace REMS.Services;

public class SettingsRepository(AppDbContext _db) : ISettings
{
    public async Task<Result<Setting>> GetSettings()
    {
        async Task<bool> Valid()
        {
            var count = await _db.Settings.CountAsync();
            return count != 1;
        }

        var settings = await _db.Settings.FirstOrDefaultAsync();
        if (settings is null)
        {
            return Result<Setting>.Failure(new Error("The settings table was empty"));
        }
        if (await Valid())
        {
            return Result<Setting>.Failure(new Error("The Settings Table can't have more than 1 entity"));
        }
        return Result<Setting>.Success(settings);
    }
    public async Task<Result<string>> GetEmail()
    {
        var settingsResult = await GetSettings();
        if (settingsResult.IsSuccess)
        {
            var settings = settingsResult.Value;
            return Result<string>.Success(settings!.SendTo);
        }
        return Result<string>.Failure(settingsResult.Error);
    }

    public async Task<Result<TimeOnly>> GetTimeOnlyToSendTheEmail()
    {
        var settingsResult = await GetSettings();
        if (settingsResult.IsSuccess)
        {
            var settings = settingsResult.Value;
            return Result<TimeOnly>.Success(new TimeOnly(settings!.Hour, settings!.Minute));
        }
        return Result<TimeOnly>.Failure(settingsResult.Error);
    }

    public async Task<Result<TimeOnly>> GetTimeOnlyToSendTheNotification()
    {
        var settingsResult = await GetSettings();
        if (settingsResult.IsSuccess)
        {
            var settings = settingsResult.Value;
            return Result<TimeOnly>.Success(new TimeOnly(settings!.Hour, settings!.Minute).AddMinutes(-settings.NotificationTimeDifference));
        }
        return Result<TimeOnly>.Failure(settingsResult.Error);
    }

    public async Task<Result> UpdateSettings(Setting setting)
    {
        var settingsResult = await GetSettings();
        if (settingsResult.IsSuccess)
        {
            _db.Settings.Update(setting);
            await _db.SaveChangesAsync();
            return Result<Setting>.Success(setting);
        }
        return Result.Failure(new("Error"));
    }
}
