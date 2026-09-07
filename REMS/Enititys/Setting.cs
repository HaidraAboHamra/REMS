using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace REMS.Enititys;

public class Setting
{
    public int Id { get; set; } 
    public string SendTo { get; set; } = string.Empty;
    public int Hour { get; set; }
    public int Minute { get; set; }
    public int NotificationTimeDifference { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LatePenaltyAmount { get; set; }
}
