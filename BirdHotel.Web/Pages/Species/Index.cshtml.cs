using BirdHotel.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpeciesEntity = BirdHotel.Web.Models.Species;

namespace BirdHotel.Web.Pages.Species;

public class IndexModel(SpeciesRepository speciesRepository, BirdRepository birdRepository) : PageModel
{
    public record SpeciesRow(SpeciesEntity Species, int BirdCount);

    public List<SpeciesRow> Rows { get; private set; } = [];

    [BindProperty]
    public SpeciesEntity Input { get; set; } = new();

    public void OnGet(int? editId)
    {
        Load();
        if (editId is { } id)
            Input = Rows.FirstOrDefault(r => r.Species.Id == id)?.Species ?? new SpeciesEntity();
    }

    private void Load()
    {
        var birds = birdRepository.GetAll();
        Rows = speciesRepository.GetAll()
            .Select(s => new SpeciesRow(s, birds.Count(b => b.Species == s.Name)))
            .ToList();
    }

    public IActionResult OnPostSave()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            TempData["Error"] = "種類名を入力してください。";
            return RedirectToPage();
        }

        Input.Name = Input.Name.Trim();
        if (Input.Id == 0)
        {
            speciesRepository.Insert(Input);
            TempData["Message"] = $"「{Input.Name}」を登録しました。";
        }
        else
        {
            speciesRepository.Update(Input);
            TempData["Message"] = $"「{Input.Name}」を更新しました。";
        }

        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        speciesRepository.Delete(id);
        TempData["Message"] = "種類の一覧から削除しました（登録済みの鳥の種類はそのまま残ります）。";
        return RedirectToPage();
    }

    // 表記ゆれで別々に登録された種類を1つにまとめる
    public IActionResult OnPostMerge(int targetId, int[] sourceIds)
    {
        Load();
        var target = Rows.FirstOrDefault(r => r.Species.Id == targetId)?.Species;
        if (target is null)
        {
            TempData["Error"] = "統合先の種類が見つかりません。";
            return RedirectToPage();
        }

        var birds = birdRepository.GetAll();
        var merged = 0;
        foreach (var sourceId in sourceIds.Where(id => id != targetId))
        {
            var source = Rows.FirstOrDefault(r => r.Species.Id == sourceId)?.Species;
            if (source is null) continue;

            foreach (var bird in birds.Where(b => b.Species == source.Name))
            {
                bird.Species = target.Name;
                birdRepository.Update(bird);
            }
            speciesRepository.Delete(sourceId);
            merged++;
        }

        TempData["Message"] = merged > 0
            ? $"{merged}件を「{target.Name}」に統合しました。"
            : "統合する種類を選んでください。";
        return RedirectToPage();
    }
}
