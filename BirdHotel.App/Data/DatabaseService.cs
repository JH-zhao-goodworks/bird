using Microsoft.Data.Sqlite;

namespace BirdHotel.App.Data;

public class DatabaseService
{
    private readonly string _connectionString;

    // データベースファイルの場所。フォルダごとコピーすれば別のPCでもデータを引き継げる。
    public string DatabasePath { get; }

    public DatabaseService()
    {
        DatabasePath = ResolveDatabasePath();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath
        }.ToString();

        Initialize();
    }

    // exeと同じ場所の data フォルダに置く（持ち運べるようにするため）。
    // 書き込めない場所（Program Files 配下など）に置かれた場合は、従来の %LOCALAPPDATA% を使う。
    private static string ResolveDatabasePath()
    {
        var portableDir = Path.Combine(AppContext.BaseDirectory, "data");
        var legacyDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BirdHotelReservation");
        var legacyPath = Path.Combine(legacyDir, "bird_hotel.db");

        try
        {
            Directory.CreateDirectory(portableDir);

            // 書き込めるか実際に試す
            var probePath = Path.Combine(portableDir, ".write_test");
            File.WriteAllText(probePath, "");
            File.Delete(probePath);

            var portablePath = Path.Combine(portableDir, "bird_hotel.db");

            // 以前のバージョンで %LOCALAPPDATA% に貯めたデータがあれば引き継ぐ
            if (!File.Exists(portablePath) && File.Exists(legacyPath))
                File.Copy(legacyPath, portablePath);

            return portablePath;
        }
        catch (Exception)
        {
            Directory.CreateDirectory(legacyDir);
            return legacyPath;
        }
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
                    CanPair INTEGER NOT NULL DEFAULT 0,
                    PairName TEXT NULL,
                    Notes TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS Species (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE
                );

                CREATE TABLE IF NOT EXISTS Cages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Capacity INTEGER NOT NULL DEFAULT 2,
                    CageType TEXT NOT NULL DEFAULT '通常籠',
                    GroupName TEXT NULL,
                    GroupOrder INTEGER NOT NULL DEFAULT 0,
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
        AddMissingBirdColumns(connection);
        SeedAndMigrateSpecies(connection);
    }

    // 後から追加した項目（鳥のペア可否・ペア名、籠の種別）を、既存のデータベースにも足す。
    private static void AddMissingBirdColumns(SqliteConnection connection)
    {
        var birdColumns = GetColumnNames(connection, "Birds");

        if (!birdColumns.Contains("CanPair"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE Birds ADD COLUMN CanPair INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }

        if (!birdColumns.Contains("PairName"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE Birds ADD COLUMN PairName TEXT NULL;";
            cmd.ExecuteNonQuery();
        }

        var cageColumns = GetColumnNames(connection, "Cages");
        if (!cageColumns.Contains("CageType"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE Cages ADD COLUMN CageType TEXT NOT NULL DEFAULT '通常籠';";
            cmd.ExecuteNonQuery();
        }

        if (!cageColumns.Contains("GroupName"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE Cages ADD COLUMN GroupName TEXT NULL;";
            cmd.ExecuteNonQuery();
        }

        if (!cageColumns.Contains("GroupOrder"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE Cages ADD COLUMN GroupOrder INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }
    }

    private static HashSet<string> GetColumnNames(SqliteConnection connection, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(1));
        return columns;
    }

    private static readonly string[] DefaultSpecies =
    [
        "セキセイインコ", "オカメインコ", "コザクラインコ", "ボタンインコ",
        "サザナミインコ", "マメルリハ", "オキナインコ", "ヨウム", "ウロコインコ",
    ];

    // 種類マスタに初期候補を投入し、既に鳥に入力済みの種類（自由入力だった頃のデータ）も
    // プルダウンの選択肢として使えるよう取り込む。
    private static void SeedAndMigrateSpecies(SqliteConnection connection)
    {
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "INSERT OR IGNORE INTO Species (Name) VALUES " +
                string.Join(", ", DefaultSpecies.Select((_, i) => $"($species{i})")) + ";";
            for (var i = 0; i < DefaultSpecies.Length; i++)
                cmd.Parameters.AddWithValue($"$species{i}", DefaultSpecies[i]);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO Species (Name)
                SELECT DISTINCT TRIM(Species) FROM Birds WHERE TRIM(COALESCE(Species, '')) <> '';
                """;
            cmd.ExecuteNonQuery();
        }
    }

    // 旧バージョン（飼い主を Birds.OwnerName/OwnerContact/IsOwnerBird に直接保持していた頃）の
    // データベースから、Owners テーブルを使う新しいスキーマへ既存データを保ったまま移行する。
    private static void MigrateLegacyOwnerColumns(SqliteConnection connection)
    {
        var birdColumns = GetColumnNames(connection, "Birds");

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
