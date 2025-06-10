using MudBlazor;
using REMS.Enititys;

namespace REMS.Interfaces
{
    public interface IFollowUpReportService
    {
        Task<List<FollowUpReport>> GetReportsByDate(DateTime date);
        Task<FollowUpReport> AddReport(FollowUpReport report);
        Task SendWeeklyComplaint();
        Task<Complaint> AddComplaint(Complaint complaint);
        Task SendDailyReports(string email, DateTime date);
        Task<FollowUpReport> Update(FollowUpReport report);
        Task<FollowUpReport> Update(FollowUpReport report, string newPath);
        Task<bool> Delete(int Id);
        Task<FollowUpReport> Get(int Id);
    }
}
