using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys
{
    public class Complaint
    {
        [Key]
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
    }
}
