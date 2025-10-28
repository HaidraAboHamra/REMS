using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys
{
    public class FollowUpReport
    {
        [Key]
        public int Id { get; set; }

        // المحتوى العام للتقرير
        [DisplayName("المهمة")]
        public string? Content { get; set; }

        // اسم المبلغ/المستخدم الذي أنشأ التقرير
        [DisplayName("الاسم الكامل")]
        public string? FullName { get; set; }

        // تاريخ إنشاء التقرير (افتراضي: الآن)
        [DisplayName("التاريخ")]
        public DateTime DateTime { get; set; } = DateTime.Now;

        // حالة انتهاء التقرير
        public bool IsDone { get; set; }

        [DisplayName("الحالة")]
        public string? IsDoneOrNot { get; set; }

        // مسار ملف عام/صورة رئيسية إن وُجد
        public string? Path { get; set; }

        // ------------------ الحقول الأساسية الجديدة ------------------

        [DisplayName("اسم المنطقة")]
        public string? Region { get; set; }

        [DisplayName("اسم المحافظة")]
        public string? Governorate { get; set; }

        [DisplayName("اسم المنسق")]
        public string? Coordinator { get; set; }

        [DisplayName("تاريخ العمل")]
        [DataType(DataType.Date)]
        public DateTime? WorkDate { get; set; }

        [DisplayName("اسم المتجر")]
        public string? StoreName { get; set; }

        [DisplayName("نوع المتجر")]
        public string? StoreType { get; set; }

        [DisplayName("العنوان")]
        public string? Address { get; set; }

        [DisplayName("رقم الهاتف")]
        public string? Phone { get; set; }

        // مسار / رابط ملف توقيع العقد (يخزن المسار النسبي داخل wwwroot/Files مثلاً)
        [DisplayName("توقيع العقد - ملف")]
        public string? ContractFilePath { get; set; }

        [DisplayName("اسم ملف توقيع العقد")]
        public string? ContractFileName { get; set; }

        [DisplayName("تاريخ العقد")]
        [DataType(DataType.Date)]
        public DateTime? ContractDate { get; set; }

        [DisplayName("إنجاز كافة المهام")]
        public bool AllTasksDone { get; set; }

        [DisplayName("عدد المنتجات التي تم إدخالها")]
        public int? ProductsCount { get; set; }

        // ------------------ الحقول المخصصة (كما كان) ------------------
        // حفظ الحقول المخصصة بصيغة JSON (مصفوفة من { Name, Type, Value })
        public string? CustomFieldsJson { get; set; }
    }
}
