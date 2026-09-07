using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys
{
    public class LoginForWeb
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsSalman {  get; set; }
    }
}
