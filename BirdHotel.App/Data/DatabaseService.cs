using Microsoft.Data.Sqlite;

namespace BirdHotel.App.Data;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BirdHotelReservation");
        Directory.CreateDirectory(appDataDir);
        var dbPath = Path.Combine(appDataDir, "bird_hotel.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath
        }.ToString();

        Initialize();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private void Initialize()
    {
        using var connection = OpenConnection();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Owners (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Contact TEXT NULL,
                    IsProprietor INTEGER NOT NULL DEFAULT 0,
                    Notes TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS Birds (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Species TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    BirthDate TEXT NULL,
                    Size TEXT NOT NULL,
                    Gender TEXT NOT NULL,
                    OwnerId INTEGER NULL REFERENCES Owners(Id),
                    Notes TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS Cages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Capacity INTEGER NOT NULL DEFAULT 2,
                    Notes TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS Reservations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    BirdId INTEGER NOT NULL REFERENCES Birds(Id) ON DELETE CASCADE,
                    CageId INTEGER NOT NULL REFERENCES Cages(Id) ON DELETE CASCADE,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT NULL,
                    Notes TEXT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        MigrateLegacyOwnerColumns(connection);
    }

    // 旧バージョン（飼い主を Birds.OwnerName/OwnerContact/IsOwnerBird に直接保持していた頃）の
    // データベースから、Owners テーブルを使う新しいスキーマへ既存データを保ったまま移行する。
    private static void MigrateLegacyOwnerColumns(SqliteConnection connection)
    {
        var birdColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Birds);";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                birdColumns.Add(reader.GetString(1));
        }

        if (!birdColumns.Contains("OwnerId"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE Birds ADD COLUMN OwnerId INTEGER NULL REFERENCES Owners(Id);";
            cmd.ExecuteNonQuery();
        }

        if (!birdColumns.Contains("OwnerName"))
            return; // 旧カラムが無ければ移行済み、または新規データベース

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO Owners (Name, Contact, IsProprietor)
                SELECT OwnerName, OwnerContact, MAX(IsOwnerBird)
                FROM Birds
                WHERE OwnerId IS NULL AND TRIM(COALESCE(OwnerName, '')) <> ''
                GROUP BY OwnerName, OwnerContact;
                """;
            cmd.ExecuteNonQuery();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE Birds
                SET OwnerId = (
                    SELECT o.Id FROM Owners o
                    WHERE o.Name = Birds.OwnerName
                      AND IFNULL(o.Contact, '') = IFNULL(Birds.OwnerContact, '')
                    LIMIT 1
                )
                WHERE OwnerId IS NULL AND TRIM(COALESCE(OwnerName, '')) <> '';
                """;
            cmd.ExecuteNonQuery();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                ALTER TABLE Birds DROP COLUMN OwnerName;
                ALTER TABLE Birds DROP COLUMN OwnerContact;
                ALTER TABLE Birds DROP COLUMN IsOwnerBird;
                """;
            cmd.ExecuteNonQuery();
        }
    }
}
