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

        var charges = BuildCharges(reservations, birdsById);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("予約一覧");

        string[] headers = ["鳥名前", "種類", "飼い主", "ペア可否", "ペア名", "開始日", "終了日", "日数", "詳細", "料金"];
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

            // 日数は初日を含めない数え方（8/1〜8/3 なら 2）
            if (reservation.IsIndefinite)
                sheet.Cell(row, 8).Value = "無期限";
            else
                sheet.Cell(row, 8).Value = (reservation.EndDate!.Value - reservation.StartDate).Days;

            var charge = charges[reservation.Id];
            sheet.Cell(row, 9).Value = charge.Detail;
            if (charge.Total is { } total)
                sheet.Cell(row, 10).Value = total;

            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);

        return reservations.Count;
    }

    private record Charge(string Detail, int? Total);

    // 料金は籠ごとに計算する。同じ籠に同じ期間で同室している鳥は、まとめて1件分だけ請求する。
    private static Dictionary<int, Charge> BuildCharges(List<Reservation> reservations, Dictionary<int, Bird> birdsById)
    {
        var charges = new Dictionary<int, Charge>();

        var groups = reservations.GroupBy(r => (r.CageId, r.StartDate, r.EndDate));
        foreach (var group in groups)
        {
            var members = group.ToList();
            var first = members[0];

            if (first.IsIndefinite)
            {
                // 期間が決まっていないもの（経営者の鳥など）は料金を出さない
                foreach (var member in members)
                    charges[member.Id] = new Charge("無期限", null);
                continue;
            }

            // 同室に中大型がいる場合は、大きい方の料金で計算する
            var size = members.Any(m => birdsById.TryGetValue(m.BirdId, out var b) && b.Size == BirdSize.中大型)
                ? BirdSize.中大型
                : BirdSize.中小型;

            var days = (first.EndDate!.Value - first.StartDate).Days;
            var (total, detail) = PricingCalculator.Calculate(days, size);

            charges[first.Id] = new Charge(detail, total);
            foreach (var member in members.Skip(1))
                charges[member.Id] = new Charge($"「{first.BirdName}」と同じ籠のため上の行に計上", 0);
        }

        return charges;
    }
}
