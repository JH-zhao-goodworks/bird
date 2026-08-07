using BirdHotel.Web.Models;
using Npgsql;

namespace BirdHotel.Web.Data;

public class OwnerRepository(Database db)
{
    public List<Owner> GetAll()
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, contact, is_proprietor, notes FROM owners ORDER BY is_proprietor DESC, id;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Owner>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public Owner? GetById(int id)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, contact, is_proprietor, notes FROM owners WHERE id = @id;";
        cmd.Parameters.AddWithValue("id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Insert(Owner owner)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO owners (name, contact, is_proprietor, notes) VALUES (@name, @contact, @isProprietor, @notes) RETURNING id;";
        AddParams(cmd, owner);
        return (int)cmd.ExecuteScalar()!;
    }

    public void Update(Owner owner)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE owners SET name = @name, contact = @contact, is_proprietor = @isProprietor, notes = @notes WHERE id = @id;";
        AddParams(cmd, owner);
        cmd.Parameters.AddWithValue("id", owner.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM owners WHERE id = @id;";
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }

    private static void AddParams(NpgsqlCommand cmd, Owner owner)
    {
        cmd.Parameters.AddWithValue("name", owner.Name);
        cmd.Parameters.AddWithValue("contact", (object?)owner.Contact ?? DBNull.Value);
        cmd.Parameters.AddWithValue("isProprietor", owner.IsProprietor);
        cmd.Parameters.AddWithValue("notes", (object?)owner.Notes ?? DBNull.Value);
    }

    private static Owner Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Contact = reader.IsDBNull(2) ? "" : reader.GetString(2),
        IsProprietor = reader.GetBoolean(3),
        Notes = reader.IsDBNull(4) ? "" : reader.GetString(4),
    };
}

public class SpeciesRepository(Database db)
{
    public List<Species> GetAll()
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM species ORDER BY name;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Species>();
        while (reader.Read())
            result.Add(new Species { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        return result;
    }

    public int Insert(Species species)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO species (name) VALUES (@name) ON CONFLICT (name) DO UPDATE SET name = EXCLUDED.name RETURNING id;";
        cmd.Parameters.AddWithValue("name", species.Name);
        return (int)cmd.ExecuteScalar()!;
    }

    public void Update(Species species)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE species SET name = @name WHERE id = @id;";
        cmd.Parameters.AddWithValue("name", species.Name);
        cmd.Parameters.AddWithValue("id", species.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM species WHERE id = @id;";
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }
}

public class BirdRepository(Database db)
{
    private const string BaseSelect = """
        SELECT b.id, b.species, b.name, b.birth_date, b.size, b.gender, b.owner_id, b.notes,
               COALESCE(o.name, ''), COALESCE(o.is_proprietor, FALSE), b.can_pair, COALESCE(b.pair_name, '')
        FROM birds b
        LEFT JOIN owners o ON o.id = b.owner_id
        """;

    public List<Bird> GetAll()
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " ORDER BY b.id;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Bird>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public List<Bird> GetByOwner(int ownerId)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " WHERE b.owner_id = @ownerId ORDER BY b.id;";
        cmd.Parameters.AddWithValue("ownerId", ownerId);
        using var reader = cmd.ExecuteReader();
        var result = new List<Bird>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public Bird? GetById(int id)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " WHERE b.id = @id;";
        cmd.Parameters.AddWithValue("id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Insert(Bird bird)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO birds (species, name, birth_date, size, gender, owner_id, can_pair, pair_name, notes)
            VALUES (@species, @name, @birthDate, @size, @gender, @ownerId, @canPair, @pairName, @notes)
            RETURNING id;
            """;
        AddParams(cmd, bird);
        return (int)cmd.ExecuteScalar()!;
    }

    public void Update(Bird bird)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE birds SET species = @species, name = @name, birth_date = @birthDate, size = @size,
                gender = @gender, owner_id = @ownerId, can_pair = @canPair, pair_name = @pairName, notes = @notes
            WHERE id = @id;
            """;
        AddParams(cmd, bird);
        cmd.Parameters.AddWithValue("id", bird.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM birds WHERE id = @id;";
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }

    private static void AddParams(NpgsqlCommand cmd, Bird bird)
    {
        cmd.Parameters.AddWithValue("species", bird.Species);
        cmd.Parameters.AddWithValue("name", bird.Name);
        cmd.Parameters.AddWithValue("birthDate", (object?)bird.BirthDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("size", bird.Size.ToString());
        cmd.Parameters.AddWithValue("gender", bird.Gender.ToString());
        cmd.Parameters.AddWithValue("ownerId", (object?)bird.OwnerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("canPair", bird.CanPair);
        cmd.Parameters.AddWithValue("pairName", (object?)bird.PairName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)bird.Notes ?? DBNull.Value);
    }

    private static Bird Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Species = reader.GetString(1),
        Name = reader.GetString(2),
        BirthDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
        Size = Enum.TryParse<BirdSize>(reader.GetString(4), out var size) ? size : BirdSize.中小型,
        Gender = Enum.TryParse<BirdGender>(reader.GetString(5), out var gender) ? gender : BirdGender.不明,
        OwnerId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        Notes = reader.IsDBNull(7) ? "" : reader.GetString(7),
        OwnerName = reader.GetString(8),
        IsProprietorBird = reader.GetBoolean(9),
        CanPair = reader.GetBoolean(10),
        PairName = reader.GetString(11),
    };
}

public class CageRepository(Database db)
{
    public List<Cage> GetAll()
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, capacity, cage_type, COALESCE(group_name, ''), group_order, notes FROM cages;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Cage>();
        while (reader.Read())
            result.Add(Map(reader));
        result.Sort((a, b) => CompareNatural(a.Name, b.Name));
        return result;
    }

