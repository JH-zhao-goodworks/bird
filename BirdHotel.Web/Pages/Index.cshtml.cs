using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BirdHotel.Web.Pages;

public class IndexModel(CageRepository cageRepository, ReservationRepository reservationRepository) : PageModel
{
    public record CageCard(Cage Cage, List<Reservation> Reservations, int MaxConcurrent);
    public record CageGroup(string Key, string Label, List<CageCard> Cards);

    public List<CageGroup> Groups { get; private set; } = [];

    public void OnGet()
    {
        var cages = cageRepository.GetAll();
        var allReservations = reservationRepository.GetAll();

        Groups = OrderGroups(cages)
            .Select(group => new CageGroup(
                group.Key,
                group.Key.Length > 0 ? group.Key : "グループなし",
                group.Select(cage =>
                {
                    var reservations = allReservations
                        .Where(r => r.CageId == cage.Id)
                        .OrderBy(r => r.StartDate)
                        .ThenBy(r => r.BirdName)
                        .ToList();
                    var maxConcurrent = reservations
                        .Select(r => reservations.Count(other => r.OverlapsWith(other.StartDate, other.EndDate)))
                        .DefaultIfEmpty(0)
                        .Max();
                    return new CageCard(cage, reservations, maxConcurrent);
                }).ToList()))
            .ToList();
    }

    // 表示順（GroupOrder）が設定されていればその順、未設定なら名前順。グループ未設定の籠は最後。
    private static List<IGrouping<string, Cage>> OrderGroups(List<Cage> cages) =>
        cages
            .GroupBy(c => c.GroupName)
            .OrderBy(g => g.Min(c => c.GroupOrder) == 0)
            .ThenBy(g => g.Min(c => c.GroupOrder))
            .ThenBy(g => g.Key.Length == 0)
            .ThenBy(g => g.Key, Comparer<string>.Create(CageRepository.CompareNatural))
            .ToList();

    // グループを1つ前後に動かして、その並び順を保存する
    public IActionResult OnPostMoveGroup(string? groupKey, int direction)
    {
        var cages = cageRepository.GetAll();
        var groupKeys = OrderGroups(cages).Select(g => g.Key).ToList();

        var index = groupKeys.IndexOf(groupKey ?? "");
        var target = index + direction;
        if (index >= 0 && target >= 0 && target < groupKeys.Count)
        {
            (groupKeys[index], groupKeys[target]) = (groupKeys[target], groupKeys[index]);

            for (var i = 0; i < groupKeys.Count; i++)
            {
                foreach (var cage in cages.Where(c => c.GroupName == groupKeys[i]))
                {
                    cage.GroupOrder = i + 1;
                    cageRepository.Update(cage);
                }
            }
        }

        return RedirectToPage();
    }

    public IActionResult OnPostClearAll()
    {
        var reservations = reservationRepository.GetAll();
        foreach (var reservation in reservations)
            reservationRepository.Delete(reservation.Id);

        TempData["Message"] = $"すべての籠の予約{reservations.Count}件を取り消しました。";
        return RedirectToPage();
    }
}
