namespace BirdHotel.App.Models;

public enum BirdSize
{
    中小型,
    中大型
}

public enum BirdGender
{
    オス,
    メス,
    不明
}

public class Bird
{
    public int Id { get; set; }
    public string Species { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime? BirthDate { get; set; }
    public BirdSize Size { get; set; } = BirdSize.中小型;
    public BirdGender Gender { get; set; } = BirdGender.不明;
    public int? OwnerId { get; set; }

    // 他の鳥と同じ籠に入れてよいか（ペア可）と、そのペア（同居グループ）の名前
    public bool CanPair { get; set; }
    public string PairName { get; set; } = "";
    public string Notes { get; set; } = "";

    // 表示用（DBには保存しない、Ownersテーブルとの結合結果を保持する）
    public string OwnerName { get; set; } = "";
    public bool IsProprietorBird { get; set; }

    public override string ToString() => $"{Name}（{Species}）";
}
