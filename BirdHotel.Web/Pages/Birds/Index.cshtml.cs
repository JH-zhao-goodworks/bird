using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BirdHotel.Web.Pages.Birds;

public class IndexModel(
    BirdRepository birdRepository,
    OwnerRepository ownerRepository,
    ReservationRepository reservationRepository) : PageModel
{
    public record PairGroup(string Label, List<Bird> Birds);

    public List<PairGroup> Groups { get; private set; } = [];
    public List<Owner> Owners { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? OwnerFilter { get; set; }

    public void OnGet()
    {
        Owners = ownerRepository.GetAll();

        var birds = birdRepository.GetAll();
        if (OwnerFilter is { } ownerId)
            birds = birds.Where(b => b.OwnerId == ownerId).ToList();

        // ペア可の鳥はペア名ごとにまとめ、ペア不可の鳥は1羽ずつ独立して並べる
        var groups = new List<PairGroup>();
        var pairGroups = new Dictionary<string, PairGroup>(StringComparer.Ordinal);

        foreach (var bird in birds)
        {
            if (bird.CanPair && bird.PairName.Length > 0)
            {
                if (!pairGroups.TryGetValue(bird.PairName, out var group))
                {
                    group = new PairGroup(bird.PairName, []);
                    pairGroups[bird.PairName] = group;
                    groups.Add(group);
                }
                group.Birds.Add(bird);
            }
            else
            {
                groups.Add(new PairGroup("X", [bird]));
            }
        }

        Groups = groups;
    }

    public IActionResult OnPostDelete(int id)
    {
        var reservationCount = reservationRepository.GetByBird(id).Count;
        birdRepository.Delete(id);
        TempData["Message"] = reservationCount > 0
            ? $"鳥を削除しました（関連する予約{reservationCount}件も削除されました）。"
            : "鳥を削除しました。";
        return RedirectToPage();
    }
}
