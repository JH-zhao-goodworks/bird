using System.Globalization;
using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpeciesEntity = BirdHotel.Web.Models.Species;

namespace BirdHotel.Web.Pages.Reservations;

public class BulkImportModel(
    BirdRepository birdRepository,
    OwnerRepository ownerRepository,
    SpeciesRepository speciesRepository,
    CageRepository cageRepository,
    ReservationRepository reservationRepository) : PageModel
{
    public record ParsedRow(int LineNumber, string Name, string Species, string Owner, bool CanPair, string PairName,
        DateTime Start, DateTime? End, string? Error)
    {
        // ペア可の鳥はペア名ごとに同じ籠へまとめる。ペア不可の鳥は1羽ずつ別の籠になる。
        public string GroupKey => CanPair && PairName.Length > 0 ? PairName : "";
    }

    public record GroupPlan(List<ParsedRow> Members, Cage? Cage, string Status);

    [BindProperty]
    public string InputText { get; set; } = "";

    public List<GroupPlan> Plans { get; private set; } = [];
    public List<ParsedRow> ErrorRows { get; private set; } = [];

    public void OnGet()
    {
    }

    public void OnPostPreview() => BuildPlan();

    public IActionResult OnPostRegister()
    {
        BuildPlan();

        var assignable = Plans.Where(p => p.Cage is not null).ToList();
        if (assignable.Count == 0)
        {
            TempData["Error"] = "配属できるグループがありません。";
            return Page();
        }

        var existingSpeciesNames = new HashSet<string>(speciesRepository.GetAll().Select(s => s.Name), StringComparer.Ordinal);
        var ownersByName = ownerRepository.GetAll().ToDictionary(o => o.Name, o => o.Id, StringComparer.Ordinal);
        var existingBirds = birdRepository.GetAll();
        var replaced = 0;
        var totalBirds = 0;

        foreach (var plan in assignable)
        {
            foreach (var member in plan.Members)
            {
                if (existingSpeciesNames.Add(member.Species))
                    speciesRepository.Insert(new SpeciesEntity { Name = member.Species });

                if (!ownersByName.TryGetValue(member.Owner, out var ownerId))
                {
                    ownerId = ownerRepository.Insert(new Owner { Name = member.Owner });
                    ownersByName[member.Owner] = ownerId;
                }

                // 同じ名前・同じ飼い主の鳥が既にいればそれを使う（二重登録を防ぐ）
                var existingBird = existingBirds.FirstOrDefault(b => b.Name == member.Name && b.OwnerName == member.Owner);
                int birdId;
                if (existingBird is not null)
                {
                    birdId = existingBird.Id;

                    // 同じ期間の予約が既にあれば、消してから登録し直す
                    foreach (var reservation in reservationRepository.GetByBird(birdId))
                    {
                        if (reservation.StartDate.Date == member.Start.Date && reservation.EndDate?.Date == member.End?.Date)
                        {
                            reservationRepository.Delete(reservation.Id);
                            replaced++;
                        }
                    }
                }
                else
                {
                    var bird = new Bird
                    {
                        Species = member.Species,
                        Name = member.Name,
                        Size = BirdSize.中小型,
                        Gender = BirdGender.不明,
                        OwnerId = ownerId,
                        CanPair = member.CanPair,
                        PairName = member.PairName,
                    };
                    birdId = birdRepository.Insert(bird);
                    bird.Id = birdId;
                    bird.OwnerName = member.Owner;
                    existingBirds.Add(bird);
                }

                reservationRepository.Insert(new Reservation
                {
                    BirdId = birdId,
                    CageId = plan.Cage!.Id,
                    StartDate = member.Start,
                    EndDate = member.End,
                });
                totalBirds++;
            }
        }

        TempData["Message"] = replaced > 0
            ? $"{assignable.Count}グループ（{totalBirds}羽）を登録しました（うち{replaced}件は同じ鳥・同じ期間の既存予約を置き換えました）。"
            : $"{assignable.Count}グループ（{totalBirds}羽）を登録しました。";
        return RedirectToPage("/Index");
    }

    private void BuildPlan()
    {
        var validRows = new List<ParsedRow>();
        ErrorRows = [];

        var lineNumber = 0;
        foreach (var rawLine in (InputText ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var parts = (line.Contains('\t') ? line.Split('\t') : line.Split(',')).Select(p => p.Trim()).ToArray();
            string Get(int i) => i < parts.Length ? parts[i] : "";

            var name = Get(0);
            var species = Get(1);
            var owner = Get(2);
            var pairText = Get(3);
            var pairName = Get(4);
            var startText = Get(5);
            var endText = Get(6);

            if (name is "鳥名前" or "名前" && species == "種類") continue; // ヘッダー行はスキップ

            // 鳥名前の列が無く6列だけ貼られた場合は、どの列が足りないかを具体的に伝える
            if (parts.Length == 6 && TryParseDate(Get(4), out _) && !TryParseDate(Get(3), out _))
            {
                ErrorRows.Add(new ParsedRow(lineNumber, name, species, owner, false, pairName, default, null,
                    "鳥名前の列が抜けています（6列しかありません）。先頭に鳥の名前の列を足して7列にしてください"));
                continue;
            }

            var row = BuildRow(lineNumber, name, species, owner, pairText, pairName, startText, endText);
            if (row.Error is null) validRows.Add(row);
            else ErrorRows.Add(row);
        }

        Plans = PlanGroups(validRows);
    }

    private static ParsedRow BuildRow(int lineNumber, string name, string species, string owner,
        string pairText, string pairName, string startText, string endText)
    {
        ParsedRow Error(string message) => new(lineNumber, name, species, owner, false, pairName, default, null, message);

        if (name.Length == 0) return Error("名前が空です");
        if (species.Length == 0) return Error("種類が空です");
        if (owner.Length == 0) return Error("飼い主が空です");

        // 列が1つずれていると、日付がペア名の位置に来たり、可否が飼い主の位置に来たりする
        if (TryParseDate(pairName, out _))
            return Error("列がずれています（ペア名の位置に日付があります）。鳥名前, 種類, 飼い主, ペア可否, ペア名, 開始日, 終了日 の7列で貼り付けてください");
        if (IsPairKeyword(owner) || IsPairKeyword(species))
            return Error("列がずれています（種類か飼い主の位置に「可/不可」があります）");

        if (!TryParsePairFlag(pairText, pairName, out var canPair))
            return Error("ペア可否の欄は「可」または「不可」で入力してください");
        if (canPair && pairName.Length == 0)
            return Error("ペア可の場合はペア名を入力してください");

        if (!TryParseDate(startText, out var start))
            return Error("開始日が読み取れません");

        DateTime? end;
        if (endText.Length == 0 || endText is "無期限" or "むきげん")
            end = null;
        else if (TryParseDate(endText, out var endValue))
            end = endValue;
        else
            return Error("終了日が読み取れません");

        if (end is not null && end.Value < start)
            return Error("終了日が開始日より前です");

        return new ParsedRow(lineNumber, name, species, owner, canPair, canPair ? pairName : "", start, end, null);
    }

    private static bool IsPairKeyword(string text) => text is "可" or "不可" or "○" or "〇" or "◯" or "×";

    private static bool TryParsePairFlag(string text, string pairName, out bool canPair)
    {
        if (text.Length == 0)
        {
            canPair = pairName.Length > 0 && pairName != "X" && pairName != "x";
            return true;
        }

        switch (text)
        {
            case "可" or "○" or "〇" or "◯" or "はい" or "OK" or "ok" or "o" or "O" or "1":
                canPair = true;
                return true;
            case "不可" or "X" or "x" or "×" or "いいえ" or "NG" or "ng" or "-" or "0":
                canPair = false;
                return true;
            default:
                canPair = false;
                return false;
        }
    }

    private static bool TryParseDate(string text, out DateTime value)
    {
        string[] formats = ["yyyy/M/d", "yyyy-M-d", "yyyy/MM/dd", "yyyy-MM-dd"];
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            return true;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private List<GroupPlan> PlanGroups(List<ParsedRow> validRows)
    {
        // ペア名・開始日・終了日が一致する行をひとまとめにする
        var used = new bool[validRows.Count];
        var rawGroups = new List<(int FirstIndex, List<ParsedRow> Members)>();

        for (var i = 0; i < validRows.Count; i++)
        {
            if (used[i]) continue;
            var row = validRows[i];
            var members = new List<ParsedRow> { row };
            used[i] = true;

            if (row.GroupKey.Length > 0)
            {
                for (var j = i + 1; j < validRows.Count; j++)
                {
                    if (used[j]) continue;
                    var other = validRows[j];
                    if (other.GroupKey == row.GroupKey && other.Start == row.Start && other.End == row.End)
                    {
                        members.Add(other);
                        used[j] = true;
                    }
                }
            }

            rawGroups.Add((i, members));
        }

        // 経営者籠・持ち込み籠は決まった鳥専用のため、自動配属の候補にしない
        var cages = cageRepository.GetAll().Where(c => c.IsAutoAssignable).ToList();
        var birds = birdRepository.GetAll();
        var proprietorOwnerNames = new HashSet<string>(
            ownerRepository.GetAll().Where(o => o.IsProprietor).Select(o => o.Name), StringComparer.Ordinal);

        // 同じ鳥・同じ期間の既存予約は登録時に消して入れ直すので、空きとして扱う
        var replacedIds = new HashSet<int>();
        foreach (var row in validRows)
        {
            var bird = birds.FirstOrDefault(b => b.Name == row.Name && b.OwnerName == row.Owner);
            if (bird is null) continue;
            foreach (var reservation in reservationRepository.GetByBird(bird.Id))
                if (reservation.StartDate.Date == row.Start.Date && reservation.EndDate?.Date == row.End?.Date)
                    replacedIds.Add(reservation.Id);
        }

        var provisional = new List<(int CageId, DateTime Start, DateTime? End)>();
        foreach (var cage in cages)
            foreach (var reservation in reservationRepository.GetByCage(cage.Id))
                if (!replacedIds.Contains(reservation.Id))
                    provisional.Add((cage.Id, reservation.StartDate, reservation.EndDate));

        var plans = new List<GroupPlan>();
        foreach (var (_, members) in rawGroups.OrderBy(g => g.FirstIndex))
        {
            var start = members[0].Start;
            var end = members[0].End;
            var size = members.Count;

            // 経営者の鳥は末尾が1・2の籠を優先。末尾が5・6の籠はどのグループでも最後に回す。
            var isProprietorGroup = members.Any(m => proprietorOwnerNames.Contains(m.Owner));
            var cage = cages
                .OrderBy(c => c.AssignmentPriority(isProprietorGroup))
                .FirstOrDefault(c =>
                    size <= c.Capacity &&
                    !provisional.Any(p => p.CageId == c.Id && Overlaps(p.Start, p.End, start, end)));

            var replaceNote = members.Any(m =>
            {
                var bird = birds.FirstOrDefault(b => b.Name == m.Name && b.OwnerName == m.Owner);
                return bird is not null && reservationRepository.GetByBird(bird.Id)
                    .Any(r => r.StartDate.Date == m.Start.Date && r.EndDate?.Date == m.End?.Date);
            }) ? "（既存予約を置き換え）" : "";

            if (cage is null)
            {
                plans.Add(new GroupPlan(members, null, "自動配属できませんでした（空き籠なし）"));
            }
            else
            {
                provisional.Add((cage.Id, start, end));
                plans.Add(new GroupPlan(members, cage, $"「{cage.Name}」に配属{replaceNote}"));
            }
        }

        return plans;
    }

    private static bool Overlaps(DateTime aStart, DateTime? aEnd, DateTime bStart, DateTime? bEnd)
    {
        var aEndValue = aEnd ?? DateTime.MaxValue;
        var bEndValue = bEnd ?? DateTime.MaxValue;
        return aStart <= bEndValue && bStart <= aEndValue;
    }
}
