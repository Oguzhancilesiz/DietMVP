using DietMVP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Services
{
    public class DietService
    {
        public async Task<ProgramEntity> CreateProgramHeaderAsync(Guid patientId, DateOnly start, int days, int? dailyWaterTargetMl = null)
        {
            await Supa.InitAsync();
            var prog = new ProgramEntity
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                StartDate = start,
                DaysCount = days,
                EndDate = start.AddDays(days - 1),
                DailyWaterTargetMl = dailyWaterTargetMl
            };
            return (await Supa.Client.From<ProgramEntity>().Insert(prog)).Models[0];
        }

        // seçiliSlots: slot adları (Breakfast, Snack1, Lunch, Snack2, Dinner)
        public async Task SeedDaysAndMealsAsync(Guid programId,
            IEnumerable<string> selectedSlots,
            Dictionary<string, (TimeOnly s, TimeOnly e)> timeMap)
        {
            await Supa.InitAsync();
            var prog = (await Supa.Client.From<ProgramEntity>().Where(x => x.Id == programId).Get()).Models.First();

            for (int i = 0; i < prog.DaysCount; i++)
            {
                var day = new ProgramDay { Id = Guid.NewGuid(), ProgramId = prog.Id, LocalDate = prog.StartDate.AddDays(i) };
                day = (await Supa.Client.From<ProgramDay>().Insert(day)).Models[0];

                var meals = new List<Meal>();
                foreach (var slot in selectedSlots)
                {
                    var t = timeMap[slot];
                    meals.Add(new Meal
                    {
                        Id = Guid.NewGuid(),
                        ProgramDayId = day.Id,
                        Slot = slot,
                        StartTime = t.s,
                        EndTime = t.e
                    });
                }
                if (meals.Count > 0)
                    await Supa.Client.From<Meal>().Insert(meals);
            }
        }

        public async Task<List<ProgramDay>> GetDaysAsync(Guid programId)
        {
            await Supa.InitAsync();
            var res = await Supa.Client.From<ProgramDay>().Where(x => x.ProgramId == programId).Get();
            return res.Models;
        }

        public async Task<List<Meal>> GetMealsOfDayAsync(Guid dayId)
        {
            await Supa.InitAsync();
            var res = await Supa.Client.From<Meal>().Where(m => m.ProgramDayId == dayId).Get();
            return res.Models.OrderBy(m => m.StartTime).ToList();
        }

        public async Task UpdateMealAsync(Guid mealId, string? title, string? note, TimeOnly? start, TimeOnly? end)
        {
            await Supa.InitAsync();
            // Set/Update zinciri
            await Supa.Client.From<Meal>()
                .Where(m => m.Id == mealId)
                .Set(m => m.Title, title)
                .Set(m => m.Note, note)
                .Set(m => m.StartTime, start ?? default)
                .Set(m => m.EndTime, end ?? default)
                .Update();
        }

        public async Task<Meal> AddMealToDayAsync(Guid dayId, string slot, TimeOnly start, TimeOnly end)
        {
            await Supa.InitAsync();
            var meal = new Meal
            {
                Id = Guid.NewGuid(),
                ProgramDayId = dayId,
                Slot = slot,
                StartTime = start,
                EndTime = end
            };
            return (await Supa.Client.From<Meal>().Insert(meal)).Models[0];
        }


        // Kaynak günün tüm içeriğini, programdaki KALAN günlere kopyalar.
        // overwrite=true ise hedef günlerdeki mevcut öğünleri siler (meal_items cascade ile silinir).
        public async Task<int> CopyDayToRemainingAsync(Guid sourceDayId, bool overwrite)
        {
            await Supa.InitAsync();

            // kaynak gün + programı çek
            var srcDay = (await Supa.Client.From<ProgramDay>().Where(d => d.Id == sourceDayId).Get()).Models.First();
            var prog = (await Supa.Client.From<ProgramEntity>().Where(p => p.Id == srcDay.ProgramId).Get()).Models.First();

            // hedefler: aynı programın, tarih > kaynak tarih olan günleri
            var targets = (await Supa.Client.From<ProgramDay>()
                                          .Where(d => d.ProgramId == prog.Id)
                                          .Filter(nameof(ProgramDay.LocalDate), Operator.GreaterThan, srcDay.LocalDate.ToString("yyyy-MM-dd")) // > srcDay
                                          .Order(d => d.LocalDate, Ordering.Ascending)
                                          .Get()).Models;

            int done = 0;
            foreach (var t in targets)
            {
                await CloneDayAsync(srcDay.Id, t.Id, overwrite);
                done++;
            }
            return done;
        }

        // Tek gün kopyalama: src → dst
        public async Task CloneDayAsync(Guid srcDayId, Guid dstDayId, bool overwrite)
        {
            await Supa.InitAsync();

            if (overwrite)
            {
                // hedef gündeki tüm öğünleri sil (meal_items FK cascade)
                await Supa.Client.From<Meal>().Where(m => m.ProgramDayId == dstDayId).Delete();
            }

            // kaynak günün öğünlerini çek
            var srcMeals = (await Supa.Client.From<Meal>()
                                .Where(m => m.ProgramDayId == srcDayId)
                                .Order(m => m.StartTime, Ordering.Ascending)
                                .Get()).Models;

            if (srcMeals.Count == 0) return;

            // yeni öğünleri inşa et
            var newMeals = new List<Meal>();
            var mapOldToNew = new Dictionary<Guid, Guid>();

            foreach (var m in srcMeals)
            {
                var newId = Guid.NewGuid();
                mapOldToNew[m.Id] = newId;

                newMeals.Add(new Meal
                {
                    Id = newId,
                    ProgramDayId = dstDayId,
                    Slot = m.Slot,
                    StartTime = m.StartTime,
                    EndTime = m.EndTime,
                    Title = m.Title,
                    Note = m.Note,
                    Kcal = m.Kcal
                });
            }

            await Supa.Client.From<Meal>().Insert(newMeals);

            // her öğünün item'larını kopyala
            var newItems = new List<MealItem>();
            foreach (var m in srcMeals)
            {
                var items = (await Supa.Client.From<MealItem>().Where(i => i.MealId == m.Id).Get()).Models;
                foreach (var it in items)
                {
                    newItems.Add(new MealItem
                    {
                        Id = Guid.NewGuid(),
                        MealId = mapOldToNew[m.Id],
                        Name = it.Name,
                        Qty = it.Qty,
                        Unit = it.Unit,
                        Kcal = it.Kcal,
                        Note = it.Note,
                        Sort = it.Sort
                    });
                }
            }

            if (newItems.Count > 0)
                await Supa.Client.From<MealItem>().Insert(newItems);
        }
    }
}
