using Microsoft.EntityFrameworkCore;

namespace REMS.Enititys;

public class Setting
{
    public int Id { get; set; } 
    public string SendTo { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
    public int NotificationTimeDifference { get; set; }
}
