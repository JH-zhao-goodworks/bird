using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BirdHotel.Web.Pages.Owners;

public class IndexModel(OwnerRepository ownerRepository, BirdRepository birdRepository) : PageModel
{
    public record OwnerRow(Owner Owner, int BirdCount);

    public List<OwnerRow> Rows { get; private set; } = [];

    [BindProperty]
    public Owner Input { get; set; } = new();

    public void OnGet(int? editId)
    {
        Load();
        if (editId is { } id)
            Input = ownerRepository.GetById(id) ?? new Owner();
    }

    private void Load()
    {
        var birds = birdRepository.GetAll();
        Rows = ownerRepository.GetAll()
            .Select(o => new OwnerRow(o, birds.Count(b => b.OwnerId == o.Id)))
            .ToList();
    }

    public IActionResult OnPostSave()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            TempData["Error"] = "飼い主名を入力してください。";
            return RedirectToPage();
        }

        Input.Name = Input.Name.Trim();
        if (Input.Id == 0)
        {
            ownerRepository.Insert(Input);
            TempData["Message"] = $"「{Input.Name}」を登録しました。";
        }
        else
        {
            ownerRepository.Update(Input);
            TempData["Message"] = $"「{Input.Name}」を更新しました。";
        }

        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        var birdCount = birdRepository.GetByOwner(id).Count;
        if (birdCount > 0)
        {
            TempData["Error"] = $"この飼い主には鳥が{birdCount}羽登録されているため削除できません。先に鳥の飼い主を変更してください。";
            return RedirectToPage();
        }

        ownerRepository.Delete(id);
        TempData["Message"] = "削除しました。";
        return RedirectToPage();
    }

    // 同一人物が別々に登録されてしまった場合に1件へまとめる
    public IActionResult OnPostMerge(int targetId, int[] sourceIds)
    {
        var target = ownerRepository.GetById(targetId);
        if (target is null)
        {
            TempData["Error"] = "統合先の飼い主が見つかりません。";
            return RedirectToPage();
        }

        var merged = 0;
        foreach (var sourceId in sourceIds.Where(id => id != targetId))
        {
            foreach (var bird in birdRepository.GetByOwner(sourceId))
            {
                bird.OwnerId = targetId;
                birdRepository.Update(bird);
            }
            ownerRepository.Delete(sourceId);
            merged++;
        }

        TempData["Message"] = merged > 0
            ? $"{merged}件を「{target.Name}」に統合しました。"
            : "統合する飼い主を選んでください。";
        return RedirectToPage();
    }
}
