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
        cmd.CommandText = "SELECT Id, Name, Capacity, CageType, GroupName, GroupOrder, Notes FROM Cages;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Cage>();
        while (reader.Read())
            result.Add(Map(reader));
        result.Sort((a, b) => CompareNatural(a.Name, b.Name));
        return result;
    }

    // 「2」が「10」より前に来るように、名前に含まれる数字を数値として比較する自然順ソート
    public static int CompareNatural(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int startI = i, startJ = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                var numA = a[startI..i].TrimStart('0');
                var numB = b[startJ..j].TrimStart('0');
                if (numA.Length != numB.Length) return numA.Length - numB.Length;
                var numCompare = string.CompareOrdinal(numA, numB);
                if (numCompare != 0) return numCompare;
            }
            else
            {
                var charCompare = a[i].CompareTo(b[j]);
                if (charCompare != 0) return charCompare;
                i++; j++;
            }
        }
        return (a.Length - i) - (b.Length - j);
    }

    public Cage? GetById(int id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Capacity, CageType, GroupName, GroupOrder, Notes FROM Cages WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Insert(Cage cage)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Cages (Name, Capacity, CageType, GroupName, GroupOrder, Notes) VALUES ($name, $capacity, $cageType, $groupName, $groupOrder, $notes);
            SELECT last_insert_rowid();
            """;
        AddParams(cmd, cage);
        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    public void Update(Cage cage)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Cages SET Name = $name, Capacity = $capacity, CageType = $cageType, GroupName = $groupName, GroupOrder = $groupOrder, Notes = $notes WHERE Id = $id;";
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
        cmd.Parameters.AddWithValue("$cageType", cage.Type.ToString());
        cmd.Parameters.AddWithValue("$groupName", (object?)cage.GroupName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$groupOrder", cage.GroupOrder);
        cmd.Parameters.AddWithValue("$notes", (object?)cage.Notes ?? DBNull.Value);
    }

    private static Cage Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Capacity = reader.GetInt32(2),
        Type = Enum.TryParse<CageType>(reader.GetString(3), out var type) ? type : CageType.通常籠,
        GroupName = reader.IsDBNull(4) ? "" : reader.GetString(4),
        GroupOrder = reader.GetInt32(5),
        Notes = reader.IsDBNull(6) ? "" : reader.GetString(6),
    };
}
