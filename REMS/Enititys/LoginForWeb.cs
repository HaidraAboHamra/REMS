using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys
{
    public class LoginForWeb
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public bool IsSalman {  get; set; }
    }
}
