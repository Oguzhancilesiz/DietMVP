using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DietMVP.Models
{
    [Table("water_logs")]
    public class WaterLog : BaseModel
    {
        [PrimaryKey("id")] public Guid Id { get; set; }
        [Column("program_day_id")] public Guid ProgramDayId { get; set; }
        [Column("patient_id")] public Guid PatientId { get; set; }
        [Column("ml")] public int Ml { get; set; }
        [Column("logged_at")] public DateTime LoggedAt { get; set; }
    }
}
