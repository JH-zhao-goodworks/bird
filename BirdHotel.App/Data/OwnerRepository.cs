using BirdHotel.App.Models;
using Microsoft.Data.Sqlite;

namespace BirdHotel.App.Data;

public class OwnerRepository
{
    private readonly DatabaseService _db;

    public OwnerRepository(DatabaseService db) => _db = db;

    public List<Owner> GetAll()
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Contact, IsProprietor, Notes FROM Owners ORDER BY IsProprietor DESC, Id;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Owner>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public Owner? GetById(int id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Contact, IsProprietor, Notes FROM Owners WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Insert(Owner owner)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Owners (Name, Contact, IsProprietor, Notes) VALUES ($name, $contact, $isProprietor, $notes);
            SELECT last_insert_rowid();
            """;
        AddParams(cmd, owner);
        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    public void Update(Owner owner)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Owners SET Name = $name, Contact = $contact, IsProprietor = $isProprietor, Notes = $notes WHERE Id = $id;";
        AddParams(cmd, owner);
        cmd.Parameters.AddWithValue("$id", owner.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Owners WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static void AddParams(SqliteCommand cmd, Owner owner)
    {
        cmd.Parameters.AddWithValue("$name", owner.Name);
        cmd.Parameters.AddWithValue("$contact", (object?)owner.Contact ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$isProprietor", owner.IsProprietor ? 1 : 0);
        cmd.Parameters.AddWithValue("$notes", (object?)owner.Notes ?? DBNull.Value);
    }

    private static Owner Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Contact = reader.IsDBNull(2) ? "" : reader.GetString(2),
        IsProprietor = reader.GetInt32(3) != 0,
        Notes = reader.IsDBNull(4) ? "" : reader.GetString(4),
    };
}
