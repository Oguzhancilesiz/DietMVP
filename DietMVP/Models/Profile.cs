using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DietMVP.Models
{
    [Table("profiles")]
    public class Profile : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("role")] public string Role { get; set; } = "doctor";
        [Column("full_name")] public string FullName { get; set; } = "";
        [Column("phone")] public string? Phone { get; set; }
        [Column("daily_water_target_ml")] public int DailyWaterTargetMl { get; set; } = 2000;
    }
}