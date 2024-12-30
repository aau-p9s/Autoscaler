using Autoscaler.Lib.Forecasts;
using Microsoft.Data.Sqlite;

namespace Autoscaler.Lib.Database;

public class Database
{
    readonly string Path;
    readonly SqliteConnection Connection;
    private bool _isManualChange = false;
    public bool IsManualChange => _isManualChange;
    private readonly string HistoricalTable = "historical";
    private readonly string ForecastsTable = "forecasts";
    private readonly string SettingsTable = "settings";


    public Database(string path)
    {
        Path = path;
        Connection = new SqliteConnection($"Data Source={Path}");
        Connection.Open();
        var command = Connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS historical (
                id INTEGER PRIMARY KEY,
                timestamp DATETIME NOT NULL,
                amount DOUBLE NOT NULL
            );
            CREATE TABLE IF NOT EXISTS forecasts (
                id INTEGER PRIMARY KEY,
                timestamp DATETIME NOT NULL,
                amount DOUBLE NOT NULL,
                fetch_time DATETIME NOT NULL
            );
            CREATE TABLE IF NOT EXISTS settings (
                id INTEGER PRIMARY KEY,
                scaleup INTEGER NOT NULL,
                scaledown INTEGER NOT NULL,
                scaleperiod INTEGER NOT NULL,
                UNIQUE(scaleup, scaledown, scaleperiod)
            );

            INSERT OR IGNORE INTO settings (scaleup, scaledown, scaleperiod) VALUES (50, 20, 60000)

        ";
        //INSERT OR IGNORE INTO settings (scaleup, scaledown, scaleperiod) VALUES ('0', '0', '0')
        command.ExecuteNonQuery();
    }

    public void Add(DateTime time, double value)
    {
        var command = Connection.CreateCommand();
        command.CommandText = $@"
            INSERT INTO {HistoricalTable} (timestamp, amount) VALUES ($time, $amount)
        ";
        command.Parameters.AddWithValue("$time", time);
        command.Parameters.AddWithValue("$amount", value);
        command.ExecuteNonQuery();
    }

