using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BirdHotel.Web.Pages.Reservations;

public class CreateModel(
    BirdRepository birdRepository,
    OwnerRepository ownerRepository,
    CageRepository cageRepository,
    ReservationRepository reservationRepository) : PageModel
{
    public record CageAvailability(Cage Cage, int Occupied, int Remaining, string Note);

    public List<Bird> Birds { get; private set; } = [];
    public List<Owner> Owners { get; private set; } = [];
    public List<CageAvailability> Availabilities { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? OwnerFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CageId { get; set; }

    [BindProperty]
    public int[] BirdIds { get; set; } = [];

    [BindProperty]
    public string StartDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    [BindProperty]
    public string? EndDate { get; set; } = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

    [BindProperty]
    public bool Indefinite { get; set; }

    [BindProperty]
    public bool OverrideCapacity { get; set; }

    [BindProperty]
    public string? Notes { get; set; }

    public void OnGet() => Load();

    private void Load()
    {
        Owners = ownerRepository.GetAll();
        Birds = birdRepository.GetAll();
        if (OwnerFilter is { } ownerId)
            Birds = Birds.Where(b => b.OwnerId == ownerId).ToList();

        BuildAvailability();
    }

    private void BuildAvailability()
    {
        if (!DateTime.TryParse(StartDate, out var start)) start = DateTime.Today;
        DateTime? end = Indefinite || !DateTime.TryParse(EndDate, out var endValue) ? null : endValue;

        var selectedBirds = BirdIds.Select(birdRepository.GetById).Where(b => b is not null).ToList();
        var forProprietor = selectedBirds.Any(b => b!.IsProprietorBird);

        // 経営者の鳥は末尾が1・2の籠を優先し、末尾が5・6の籠は最後に回す
        Availabilities = cageRepository.GetAll()
            .OrderBy(c => c.AssignmentPriority(forProprietor))
            .Select(cage =>
            {
                var occupied = reservationRepository.CountOverlapping(cage.Id, start, end);
                // 鳥が1羽でもいる期間は、定員に関わらず「空きなし」として扱う
                var remaining = occupied > 0 ? 0 : cage.Capacity;
                var note = cage.IsLastResort ? "空きが無い時のみ"
                    : cage.IsProprietorPreferred ? "経営者の鳥を優先"
                    : "";
                return new CageAvailability(cage, occupied, remaining, note);
            })
            .ToList();
    }

    public IActionResult OnPostSearch()
    {
        Load();
        return Page();
    }

    public IActionResult OnPostRegister(int cageId)
    {
        Load();

        var birds = BirdIds.Select(birdRepository.GetById).Where(b => b is not null).Select(b => b!).ToList();
        if (birds.Count == 0)
        {
            TempData["Error"] = "鳥を1羽以上選んでください。";
            return Page();
        }

        var cage = cageRepository.GetById(cageId);
        if (cage is null)
        {
            TempData["Error"] = "籠を選んでください。";
            return Page();
        }

        if (!DateTime.TryParse(StartDate, out var start))
        {
            TempData["Error"] = "開始日を入力してください。";
            return Page();
        }

        DateTime? end = null;
        if (!Indefinite)
        {
            if (!DateTime.TryParse(EndDate, out var endValue))
            {
                TempData["Error"] = "終了日を入力してください（期間なしの場合はチェックを付けてください）。";
                return Page();
            }
            if (endValue < start)
            {
                TempData["Error"] = "終了日は開始日以降にしてください。";
                return Page();
            }
            end = endValue;
        }

        // 既に鳥がいるか、今回まとめて入れる羽数が定員を超える場合は特別対応が必要
        var occupied = reservationRepository.CountOverlapping(cage.Id, start, end);
        if ((occupied > 0 || occupied + birds.Count > cage.Capacity) && !OverrideCapacity)
        {
            TempData["Error"] = $"「{cage.Name}」は選択期間中に既に鳥がいるか、定員（{cage.Capacity}羽）を超えます。特別対応で登録する場合は「定員を超えても登録する」にチェックしてください。";
            return Page();
        }

        foreach (var bird in birds)
        {
            reservationRepository.Insert(new Reservation
            {
                BirdId = bird.Id,
                CageId = cage.Id,
                StartDate = start,
                EndDate = end,
                Notes = Notes ?? "",
            });
        }

        TempData["Message"] = $"「{string.Join("、", birds.Select(b => b.Name))}」を「{cage.Name}」に登録しました。";
        return RedirectToPage("/Cages/Detail", new { id = cage.Id });
    }
}
