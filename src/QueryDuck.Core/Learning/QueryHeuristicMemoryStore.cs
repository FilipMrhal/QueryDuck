using Microsoft.Data.Sqlite;

namespace QueryDuck.Core.Learning;

internal sealed class QueryHeuristicMemoryStore : IDisposable
{
    private readonly string _connectionString;
    private readonly int _maxEntries;
    private readonly Lock _gate = new();

    public QueryHeuristicMemoryStore(string databasePath, int maxEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _maxEntries = Math.Max(500, maxEntries);
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        Initialize();
    }

    public string DatabasePath =>
        new SqliteConnectionStringBuilder(_connectionString).DataSource;

    public void RecordFeedback(
        string shapeHash,
        string provider,
        string category,
        string title,
        QueryHeuristicMemoryAction action)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO feedback (recorded_at, shape_hash, provider, category, title, action)
                VALUES ($at, $shape, $provider, $category, $title, $action)
                """;
            command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$shape", shapeHash);
            command.Parameters.AddWithValue("$provider", provider);
            command.Parameters.AddWithValue("$category", category);
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$action", action.ToString());
            command.ExecuteNonQuery();
            Prune(connection);
        }
    }

    public void RecordSlowCapture(string shapeHash, string provider, double durationMs)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO shape_outcomes (recorded_at, shape_hash, provider, duration_ms)
                VALUES ($at, $shape, $provider, $duration)
                """;
            command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$shape", shapeHash);
            command.Parameters.AddWithValue("$provider", provider);
            command.Parameters.AddWithValue("$duration", durationMs);
            command.ExecuteNonQuery();
            Prune(connection);
        }
    }

    public RecommendationHeuristicScore ScoreRecommendation(
        string shapeHash,
        string provider,
        string category,
        string title)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT action, COUNT(*)
                FROM feedback
                WHERE shape_hash = $shape
                  AND provider = $provider
                  AND category = $category
                  AND title = $title
                GROUP BY action
                """;
            command.Parameters.AddWithValue("$shape", shapeHash);
            command.Parameters.AddWithValue("$provider", provider);
            command.Parameters.AddWithValue("$category", category);
            command.Parameters.AddWithValue("$title", title);

            var copied = 0;
            var selected = 0;
            var viewed = 0;
            var dismissed = 0;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var action = reader.GetString(0);
                var count = reader.GetInt32(1);
                switch (action)
                {
                    case nameof(QueryHeuristicMemoryAction.Copied):
                        copied = count;
                        break;
                    case nameof(QueryHeuristicMemoryAction.Selected):
                        selected = count;
                        break;
                    case nameof(QueryHeuristicMemoryAction.Viewed):
                        viewed = count;
                        break;
                    case nameof(QueryHeuristicMemoryAction.Dismissed):
                        dismissed = count;
                        break;
                }
            }

            var providerBoost = ScoreProviderCategory(connection, provider, category);
            var score = copied * 3.0 + selected * 1.0 + viewed * 0.25 - dismissed * 2.0 + providerBoost;
            var hint = BuildHint(copied, selected, dismissed, providerBoost > 0);
            return new RecommendationHeuristicScore(score, copied, selected, dismissed, hint);
        }
    }

    public QueryHeuristicMemoryStats GetStats()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM feedback),
                    (SELECT COUNT(DISTINCT shape_hash || '|' || provider) FROM feedback),
                    (SELECT COUNT(*) FROM feedback WHERE action = 'Copied'),
                    (SELECT COUNT(*) FROM feedback WHERE action = 'Dismissed')
                """;

            using var reader = command.ExecuteReader();
            reader.Read();
            return new QueryHeuristicMemoryStats(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                DatabasePath);
        }
    }

    public QueryHeuristicWorkloadStats GetWorkloadStats(string? provider = null, int top = 20)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT shape_hash, provider, COUNT(*), SUM(duration_ms), MAX(duration_ms), AVG(duration_ms)
                FROM shape_outcomes
                WHERE ($provider IS NULL OR provider = $provider)
                GROUP BY shape_hash, provider
                ORDER BY SUM(duration_ms) DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$provider", string.IsNullOrWhiteSpace(provider) ? DBNull.Value : provider);
            command.Parameters.AddWithValue("$limit", Math.Max(1, top));

            var shapes = new List<QueryWorkloadShapeStats>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                shapes.Add(new QueryWorkloadShapeStats(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetDouble(3),
                    reader.GetDouble(4),
                    reader.GetDouble(5)));
            }

            return new QueryHeuristicWorkloadStats(shapes);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM feedback; DELETE FROM shape_outcomes;";
            command.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
    }

    private static double ScoreProviderCategory(SqliteConnection connection, string provider, string category)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT action, COUNT(*)
            FROM feedback
            WHERE provider = $provider AND category = $category
            GROUP BY action
            """;
        command.Parameters.AddWithValue("$provider", provider);
        command.Parameters.AddWithValue("$category", category);

        var copied = 0;
        var dismissed = 0;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(0) == nameof(QueryHeuristicMemoryAction.Copied))
            {
                copied = reader.GetInt32(1);
            }
            else if (reader.GetString(0) == nameof(QueryHeuristicMemoryAction.Dismissed))
            {
                dismissed = reader.GetInt32(1);
            }
        }

        return Math.Min(1.5, copied * 0.15 - dismissed * 0.1);
    }

    private static string? BuildHint(int copied, int selected, int dismissed, bool providerPattern)
    {
        if (copied >= 2)
        {
            return $"You've copied this fix {copied} times for similar queries on this machine.";
        }

        if (selected >= 3)
        {
            return "You often pick this suggestion for similar slow queries.";
        }

        if (providerPattern)
        {
            return "Similar fixes worked well for this provider in past sessions.";
        }

        if (dismissed >= 2)
        {
            return "Lower priority — you've dismissed this before.";
        }

        return null;
    }

    private void Initialize()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS feedback (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    recorded_at TEXT NOT NULL,
                    shape_hash TEXT NOT NULL,
                    provider TEXT NOT NULL,
                    category TEXT NOT NULL,
                    title TEXT NOT NULL,
                    action TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS shape_outcomes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    recorded_at TEXT NOT NULL,
                    shape_hash TEXT NOT NULL,
                    provider TEXT NOT NULL,
                    duration_ms REAL NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_feedback_lookup
                    ON feedback(shape_hash, provider, category, title);
                CREATE INDEX IF NOT EXISTS idx_feedback_recorded
                    ON feedback(recorded_at);
                """;
            command.ExecuteNonQuery();
        }
    }

    private void Prune(SqliteConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM feedback";
        var count = Convert.ToInt32(countCommand.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        if (count <= _maxEntries)
        {
            return;
        }

        var toDelete = count - _maxEntries;
        using var deleteCommand = connection.CreateCommand();
        deleteCommand.CommandText = """
            DELETE FROM feedback
            WHERE id IN (
                SELECT id FROM feedback
                ORDER BY recorded_at ASC
                LIMIT $limit
            )
            """;
        deleteCommand.Parameters.AddWithValue("$limit", toDelete);
        deleteCommand.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
