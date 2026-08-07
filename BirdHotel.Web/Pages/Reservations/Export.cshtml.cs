using BirdHotel.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BirdHotel.Web.Pages.Reservations;

public class ExportModel(ReservationRepository reservationRepository, ReservationExportService exportService) : PageModel
{
    public int ReservationCount { get; private set; }

    public void OnGet()
    {
        ReservationCount = reservationRepository.GetAll().Count;
    }

    public IActionResult OnPostDownload()
    {
        if (reservationRepository.GetAll().Count == 0)
        {
            TempData["Error"] = "出力できる予約がありません。";
            return RedirectToPage();
        }

        var bytes = exportService.BuildExcel();
        var fileName = $"予約一覧_{DateTime.Today:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
