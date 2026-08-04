using BirdHotel.App.Models;
using Microsoft.Data.Sqlite;

namespace BirdHotel.App.Data;

public class ReservationRepository
{
    private readonly DatabaseService _db;

    public ReservationRepository(DatabaseService db) => _db = db;

    private const string BaseSelect = """
        SELECT r.Id, r.BirdId, r.CageId, r.StartDate, r.EndDate, r.Notes, b.Name, c.Name
        FROM Reservations r
        JOIN Birds b ON b.Id = r.BirdId
        JOIN Cages c ON c.Id = r.CageId
        """;

    public List<Reservation> GetAll()
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " ORDER BY r.StartDate;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Reservation>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    public List<Reservation> GetByCage(int cageId)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = BaseSelect + " WHERE r.CageId = $cageId ORDER BY r.StartDate;";
        cmd.Parameters.AddWithValue("$cageId", cageId);
        using var reader = cmd.ExecuteReader();
        var result = new List<Reservation>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    // 指定日時点で在籠中（滞在期間に含まれる）の予約一覧を取得する（ホーム画面の籠一覧表示用）
    public List<Reservation> GetActiveOn(DateTime date)
    {
        return GetAll()
            .Where(r => r.StartDate.Date <= date.Date && (r.EndDate is null || r.EndDate.Value.Date >= date.Date))
            .ToList();
    }

    public int Insert(Reservation reservation)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Reservations (BirdId, CageId, StartDate, EndDate, Notes)
            VALUES ($birdId, $cageId, $startDate, $endDate, $notes);
            SELECT last_insert_rowid();
            """;
        AddParams(cmd, reservation);
        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    public void Update(Reservation reservation)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE Reservations SET
                BirdId = $birdId, CageId = $cageId, StartDate = $startDate, EndDate = $endDate, Notes = $notes
            WHERE Id = $id;
            """;
        AddParams(cmd, reservation);
        cmd.Parameters.AddWithValue("$id", reservation.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Reservations WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // 指定期間と重なる予約数を籠ごとにカウントする（空き確認・定員チェック用）。
    // excludeReservationId は編集時に自分自身の予約を除外するために使う。
    public int CountOverlapping(int cageId, DateTime startDate, DateTime? endDate, int? excludeReservationId = null)
    {
        return GetByCage(cageId)
            .Where(r => excludeReservationId is null || r.Id != excludeReservationId)
            .Count(r => r.OverlapsWith(startDate, endDate));
    }

    private static void AddParams(SqliteCommand cmd, Reservation r)
    {
        cmd.Parameters.AddWithValue("$birdId", r.BirdId);
        cmd.Parameters.AddWithValue("$cageId", r.CageId);
        cmd.Parameters.AddWithValue("$startDate", r.StartDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$endDate", (object?)r.EndDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", (object?)r.Notes ?? DBNull.Value);
    }

    private static Reservation Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        BirdId = reader.GetInt32(1),
        CageId = reader.GetInt32(2),
        StartDate = DateTime.Parse(reader.GetString(3)),
        EndDate = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
        Notes = reader.IsDBNull(5) ? "" : reader.GetString(5),
        BirdName = reader.GetString(6),
        CageName = reader.GetString(7),
    };
}
