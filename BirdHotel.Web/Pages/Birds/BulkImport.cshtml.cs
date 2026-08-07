using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpeciesEntity = BirdHotel.Web.Models.Species;

namespace BirdHotel.Web.Pages.Birds;

public class BulkImportModel(
    BirdRepository birdRepository,
    OwnerRepository ownerRepository,
    SpeciesRepository speciesRepository) : PageModel
{
    public record ParsedRow(string Species, string Name, string Owner, bool CanPair, string PairName, string Status, bool HasError);

    [BindProperty]
    public string InputText { get; set; } = "";

    public List<ParsedRow> Rows { get; private set; } = [];

    public void OnGet()
    {
    }

    public void OnPostPreview()
    {
        Rows = Parse(InputText);
    }

    public IActionResult OnPostRegister()
    {
        var rows = Parse(InputText);
        var valid = rows.Where(r => !r.HasError).ToList();
        if (valid.Count == 0)
        {
            TempData["Error"] = "登録できる行がありません。";
            Rows = rows;
            return Page();
        }

        var existingSpeciesNames = new HashSet<string>(speciesRepository.GetAll().Select(s => s.Name), StringComparer.Ordinal);
        var ownersByName = ownerRepository.GetAll().ToDictionary(o => o.Name, o => o.Id, StringComparer.Ordinal);
        var existingBirds = birdRepository.GetAll();

        var added = 0;
        var updated = 0;

        foreach (var row in valid)
        {
            if (existingSpeciesNames.Add(row.Species))
                speciesRepository.Insert(new SpeciesEntity { Name = row.Species });

            if (!ownersByName.TryGetValue(row.Owner, out var ownerId))
            {
                ownerId = ownerRepository.Insert(new Owner { Name = row.Owner });
                ownersByName[row.Owner] = ownerId;
            }

            // 名前と飼い主が同じ鳥は二重登録せず、ペアが不可から可になる場合だけ更新する
            var existing = existingBirds.FirstOrDefault(b => b.Name == row.Name && b.OwnerName == row.Owner);
            if (existing is not null)
            {
                if (row.CanPair && (!existing.CanPair || existing.PairName != row.PairName))
                {
                    existing.CanPair = true;
                    existing.PairName = row.PairName;
                    birdRepository.Update(existing);
                    updated++;
                }
                continue;
            }

            var bird = new Bird
            {
                Species = row.Species,
                Name = row.Name,
                Size = BirdSize.中小型,
                Gender = BirdGender.不明,
                OwnerId = ownerId,
                CanPair = row.CanPair,
                PairName = row.PairName,
            };
            bird.Id = birdRepository.Insert(bird);
            bird.OwnerName = row.Owner;
            existingBirds.Add(bird);
            added++;
        }

        TempData["Message"] = $"新規登録{added}件 / 既存の鳥を更新{updated}件 で登録しました。";
        return RedirectToPage("/Birds/Index");
    }

    private List<ParsedRow> Parse(string text)
    {
        var existingSpeciesNames = new HashSet<string>(speciesRepository.GetAll().Select(s => s.Name), StringComparer.Ordinal);
        var existingOwnerNames = new HashSet<string>(ownerRepository.GetAll().Select(o => o.Name), StringComparer.Ordinal);
        var existingBirds = birdRepository.GetAll();

        var rows = new List<ParsedRow>();
        foreach (var rawLine in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var parts = (line.Contains('\t') ? line.Split('\t') : line.Split(',')).Select(p => p.Trim()).ToArray();
            string Get(int i) => i < parts.Length ? parts[i] : "";

            var species = Get(0);
            var name = Get(1);
            var owner = Get(2);
            var pairText = Get(3);
            var pairName = Get(4);

            if (species == "種類" && name == "名前") continue; // ヘッダー行らしき行はスキップ

            if (species.Length == 0) { rows.Add(new ParsedRow(species, name, owner, false, pairName, "エラー: 種類が空です", true)); continue; }
            if (name.Length == 0) { rows.Add(new ParsedRow(species, name, owner, false, pairName, "エラー: 名前が空です", true)); continue; }
            if (owner.Length == 0) { rows.Add(new ParsedRow(species, name, owner, false, pairName, "エラー: 飼い主が空です", true)); continue; }

            if (!TryParsePairFlag(pairText, pairName, out var canPair))
            {
                rows.Add(new ParsedRow(species, name, owner, false, pairName, "エラー: ペアの欄は「可」または「不可」で入力してください", true));
                continue;
            }
            if (canPair && pairName.Length == 0)
            {
                rows.Add(new ParsedRow(species, name, owner, true, pairName, "エラー: ペア可の場合はペア名を入力してください", true));
                continue;
            }

            var effectivePairName = canPair ? pairName : "";
            var existing = existingBirds.FirstOrDefault(b => b.Name == name && b.OwnerName == owner);

            string status;
            if (existing is not null)
            {
                status = canPair && (!existing.CanPair || existing.PairName != effectivePairName)
                    ? $"既に登録済み → ペアを可（{effectivePairName}）に更新"
                    : "既に登録済み（変更なし）";
            }
            else
            {
                var notes = new List<string>();
                if (!existingSpeciesNames.Contains(species)) { notes.Add("種類を新規登録"); existingSpeciesNames.Add(species); }
                if (!existingOwnerNames.Contains(owner)) { notes.Add("飼い主を新規登録"); existingOwnerNames.Add(owner); }
                status = notes.Count == 0 ? "新規登録" : "新規登録 / " + string.Join(" / ", notes);
            }

            rows.Add(new ParsedRow(species, name, owner, canPair, effectivePairName, status, false));
        }

        return rows;
    }

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
}
