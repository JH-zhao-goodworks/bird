using BirdHotel.Web.Data;
using BirdHotel.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpeciesEntity = BirdHotel.Web.Models.Species;

namespace BirdHotel.Web.Pages.Birds;

public class EditModel(
    BirdRepository birdRepository,
    OwnerRepository ownerRepository,
    SpeciesRepository speciesRepository) : PageModel
{
    public List<Owner> Owners { get; private set; } = [];
    public List<SpeciesEntity> SpeciesList { get; private set; } = [];

    [BindProperty]
    public Bird Input { get; set; } = new();

    [BindProperty]
    public string? BirthDateText { get; set; }

    public IActionResult OnGet(int? id)
    {
        LoadLookups();

        if (id is { } birdId)
        {
            var bird = birdRepository.GetById(birdId);
            if (bird is null) return NotFound();
            Input = bird;
            BirthDateText = bird.BirthDate?.ToString("yyyy-MM-dd");
        }

        return Page();
    }

    private void LoadLookups()
    {
        Owners = ownerRepository.GetAll();
        SpeciesList = speciesRepository.GetAll();
    }

    public IActionResult OnPost()
    {
        LoadLookups();

        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            TempData["Error"] = "名前を入力してください。";
            return Page();
        }
        if (string.IsNullOrWhiteSpace(Input.Species))
        {
            TempData["Error"] = "種類を選んでください。";
            return Page();
        }
        if (Input.OwnerId is null)
        {
            TempData["Error"] = "飼い主を選んでください。";
            return Page();
        }
        if (Input.CanPair && string.IsNullOrWhiteSpace(Input.PairName))
        {
            TempData["Error"] = "ペア可の場合はペア名を入力してください。同じ籠に入れる鳥同士に同じペア名を付けます。";
            return Page();
        }

        Input.Name = Input.Name.Trim();
        Input.PairName = Input.CanPair ? (Input.PairName ?? "").Trim() : "";
        Input.BirthDate = DateTime.TryParse(BirthDateText, out var birthDate) ? birthDate : null;

        if (Input.Id == 0)
        {
            birdRepository.Insert(Input);
            TempData["Message"] = $"「{Input.Name}」を登録しました。";
        }
        else
        {
            birdRepository.Update(Input);
            TempData["Message"] = $"「{Input.Name}」を更新しました。";
        }

        return RedirectToPage("/Birds/Index");
    }
}
