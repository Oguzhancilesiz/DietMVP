using DietMVP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Services
{
    public class MealItemService
    {
        public async Task<List<MealItem>> GetItemsAsync(Guid mealId)
        {
            await Supa.InitAsync();
            var res = await Supa.Client.From<MealItem>()
                .Where(x => x.MealId == mealId)
                .Order(x => x.Sort, Ordering.Ascending, NullPosition.Last) // ← kilit satır
                .Get();

            return res.Models;
        }

        public async Task<MealItem> AddItemAsync(Guid mealId, string name, decimal? qty, string? unit, int? kcal, string? note)
        {
            await Supa.InitAsync();
            var item = new MealItem { MealId = mealId, Name = name, Qty = qty, Unit = unit, Kcal = kcal, Note = note };
            var res = await Supa.Client.From<MealItem>().Insert(item);
            return res.Models.First();
        }

        public async Task DeleteItemAsync(Guid id)
        {
            await Supa.InitAsync();
            await Supa.Client.From<MealItem>().Where(x => x.Id == id).Delete();
        }
    }
}
