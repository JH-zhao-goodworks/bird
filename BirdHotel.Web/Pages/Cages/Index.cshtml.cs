using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BirdHotel.Web.Pages.Cages;

public class IndexModel(CageRepository cageRepository, ReservationRepository reservationRepository) : PageModel
{
    public List<Cage> Cages { get; private set; } = [];
    public List<string> GroupNames { get; private set; } = [];

    [BindProperty]
    public Cage Input { get; set; } = new() { Capacity = 2 };

    public void OnGet(int? editId)
    {
        Load();
        if (editId is { } id)
            Input = cageRepository.GetById(id) ?? new Cage { Capacity = 2 };
    }

    private void Load()
    {
        Cages = cageRepository.GetAll();
        GroupNames = Cages.Select(c => c.GroupName).Where(g => g.Length > 0).Distinct().ToList();
    }

    public IActionResult OnPostSave()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            TempData["Error"] = "籠名を入力してください。";
            return RedirectToPage();
        }

        Input.Name = Input.Name.Trim();
        Input.GroupName = (Input.GroupName ?? "").Trim();
        if (Input.Id == 0)
        {
            cageRepository.Insert(Input);
            TempData["Message"] = $"「{Input.Name}」を登録しました。";
        }
        else
        {
            cageRepository.Update(Input);
            TempData["Message"] = $"「{Input.Name}」を更新しました。";
        }

        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        var reservationCount = reservationRepository.GetByCage(id).Count;
        cageRepository.Delete(id);
        TempData["Message"] = reservationCount > 0
            ? $"籠を削除しました（関連する予約{reservationCount}件も削除されました）。"
            : "籠を削除しました。";
        return RedirectToPage();
    }
}
