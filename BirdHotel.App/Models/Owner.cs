namespace BirdHotel.App.Models;

public class Owner
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";

    // 経営者本人（この飼い主の鳥は予約時に自動で「期間なし」になる）
    public bool IsProprietor { get; set; }
    public string Notes { get; set; } = "";

    public override string ToString() => IsProprietor ? $"{Name}（経営者）" : Name;
}
