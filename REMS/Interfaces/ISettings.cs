using REMS.Abstractions;
using REMS.Enititys;

namespace REMS.Interfaces;

public interface ISettings
{
    Task<Result<Setting>> GetSettings();
    public Task<Result<string>> GetEmail();
    public Task<Result<TimeOnly>> GetTimeOnlyToSendTheEmail();
    public Task<Result<TimeOnly>> GetTimeOnlyToSendTheNotification();
    public Task<Result> UpdateSettings(Setting setting);
}