    public Dictionary<int, double> GetByTimestamp(DateTime time)
    {
        Dictionary<int, double> result = new();
        var command = Connection.CreateCommand();
        command.CommandText = $@"
            SELECT id, amount FROM {HistoricalTable} WHERE timestamp = $time
        ";
        command.Parameters.AddWithValue("$time", time);
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                result.Add(reader.GetInt32(0), reader.GetDouble(1));
            }
        }

        return result;
    }

    public void SetSettings(Settings settings)
    {
        var command = Connection.CreateCommand();
        var oldSettings = GetSettings();

        if (settings.ScaleUp == null)
            settings.ScaleUp = oldSettings.ScaleUp;
        if (settings.ScaleDown == null)
            settings.ScaleDown = oldSettings.ScaleDown;
        if (settings.ScalePeriod is null or < 60000)
            settings.ScalePeriod = oldSettings.ScalePeriod;

        command.CommandText = $@"
            UPDATE {SettingsTable} SET scaleup = $scaleup, scaledown = $scaledown, scaleperiod = $scaleperiod WHERE id = $id
        ";
        command.Parameters.AddWithValue("$scaleup", settings.ScaleUp);
        command.Parameters.AddWithValue("$scaledown", settings.ScaleDown);
        command.Parameters.AddWithValue("$scaleperiod", settings.ScalePeriod);
        command.Parameters.AddWithValue("$id", settings.Id);

        command.ExecuteNonQuery();
    }

    public Settings GetSettings()
    {
        var command = Connection.CreateCommand();
        Settings Settings = new();
        command.CommandText = $@"
            SELECT id, scaleup, scaledown, scaleperiod FROM {SettingsTable}
        ";
        using (var reader = command.ExecuteReader())
        {
            reader.Read();
            Settings.Id = reader.GetInt32(0);
            Settings.ScaleUp = reader.GetInt32(1);
            Settings.ScaleDown = reader.GetInt32(2);
            Settings.ScalePeriod = reader.GetInt32(3);
            return Settings;
        }
    }

    public Dictionary<DateTime, double> AllHistorical()
    {
        var command = Connection.CreateCommand();
        command.CommandText = $@"
            SELECT timestamp, amount FROM {HistoricalTable}
        ";
        Dictionary<DateTime, double> result = new();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                result[reader.GetDateTime(0)] = reader.GetDouble(1);
            }
        }

        return result;
    }

    public void InsertHistorical(IEnumerable<Tuple<int, double>> data)
    {
        foreach (var (timestamp, value) in data)
        {
            var command = Connection.CreateCommand();
            command.CommandText = $@"
                INSERT OR IGNORE INTO {HistoricalTable} (id, timestamp, amount) VALUES (
                    (SELECT id FROM {HistoricalTable} WHERE strftime('%Y-%m-%d-%H:%M', timestamp) = strftime('%Y-%m-%d-%H:%M', $time)),
                    $time,
                    $amount
                )
            ";
            command.Parameters.AddWithValue("$time", new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(timestamp));
            command.Parameters.AddWithValue("$amount", value);
            command.ExecuteNonQueryAsync();
        }
    }
    
    public void InsertForecast(List<Forecast> data)
    {
        foreach (var item in data)
        {
            var command = Connection.CreateCommand();
            command.CommandText = $@"
                INSERT OR IGNORE INTO {ForecastsTable} (id, timestamp, amount, fetch_time) VALUES (
                    (SELECT id FROM {ForecastsTable} WHERE strftime('%Y-%m-%d-%H:%M', timestamp) = strftime('%Y-%m-%d-%H:%M', $time)),
                    $time,
                    $amount,
                    date('now')
                )
            ";
            command.Parameters.AddWithValue("$time", item.Timestamp);
            command.Parameters.AddWithValue("$amount", item.Value);
            command.ExecuteNonQueryAsync();
        }
    }

    public PredictionResult Historic(DateTime from)
    {
        var command = Connection.CreateCommand();
        command.CommandText = $@"
            SELECT timestamp, amount FROM {HistoricalTable} WHERE
                strftime('%Y-%m-%d-%H:%M', timestamp) >= strftime('%Y-%m-%d-%H:%M', $time)
        ";
        command.Parameters.AddWithValue("$time", from);
        PredictionResult result = new();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                result.Time.Add(reader.GetDateTime(0));
                result.Amount.Add(reader.GetDouble(1));
            }
        }

        return result;
    }
    
    public Forecast GetNewestHistorical()
    {
        var command = Connection.CreateCommand();
        command.CommandText = $@"
            SELECT timestamp, amount FROM {HistoricalTable} ORDER BY timestamp DESC LIMIT 1
        ";
        using (var reader = command.ExecuteReader())
        {
            reader.Read();
            return new Forecast(reader.GetDateTime(0), reader.GetDouble(1));
        }
    }

    public Dictionary<DateTime, double> Prediction(DateTime to)
    {
        var command = Connection.CreateCommand();
        command.CommandText = $@"
            SELECT timestamp, amount FROM {ForecastsTable} WHERE
                strftime('%Y-%m-%d-%H:%M', timestamp) <= strftime('%Y-%m-%d-%H:%M', $time)
        ";
        command.Parameters.AddWithValue("$time", to);
        Dictionary<DateTime, double> result = new();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                result[reader.GetDateTime(0)] = reader.GetDouble(1);
            }
        }

        return result;
    }

    public void ManualChange(Dictionary<DateTime, double> data)
    {
        foreach (var p in data)
        {
            // Delete existing rows with the same timestamp
            using (var deleteCommand = Connection.CreateCommand())
            {
                deleteCommand.CommandText = $@"
                DELETE FROM {ForecastsTable} WHERE timestamp = $time
            ";
                deleteCommand.Parameters.AddWithValue("$time", p.Key);
                deleteCommand.ExecuteNonQuery();
            }
        }

        foreach (var p in data)
        {
            // Insert new rows
            using (var command = Connection.CreateCommand())
            {
                command.CommandText = $@"
                INSERT INTO {ForecastsTable} (timestamp, amount, fetch_time) VALUES ($time, $amount, date('now'))
            ";
                command.Parameters.AddWithValue("$time", p.Key);
                command.Parameters.AddWithValue("$amount", p.Value);
                command.ExecuteNonQuery();
            }
        }

        _isManualChange = true;
    }
    
    public void RemoveAllHistorical()
    {
        var command = Connection.CreateCommand();
        command.CommandText = $@"
            DELETE FROM {HistoricalTable};
        ";
        command.ExecuteNonQuery();
    }
    
    public void RemoveAllForecasts()
    {
        var command = Connection.CreateCommand();
        command.CommandText = $@"
            DELETE FROM {ForecastsTable};
        ";
        command.ExecuteNonQuery();
        _isManualChange = false;
    }
}