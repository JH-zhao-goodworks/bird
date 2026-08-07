using BirdHotel.App.Data;
using BirdHotel.App.Forms;

namespace BirdHotel.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var db = new DatabaseService();
        var ownerRepository = new OwnerRepository(db);
        var speciesRepository = new SpeciesRepository(db);
        var birdRepository = new BirdRepository(db);
        var cageRepository = new CageRepository(db);
        var reservationRepository = new ReservationRepository(db);

        Application.Run(new MainForm(birdRepository, cageRepository, reservationRepository, ownerRepository, speciesRepository));
    }
}
