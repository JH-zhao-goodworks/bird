using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BirdHotel.Web.Pages.Cages;

public class DetailModel(
    CageRepository cageRepository,
    BirdRepository birdRepository,
    ReservationRepository reservationRepository) : PageModel
{
    public record ReservationRow(Reservation Reservation, Bird? Bird);

    public Cage Cage { get; private set; } = new();
    public List<ReservationRow> Rows { get; private set; } = [];
    public int MaxConcurrent { get; private set; }
    public List<Cage> OtherCages { get; private set; } = [];

    public IActionResult OnGet(int id)
    {
        var cage = cageRepository.GetById(id);
        if (cage is null) return NotFound();

        Load(cage);
        return Page();
    }

    private void Load(Cage cage)
    {
        Cage = cage;
        var birdsById = birdRepository.GetAll().ToDictionary(b => b.Id);
        var reservations = reservationRepository.GetByCage(cage.Id)
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.BirdName)
            .ToList();

        Rows = reservations
            .Select(r => new ReservationRow(r, birdsById.GetValueOrDefault(r.BirdId)))
            .ToList();

        MaxConcurrent = reservations
            .Select(r => reservations.Count(other => r.OverlapsWith(other.StartDate, other.EndDate)))
            .DefaultIfEmpty(0)
            .Max();

        OtherCages = cageRepository.GetAll().Where(c => c.Id != cage.Id).ToList();
    }

    public IActionResult OnPostClear(int id)
    {
        var reservations = reservationRepository.GetByCage(id);
        foreach (var reservation in reservations)
            reservationRepository.Delete(reservation.Id);

        TempData["Message"] = $"予約{reservations.Count}件を取り消しました。";
        return RedirectToPage(new { id });
    }

    // 選んだ鳥を別の籠へ移す。期間が重なる場合は移動できない（無期限は重複として数えない）。
    public IActionResult OnPostMove(int id, int[] reservationIds, int destinationCageId)
    {
        var destination = cageRepository.GetById(destinationCageId);
        if (destination is null)
        {
            TempData["Error"] = "移動先の籠が見つかりません。";
            return RedirectToPage(new { id });
        }

        var moving = reservationIds
            .Select(reservationRepository.GetById)
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

        if (moving.Count == 0)
        {
            TempData["Error"] = "移動する鳥を選んでください。";
            return RedirectToPage(new { id });
        }

        var destinationReservations = reservationRepository.GetByCage(destinationCageId);
        foreach (var bird in moving)
        {
            var blocking = destinationReservations.FirstOrDefault(r => Conflicts(r, bird));
            if (blocking is not null)
            {
                TempData["Error"] = $"移動できません。「{destination.Name}」の「{blocking.BirdName}」と「{bird.BirdName}」の期間が重なっています。";
                return RedirectToPage(new { id });
            }
        }

        foreach (var bird in moving)
        {
            bird.CageId = destinationCageId;
            reservationRepository.Update(bird);
        }

        TempData["Message"] = $"{moving.Count}羽を「{destination.Name}」へ移動しました。";
        return RedirectToPage(new { id });
    }

    // 無期限の予約は重複として数えない（経営者の鳥が常駐していても移動できるようにするため）
    private static bool Conflicts(Reservation a, Reservation b)
    {
        if (a.IsIndefinite || b.IsIndefinite) return false;
        return a.StartDate <= b.EndDate!.Value && b.StartDate <= a.EndDate!.Value;
    }

    public IActionResult OnPostDeleteReservation(int id, int reservationId)
    {
        reservationRepository.Delete(reservationId);
        TempData["Message"] = "予約を取り消しました。";
        return RedirectToPage(new { id });
    }
}
