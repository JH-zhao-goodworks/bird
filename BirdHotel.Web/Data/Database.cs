using Npgsql;

namespace BirdHotel.Web.Data;

public class Database
{
    private readonly string _connectionString;

    public Database(IConfiguration configuration)
    {
        // Render/Neon は DATABASE_URL 形式（postgres://user:pass@host/db）で渡されることが多いので両対応にする
        var raw = Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? configuration.GetConnectionString("Default")
                  ?? throw new InvalidOperationException("接続文字列が設定されていません。環境変数 DATABASE_URL を設定してください。");

        _connectionString = raw.StartsWith("postgres://") || raw.StartsWith("postgresql://")
            ? ConvertUrlToConnectionString(raw)
            : raw;
    }

    private static string ConvertUrlToConnectionString(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = SslMode.Require,
        }.ToString();
    }

    public NpgsqlConnection OpenConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    // 起動時にテーブルを作る（既にあれば何もしない）
    public void Initialize()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS owners (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                contact TEXT,
                is_proprietor BOOLEAN NOT NULL DEFAULT FALSE,
                notes TEXT
            );

            CREATE TABLE IF NOT EXISTS species (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS birds (
                id SERIAL PRIMARY KEY,
                species TEXT NOT NULL,
                name TEXT NOT NULL,
                birth_date DATE,
                size TEXT NOT NULL,
                gender TEXT NOT NULL,
                owner_id INTEGER REFERENCES owners(id),
                can_pair BOOLEAN NOT NULL DEFAULT FALSE,
                pair_name TEXT,
                notes TEXT
            );

            CREATE TABLE IF NOT EXISTS cages (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                capacity INTEGER NOT NULL DEFAULT 2,
                cage_type TEXT NOT NULL DEFAULT '通常籠',
                group_name TEXT,
                group_order INTEGER NOT NULL DEFAULT 0,
                notes TEXT
            );

            CREATE TABLE IF NOT EXISTS reservations (
                id SERIAL PRIMARY KEY,
                bird_id INTEGER NOT NULL REFERENCES birds(id) ON DELETE CASCADE,
                cage_id INTEGER NOT NULL REFERENCES cages(id) ON DELETE CASCADE,
                start_date DATE NOT NULL,
                end_date DATE,
                notes TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        SeedSpecies(connection);
    }

    private static readonly string[] DefaultSpecies =
    [
        "セキセイインコ", "オカメインコ", "コザクラインコ", "ボタンインコ",
        "サザナミインコ", "マメルリハ", "オキナインコ", "ヨウム", "ウロコインコ",
    ];

    private static void SeedSpecies(NpgsqlConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO species (name) VALUES " +
            string.Join(", ", DefaultSpecies.Select((_, i) => $"(@s{i})")) +
            " ON CONFLICT (name) DO NOTHING;";
        for (var i = 0; i < DefaultSpecies.Length; i++)
            cmd.Parameters.AddWithValue($"s{i}", DefaultSpecies[i]);
        cmd.ExecuteNonQuery();
    }
}
