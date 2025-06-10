using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys
{
    public class FollowUpReport
    {
        [Key]
        public int Id { get; set; }
        [DisplayName("المهمة")]

        public string? Content { get; set; }
        [DisplayName("الاسم الكامل")]

        public string? FullName { get; set; }
        [DisplayName("التاريخ")]

        public DateTime DateTime { get; set; }
        public bool IsDone { get; set; }
        [DisplayName("الحالة")]

        public string? IsDoneOrNot { get; set; }
        public string? Path { get; set; }
    }
}
