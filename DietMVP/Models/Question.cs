using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DietMVP.Models
{
    [Table("questions")]
    public class Question : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("patient_id")]
        public Guid PatientId { get; set; }

        [Column("doctor_id")]
        public Guid DoctorId { get; set; }

        [Column("title")]
        public string? Title { get; set; }

        [Column("body")]
        public string? Body { get; set; }

        [Column("answer_text")]
        public string? AnswerText { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Open"; // Open, Answered, Closed (istersen genişlet)

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("answered_at")]
        public DateTime? AnsweredAt { get; set; }
    }
}
