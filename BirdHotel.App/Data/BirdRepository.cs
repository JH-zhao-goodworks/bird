using BirdHotel.App.Models;
using Microsoft.Data.Sqlite;

namespace BirdHotel.App.Data;

public class BirdRepository
{
    private readonly DatabaseService _db;

    public BirdRepository(DatabaseService db) => _db = db;

    private const string BaseSelect = """
        SELECT b.Id, b.Species, b.Name, b.BirthDate, b.Size, b.Gender, b.OwnerId, b.Notes,
               IFNULL(o.Name, ''), IFNULL(o.IsProprietor, 0), b.CanPair, IFNULL(b.PairName, '')
        FROM Birds b
        LEFT JOIN Owners o ON o.Id = b.OwnerId
        """;

    public List<Bird> GetAll()
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " ORDER BY b.Id;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Bird>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public List<Bird> GetByOwner(int ownerId)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " WHERE b.OwnerId = $ownerId ORDER BY b.Id;";
        cmd.Parameters.AddWithValue("$ownerId", ownerId);
        using var reader = cmd.ExecuteReader();
        var result = new List<Bird>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public Bird? GetById(int id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " WHERE b.Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Insert(Bird bird)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Birds (Species, Name, BirthDate, Size, Gender, OwnerId, CanPair, PairName, Notes)
            VALUES ($species, $name, $birthDate, $size, $gender, $ownerId, $canPair, $pairName, $notes);
            SELECT last_insert_rowid();
            """;
        AddParams(cmd, bird);
        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    public void Update(Bird bird)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE Birds SET
                Species = $species, Name = $name, BirthDate = $birthDate, Size = $size,
                Gender = $gender, OwnerId = $ownerId, CanPair = $canPair, PairName = $pairName, Notes = $notes
            WHERE Id = $id;
            """;
        AddParams(cmd, bird);
        cmd.Parameters.AddWithValue("$id", bird.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Birds WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static void AddParams(SqliteCommand cmd, Bird bird)
    {
        cmd.Parameters.AddWithValue("$species", bird.Species);
        cmd.Parameters.AddWithValue("$name", bird.Name);
        cmd.Parameters.AddWithValue("$birthDate", (object?)bird.BirthDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$size", bird.Size.ToString());
        cmd.Parameters.AddWithValue("$gender", bird.Gender.ToString());
        cmd.Parameters.AddWithValue("$ownerId", (object?)bird.OwnerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$canPair", bird.CanPair ? 1 : 0);
        cmd.Parameters.AddWithValue("$pairName", (object?)bird.PairName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", (object?)bird.Notes ?? DBNull.Value);
    }

    private static Bird Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Species = reader.GetString(1),
        Name = reader.GetString(2),
        BirthDate = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
        Size = Enum.Parse<BirdSize>(reader.GetString(4)),
        Gender = Enum.Parse<BirdGender>(reader.GetString(5)),
        OwnerId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        Notes = reader.IsDBNull(7) ? "" : reader.GetString(7),
        OwnerName = reader.GetString(8),
        IsProprietorBird = reader.GetInt32(9) != 0,
        CanPair = reader.GetInt32(10) != 0,
        PairName = reader.GetString(11),
    };
}
