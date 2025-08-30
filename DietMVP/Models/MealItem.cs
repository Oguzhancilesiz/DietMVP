using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DietMVP.Models
{
    [Table("meal_items")]
    public class MealItem : BaseModel
    {
        [PrimaryKey("id")] public Guid Id { get; set; }
        [Column("meal_id")] public Guid MealId { get; set; }
        [Column("name")] public string Name { get; set; } = "";
        [Column("qty")] public decimal? Qty { get; set; }
        [Column("unit")] public string? Unit { get; set; }
        [Column("kcal")] public int? Kcal { get; set; }
        [Column("note")] public string? Note { get; set; }
        [Column("sort")] public int Sort { get; set; } = 0;
    }
}
