namespace BirdHotel.App.Models;

public enum CageType
{
    通常籠,
    経営者籠,
    持ち込み籠
}

public class Cage
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Capacity { get; set; } = 2;

    // 経営者籠・持ち込み籠は決まった鳥専用のため、一括予約登録の自動配属では使わない
    public CageType Type { get; set; } = CageType.通常籠;
    public string Notes { get; set; } = "";

    public bool IsAutoAssignable => Type == CageType.通常籠;

    public override string ToString() => Type == CageType.通常籠 ? Name : $"{Name}（{Type}）";
}
