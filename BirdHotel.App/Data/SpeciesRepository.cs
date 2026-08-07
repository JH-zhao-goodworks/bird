using Microsoft.Data.Sqlite;

namespace BirdHotel.App.Data;

public class SpeciesRepository
{
    private readonly DatabaseService _db;

    public SpeciesRepository(DatabaseService db) => _db = db;

    public List<Models.Species> GetAll()
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Species ORDER BY Name;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Models.Species>();
        while (reader.Read())
            result.Add(new Models.Species { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        return result;
    }

    public int Insert(Models.Species species)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Species (Name) VALUES ($name);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$name", species.Name);
        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    public void Update(Models.Species species)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Species SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", species.Name);
        cmd.Parameters.AddWithValue("$id", species.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Species WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
