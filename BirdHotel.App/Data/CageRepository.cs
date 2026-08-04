using BirdHotel.App.Models;
using Microsoft.Data.Sqlite;

namespace BirdHotel.App.Data;

public class CageRepository
{
    private readonly DatabaseService _db;

    public CageRepository(DatabaseService db) => _db = db;

    public List<Cage> GetAll()
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Capacity, Notes FROM Cages ORDER BY Id;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Cage>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public Cage? GetById(int id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Capacity, Notes FROM Cages WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Insert(Cage cage)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Cages (Name, Capacity, Notes) VALUES ($name, $capacity, $notes);
            SELECT last_insert_rowid();
            """;
        AddParams(cmd, cage);
        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    public void Update(Cage cage)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Cages SET Name = $name, Capacity = $capacity, Notes = $notes WHERE Id = $id;";
        AddParams(cmd, cage);
        cmd.Parameters.AddWithValue("$id", cage.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Cages WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static void AddParams(SqliteCommand cmd, Cage cage)
    {
        cmd.Parameters.AddWithValue("$name", cage.Name);
        cmd.Parameters.AddWithValue("$capacity", cage.Capacity);
        cmd.Parameters.AddWithValue("$notes", (object?)cage.Notes ?? DBNull.Value);
    }

    private static Cage Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Capacity = reader.GetInt32(2),
        Notes = reader.IsDBNull(3) ? "" : reader.GetString(3),
    };
}
