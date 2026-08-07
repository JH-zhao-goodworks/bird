namespace BirdHotel.App.Models;

public class Species
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public override string ToString() => Name;
}
