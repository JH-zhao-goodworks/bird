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

    // 同じグループ名の籠は、ホーム画面でひとまとまりの枠に表示される
    public string GroupName { get; set; } = "";

    // ホーム画面でのグループの並び順（同じグループの籠は同じ値を持つ。0は未設定）
    public int GroupOrder { get; set; }
    public string Notes { get; set; } = "";

    public bool IsAutoAssignable => Type == CageType.通常籠;

    // 籠名の末尾の数字（「A-1」なら1、「3」なら3）。予約の優先順位判定に使う。
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

    // 数値が小さいほど優先。予約を入れる籠を選ぶ順番に使う。
    public int AssignmentPriority(bool forProprietorBird)
    {
        if (IsLastResort) return 3;
        if (forProprietorBird) return IsProprietorPreferred ? 0 : 2;
        return IsProprietorPreferred ? 2 : 1;
    }

    public override string ToString() => Type == CageType.通常籠 ? Name : $"{Name}（{Type}）";
}
