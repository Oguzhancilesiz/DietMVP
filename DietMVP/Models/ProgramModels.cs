using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DietMVP.Models
{

    [Table("programs")]
    public class ProgramEntity : BaseModel
    {
        [PrimaryKey("id")] public Guid Id { get; set; }
        [Column("patient_id")] public Guid PatientId { get; set; }
        [Column("start_date")] public DateOnly StartDate { get; set; }
        [Column("end_date")] public DateOnly EndDate { get; set; }
        [Column("days_count")] public int DaysCount { get; set; }
        [Column("daily_water_target_ml")] public int? DailyWaterTargetMl { get; set; }
    }

    [Table("program_days")]
    public class ProgramDay : BaseModel
    {
        [PrimaryKey("id")] public Guid Id { get; set; }
        [Column("program_id")] public Guid ProgramId { get; set; }
        [Column("local_date")] public DateOnly LocalDate { get; set; }
    }

    public enum MealSlot { Breakfast, Snack1, Lunch, Snack2, Dinner }

    [Table("meals")]
    public class Meal : BaseModel
    {
        [PrimaryKey("id")] public Guid Id { get; set; }
        [Column("program_day_id")] public Guid ProgramDayId { get; set; }
        [Column("slot")] public string Slot { get; set; } = "Breakfast";
        [Column("start_time")] public TimeOnly StartTime { get; set; }
        [Column("end_time")] public TimeOnly EndTime { get; set; }
        [Column("title")] public string? Title { get; set; }
        [Column("note")] public string? Note { get; set; }
        [Column("kcal")] public int? Kcal { get; set; }
    }
}
