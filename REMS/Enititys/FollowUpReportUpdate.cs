using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace REMS.Enititys
{
    public class FollowUpReportUpdate
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(FollowUpReport))]
        public int FollowUpReportId { get; set; }

        public FollowUpReport? FollowUpReport { get; set; }


        [DisplayName("عدد المنجز بعد التحديث")]
        public int CompletedItems { get; set; }


        [DisplayName("ما تم إنجازه")]
        public string? CompletedDetails { get; set; }


        [DisplayName("ملاحظات")]
        public string? Notes { get; set; }


        [DisplayName("الحالة")]
        public string? Status { get; set; }


        [DisplayName("نسبة الإنجاز")]
        public int ProgressPercentage { get; set; }


        [DisplayName("التاريخ")]
        public DateTime DateTime { get; set; } = DateTime.Now;


        [DisplayName("الموظف")]
        public string? UpdatedBy { get; set; }
    }
}