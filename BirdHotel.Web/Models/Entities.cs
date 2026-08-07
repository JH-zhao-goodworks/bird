namespace BirdHotel.Web.Models;

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

public enum CageType
{
    通常籠,
    経営者籠,
    持ち込み籠
}

public class Owner
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";

    // 経営者本人（この飼い主の鳥は予約時に自動で「期間なし」になる）
    public bool IsProprietor { get; set; }
    public string Notes { get; set; } = "";
}

public class Species
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
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

    // 表示用（飼い主テーブルとの結合結果）
    public string OwnerName { get; set; } = "";
    public bool IsProprietorBird { get; set; }
}

public class Cage
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Capacity { get; set; } = 2;

    // 経営者籠・持ち込み籠は決まった鳥専用のため、一括予約登録の自動配属では使わない
    public CageType Type { get; set; } = CageType.通常籠;

    // 同じグループ名の籠は、ホーム画面でひとまとまりに表示される
    public string GroupName { get; set; } = "";
    public int GroupOrder { get; set; }
    public string Notes { get; set; } = "";

    public bool IsAutoAssignable => Type == CageType.通常籠;

    // 籠名の末尾の数字（「A-1」なら1）。予約の優先順位判定に使う。
    public int TrailingNumber
    {
        get
        {
            var start = Name.Length;
            while (start > 0 && char.IsDigit(Name[start - 1]))
                start--;
            return start < Name.Length && int.TryParse(Name[start..], out var value) ? value : 0;
        }
    }

    // 末尾が1・2の籠は経営者の鳥用、末尾が5・6の籠は他に空きが無いときだけ使う
    public bool IsProprietorPreferred => TrailingNumber is 1 or 2;
    public bool IsLastResort => TrailingNumber is 5 or 6;

    public int AssignmentPriority(bool forProprietorBird)
    {
        if (IsLastResort) return 3;
        if (forProprietorBird) return IsProprietorPreferred ? 0 : 2;
        return IsProprietorPreferred ? 2 : 1;
    }
}

public class Reservation
{
    public int Id { get; set; }
    public int BirdId { get; set; }
    public int CageId { get; set; }
    public DateTime StartDate { get; set; }

    // null = 期間なし（経営者自身の鳥など、退室日未定の長期滞在）
    public DateTime? EndDate { get; set; }
    public string Notes { get; set; } = "";

    // 表示用（結合結果）
    public string BirdName { get; set; } = "";
    public string CageName { get; set; } = "";
    public int? OwnerId { get; set; }
    public string OwnerName { get; set; } = "";

    public bool IsIndefinite => EndDate is null;

    public bool OverlapsWith(DateTime otherStart, DateTime? otherEnd)
    {
        var thisEnd = EndDate ?? DateTime.MaxValue;
        var otherEndValue = otherEnd ?? DateTime.MaxValue;
        return StartDate <= otherEndValue && otherStart <= thisEnd;
    }
}
