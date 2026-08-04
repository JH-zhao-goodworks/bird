namespace BirdHotel.App.Models;

public class Cage
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Capacity { get; set; } = 2;
    public string Notes { get; set; } = "";

    public override string ToString() => Name;
}
