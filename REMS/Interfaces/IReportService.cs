using REMS.Enititys;

namespace REMS.Interfaces
{
    public interface IReportService
    {
        // =====================================================
        // Reports القديمة
        // =====================================================

        Task<List<Report>> GetReportsByDate(
            DateTime date);

        Task<Report> AddReport(
            Report report);

        Task SendDailyReports();

        Task SendDailyReports(
            string email,
            DateTime date);

        // =====================================================
        // FollowUpReport
        // =====================================================

        Task<FollowUpReport> AddFollowUpReport(
            FollowUpReport report);

        Task SendTaskAssignmentEmail(
            FollowUpReport report);

        Task SendDailyTaskReports();

        Task SendDailyTaskReports(
            string email,
            DateTime date);

        Task SendWeeklyTaskReports();

        Task SendWeeklyTaskReports(
            string email,
            DateTime weekStart);

        // =====================================================
        // Complaints
        // =====================================================

        Task<Complaint> AddComplaint(
            Complaint complaint);

        Task SendWeeklyComplaint();
    }
}