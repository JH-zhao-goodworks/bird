using BirdHotel.App.Models;
using ClosedXML.Excel;

namespace BirdHotel.App.Data;

public class ReservationExportService
{
    private readonly BirdRepository _birdRepository;
    private readonly ReservationRepository _reservationRepository;

    public ReservationExportService(BirdRepository birdRepository, ReservationRepository reservationRepository)
    {
        _birdRepository = birdRepository;
        _reservationRepository = reservationRepository;
    }

    // 予約が入っている鳥を、籠に関係なくすべてExcelに書き出す。戻り値は書き出した行数。
    public int ExportToExcel(string filePath)
    {
        var birdsById = _birdRepository.GetAll().ToDictionary(b => b.Id);
        var reservations = _reservationRepository.GetAll()
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.BirdName)
            .ToList();

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("予約一覧");

        string[] headers = ["鳥名前", "種類", "飼い主", "ペア可否", "ペア名", "開始日", "終了日"];
        for (var i = 0; i < headers.Length; i++)
        {
            var headerCell = sheet.Cell(1, i + 1);
            headerCell.Value = headers[i];
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = 2;
        foreach (var reservation in reservations)
        {
            birdsById.TryGetValue(reservation.BirdId, out var bird);

            sheet.Cell(row, 1).Value = reservation.BirdName;
            sheet.Cell(row, 2).Value = bird?.Species ?? "";
            sheet.Cell(row, 3).Value = reservation.OwnerName;
            sheet.Cell(row, 4).Value = bird is not null && bird.CanPair ? "可" : "不可";
            sheet.Cell(row, 5).Value = bird is not null && bird.CanPair && bird.PairName.Length > 0 ? bird.PairName : "X";
            sheet.Cell(row, 6).Value = reservation.StartDate.ToString("yyyy/MM/dd");
            sheet.Cell(row, 7).Value = reservation.IsIndefinite ? "無期限" : reservation.EndDate!.Value.ToString("yyyy/MM/dd");
            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);

        return reservations.Count;
    }
}
