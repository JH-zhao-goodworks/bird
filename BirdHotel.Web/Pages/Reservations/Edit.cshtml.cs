using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BirdHotel.Web.Pages.Reservations;

public class EditModel(CageRepository cageRepository, ReservationRepository reservationRepository) : PageModel
{
    public List<Cage> Cages { get; private set; } = [];
    public string BirdName { get; private set; } = "";

    [BindProperty]
    public int Id { get; set; }

    [BindProperty]
    public int CageId { get; set; }

    [BindProperty]
    public string StartDate { get; set; } = "";

    [BindProperty]
    public string? EndDate { get; set; }

    [BindProperty]
    public bool Indefinite { get; set; }

    [BindProperty]
    public string? Notes { get; set; }

    [BindProperty]
    public bool OverrideCapacity { get; set; }

    public IActionResult OnGet(int id)
    {
        var reservation = reservationRepository.GetById(id);
        if (reservation is null) return NotFound();

        Cages = cageRepository.GetAll();
        Id = reservation.Id;
        CageId = reservation.CageId;
        BirdName = reservation.BirdName;
        StartDate = reservation.StartDate.ToString("yyyy-MM-dd");
        EndDate = reservation.EndDate?.ToString("yyyy-MM-dd");
        Indefinite = reservation.IsIndefinite;
        Notes = reservation.Notes;

        return Page();
    }

    public IActionResult OnPost()
    {
        Cages = cageRepository.GetAll();

        var reservation = reservationRepository.GetById(Id);
        if (reservation is null) return NotFound();
        BirdName = reservation.BirdName;

        if (!DateTime.TryParse(StartDate, out var start))
        {
            TempData["Error"] = "開始日を入力してください。";
            return Page();
        }

        DateTime? end = null;
        if (!Indefinite)
        {
            if (!DateTime.TryParse(EndDate, out var endValue))
            {
                TempData["Error"] = "終了日を入力してください（期間なしの場合はチェックを付けてください）。";
                return Page();
            }
            if (endValue < start)
            {
                TempData["Error"] = "終了日は開始日以降にしてください。";
                return Page();
            }
            end = endValue;
        }

        // 自分自身の予約は除外して重複を判定する
        var occupied = reservationRepository.CountOverlapping(CageId, start, end, excludeReservationId: Id);
        if (occupied > 0 && !OverrideCapacity)
        {
            var cage = Cages.FirstOrDefault(c => c.Id == CageId);
            TempData["Error"] = $"「{cage?.Name}」は選択期間中に既に鳥がいます。特別対応で登録する場合は「定員を超えても登録する」にチェックしてください。";
            return Page();
        }

        reservation.CageId = CageId;
        reservation.StartDate = start;
        reservation.EndDate = end;
        reservation.Notes = Notes ?? "";
        reservationRepository.Update(reservation);

        TempData["Message"] = "予約を更新しました。";
        return RedirectToPage("/Reservations/Index");
    }

    public IActionResult OnPostDelete()
    {
        reservationRepository.Delete(Id);
        TempData["Message"] = "予約を取り消しました。";
        return RedirectToPage("/Reservations/Index");
    }
}
