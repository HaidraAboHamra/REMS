using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace REMS.Enititys
{
    public class FollowUpReport
    {
        [Key]
        public int Id { get; set; }

        // =========================================
        // معلومات المهمة
        // =========================================

        [DisplayName("اسم المهمة")]
        [Required]
        public string? Content { get; set; }

        [DisplayName("تفاصيل المهمة")]
        public string? TaskDetails { get; set; }

        [DisplayName("نوع المهمة")]
        public string? TaskType { get; set; }

        [DisplayName("اسم العميل / المشروع")]
        public string? ClientOrProject { get; set; }

        // =========================================
        // الموظف المسؤول
        // =========================================

        [DisplayName("رقم الموظف المسؤول")]
        public int? AssignedEmployeeId { get; set; }

        [DisplayName("الموظف المسؤول")]
        public string? AssignedEmployee { get; set; }

        [DisplayName("الأولوية")]
        public string? Priority { get; set; }

        // =========================================
        // متابعة الإنجاز
        // =========================================

        [DisplayName("عدد البنود الكلي")]
        public int TotalItems { get; set; }

        [DisplayName("عدد البنود المنجزة")]
        public int CompletedItems { get; set; }

        [DisplayName("ما تم إنجازه")]
        public string? CompletedDetails { get; set; }

        [DisplayName("المدة المتوقعة بالأيام")]
        public decimal? ExpectedDurationDays { get; set; }

        [DisplayName("تاريخ بدء المهمة")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DisplayName("تاريخ التسليم المتوقع")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [DisplayName("تاريخ الإكمال")]
        [DataType(DataType.Date)]
        public DateTime? CompletedDate { get; set; }

        // =========================================
        // الحالة
        // =========================================

        [DisplayName("الحالة")]
        public string? IsDoneOrNot { get; set; }

        public bool IsDone { get; set; }

        // =========================================
        // المستخدم والتواريخ
        // =========================================

        [DisplayName("أنشأ المهمة")]
        public string? FullName { get; set; }

        [DisplayName("تاريخ الإنشاء")]
        public DateTime DateTime { get; set; } = DateTime.Now;

        [DisplayName("آخر تحديث")]
        public DateTime? LastUpdatedDate { get; set; }

        [DisplayName("آخر من قام بالتحديث")]
        public string? LastUpdatedBy { get; set; }

        // =========================================
        // ملف / مرفق
        // =========================================

        [DisplayName("المرفق")]
        public string? Path { get; set; }

        // =========================================
        // حقول قديمة
        // أبقيتها مؤقتاً حتى لا نخسر البيانات القديمة
        // =========================================

        public string? Region { get; set; }

        public string? Governorate { get; set; }

        public string? Coordinator { get; set; }

        public DateTime? WorkDate { get; set; }

        public string? StoreName { get; set; }

        public string? StoreType { get; set; }

        public string? Address { get; set; }

        public string? Phone { get; set; }

        public string? ContractFilePath { get; set; }

        public string? ContractFileName { get; set; }

        public DateTime? ContractDate { get; set; }

        public bool AllTasksDone { get; set; }

        public int? ProductsCount { get; set; }

        // =========================================
        // قيم محسوبة
        // =========================================

        [NotMapped]
        public int RemainingItems
        {
            get
            {
                return Math.Max(
                    TotalItems - CompletedItems,
                    0
                );
            }
        }

        [NotMapped]
        public int ProgressPercentage
        {
            get
            {
                if (TotalItems <= 0)
                    return 0;

                return (int)Math.Round(
                    ((double)CompletedItems / TotalItems) * 100
                );
            }
        }
    }
}