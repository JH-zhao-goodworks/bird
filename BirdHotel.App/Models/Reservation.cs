namespace BirdHotel.App.Models;

public class Reservation
{
    public int Id { get; set; }
    public int BirdId { get; set; }
    public int CageId { get; set; }
    public DateTime StartDate { get; set; }

    // null = 期間なし（経営者自身の鳥など、退室日未定の長期滞在）
    public DateTime? EndDate { get; set; }
    public string Notes { get; set; } = "";

    // 表示用（DBには保存しない、一覧表示のために結合結果を保持する）
    public string BirdName { get; set; } = "";
    public string CageName { get; set; } = "";

    public bool IsIndefinite => EndDate is null;

    public bool OverlapsWith(DateTime otherStart, DateTime? otherEnd)
    {
        var thisEnd = EndDate ?? DateTime.MaxValue;
        var otherEndValue = otherEnd ?? DateTime.MaxValue;
        return StartDate <= otherEndValue && otherStart <= thisEnd;
    }
}
