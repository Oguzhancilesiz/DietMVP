using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DietMVP.Models
{
    [Table("meal_logs")]
    public class MealLog : BaseModel
    {
        [PrimaryKey("id")] public Guid Id { get; set; }
        [Column("meal_id")] public Guid MealId { get; set; }
        [Column("patient_id")] public Guid PatientId { get; set; }
        [Column("status")] public string Status { get; set; } = "Eaten"; // Eaten | Skipped
        [Column("photo_url")] public string? PhotoUrl { get; set; }
        [Column("logged_at")] public DateTime LoggedAt { get; set; }
    }

}