    public Cage? GetById(int id)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, capacity, cage_type, COALESCE(group_name, ''), group_order, notes FROM cages WHERE id = @id;";
        cmd.Parameters.AddWithValue("id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Insert(Cage cage)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO cages (name, capacity, cage_type, group_name, group_order, notes)
            VALUES (@name, @capacity, @cageType, @groupName, @groupOrder, @notes)
            RETURNING id;
            """;
        AddParams(cmd, cage);
        return (int)cmd.ExecuteScalar()!;
    }

    public void Update(Cage cage)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE cages SET name = @name, capacity = @capacity, cage_type = @cageType,
                group_name = @groupName, group_order = @groupOrder, notes = @notes
            WHERE id = @id;
            """;
        AddParams(cmd, cage);
        cmd.Parameters.AddWithValue("id", cage.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM cages WHERE id = @id;";
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
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

    private static void AddParams(NpgsqlCommand cmd, Cage cage)
    {
        cmd.Parameters.AddWithValue("name", cage.Name);
        cmd.Parameters.AddWithValue("capacity", cage.Capacity);
        cmd.Parameters.AddWithValue("cageType", cage.Type.ToString());
        cmd.Parameters.AddWithValue("groupName", (object?)cage.GroupName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("groupOrder", cage.GroupOrder);
        cmd.Parameters.AddWithValue("notes", (object?)cage.Notes ?? DBNull.Value);
    }

    private static Cage Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Capacity = reader.GetInt32(2),
        Type = Enum.TryParse<CageType>(reader.GetString(3), out var type) ? type : CageType.通常籠,
        GroupName = reader.GetString(4),
        GroupOrder = reader.GetInt32(5),
        Notes = reader.IsDBNull(6) ? "" : reader.GetString(6),
    };
}

public class ReservationRepository(Database db)
{
    private const string BaseSelect = """
        SELECT r.id, r.bird_id, r.cage_id, r.start_date, r.end_date, r.notes, b.name, c.name,
               b.owner_id, COALESCE(o.name, '')
        FROM reservations r
        JOIN birds b ON b.id = r.bird_id
        JOIN cages c ON c.id = r.cage_id
        LEFT JOIN owners o ON o.id = b.owner_id
        """;

    public List<Reservation> GetAll()
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " ORDER BY r.start_date;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Reservation>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public List<Reservation> GetByCage(int cageId)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " WHERE r.cage_id = @cageId ORDER BY r.start_date;";
        cmd.Parameters.AddWithValue("cageId", cageId);
        using var reader = cmd.ExecuteReader();
        var result = new List<Reservation>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public List<Reservation> GetByBird(int birdId)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " WHERE r.bird_id = @birdId ORDER BY r.start_date;";
        cmd.Parameters.AddWithValue("birdId", birdId);
        using var reader = cmd.ExecuteReader();
        var result = new List<Reservation>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public Reservation? GetById(int id)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " WHERE r.id = @id;";
        cmd.Parameters.AddWithValue("id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Insert(Reservation reservation)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO reservations (bird_id, cage_id, start_date, end_date, notes)
            VALUES (@birdId, @cageId, @startDate, @endDate, @notes)
            RETURNING id;
            """;
        AddParams(cmd, reservation);
        return (int)cmd.ExecuteScalar()!;
    }

    public void Update(Reservation reservation)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE reservations SET bird_id = @birdId, cage_id = @cageId,
                start_date = @startDate, end_date = @endDate, notes = @notes
            WHERE id = @id;
            """;
        AddParams(cmd, reservation);
        cmd.Parameters.AddWithValue("id", reservation.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM reservations WHERE id = @id;";
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }

    // 指定期間と重なる予約数を籠ごとにカウントする（空き確認・定員チェック用）
    public int CountOverlapping(int cageId, DateTime startDate, DateTime? endDate, int? excludeReservationId = null)
    {
        return GetByCage(cageId)
            .Where(r => excludeReservationId is null || r.Id != excludeReservationId)
            .Count(r => r.OverlapsWith(startDate, endDate));
    }

    private static void AddParams(NpgsqlCommand cmd, Reservation r)
    {
        cmd.Parameters.AddWithValue("birdId", r.BirdId);
        cmd.Parameters.AddWithValue("cageId", r.CageId);
        cmd.Parameters.AddWithValue("startDate", r.StartDate.Date);
        cmd.Parameters.AddWithValue("endDate", (object?)r.EndDate?.Date ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)r.Notes ?? DBNull.Value);
    }

    private static Reservation Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        BirdId = reader.GetInt32(1),
        CageId = reader.GetInt32(2),
        StartDate = reader.GetDateTime(3),
        EndDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
        Notes = reader.IsDBNull(5) ? "" : reader.GetString(5),
        BirdName = reader.GetString(6),
        CageName = reader.GetString(7),
        OwnerId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
        OwnerName = reader.GetString(9),
    };
}
