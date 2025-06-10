using REMS.Enititys;

namespace REMS.Interfaces
{
    public interface IReportService
    {
        Task<List<Report>> GetReportsByDate(DateTime date);
        Task<Report> AddReport(Report report);
        Task SendDailyReports();
        Task SendWeeklyComplaint();
        Task<Complaint> AddComplaint(Complaint complaint);
        Task SendDailyReports(string email,DateTime date);
    }
}
