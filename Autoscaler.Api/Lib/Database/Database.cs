using Autoscaler.Lib.Database.Models;
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