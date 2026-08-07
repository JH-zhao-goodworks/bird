using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BirdHotel.Web.Pages.Reservations;

public class IndexModel(ReservationRepository reservationRepository) : PageModel
{
    public List<Reservation> Reservations { get; private set; } = [];

    public void OnGet()
    {
        Reservations = reservationRepository.GetAll();
    }

    public IActionResult OnPostDelete(int id)
    {
        reservationRepository.Delete(id);
        TempData["Message"] = "予約を取り消しました。";
        return RedirectToPage();
    }
}
