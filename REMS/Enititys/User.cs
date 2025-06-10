using Microsoft.AspNetCore.Identity;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [DisplayName("الاسم الكامل")]
        public string? FullName { get; set; }
        [DisplayName("رقم هاتف")]
        public string? PhoneNumber { get; set; }
        public long? ChatId {  get; set; }
        [DisplayName("الايميل")]
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }
        public bool IsAdmin { get; set; } = false;
        public bool IsItAdmin { get; set; } = false;
        public bool IsFollowUpAdmin { get; set; } = false;
        public bool IsFUser {  get; set; } = false;

    }
}
