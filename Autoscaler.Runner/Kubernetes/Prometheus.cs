using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web;
using Autoscaler.Persistence.HistoricRepository;

namespace Autoscaler.Runner.Kubernetes;

public class Prometheus : IAPI
{
    readonly string _addr;
    private readonly HttpClient _client;

    public Prometheus(string addr)
    {
        _addr = addr;
        _client = new();
        if (!IsUp())
        {
            Console.WriteLine("Prometheus shouldn't be down");
            Environment.Exit(1);
        }
    }

    public async Task<IEnumerable<HistoricEntity>> QueryRange(string queryString, DateTime start, DateTime end,
        int period)
    {
        if (!IsUp())
        {
            return new List<HistoricEntity>();
        }

        var query =
            $"query={EncodeQuery(queryString)}&start={ToRFC3339(start)}&end={ToRFC3339(end)}&step={period / 1000}s";
        var results = new List<HistoricEntity>();
        HttpResponseMessage response;
        try
        {
            response = await _client.GetAsync($"{_addr}/api/v1/query_range?{query}");
        }
        catch (Exception e)
        {
            Console.WriteLine("Prometheus seems to be down");
            HandleException(e);
            return new List<HistoricEntity>();
        }

        var jsonString = await response.Content.ReadAsStringAsync();
        try
        {
            // These warnings are useless, as the nullreference exceptions are handled in the catch block anyway
#pragma warning disable CS8602, CS8604, CS8600
            var json = await response.Content.ReadFromJsonAsync<JsonObject>();
            var result = json["data"]["result"];
            foreach (var item in result.AsArray())
            {
                foreach (var value in item["values"].AsArray())
                {
                    var datetime = new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds((double)value[0]);
                    var realValue = double.Parse((string)value[1]);
                    // TODO: implement this:  results.Add(new(datetime, realValue));
                }
            }
#pragma warning restore CS8602, CS8604, CS8600
        }
        catch (NullReferenceException e)
        {
            Console.WriteLine("Somehow there was an issue decoding json");
            Console.WriteLine($"json content: {jsonString}");
            HandleException(e);
        }

        return results;
    }

    private static string EncodeQuery(string target)
    {
        return HttpUtility.UrlEncode(target);
    }

    private static string ToRFC3339(DateTime date)
    {
        return date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffK");
    }

    static void HandleException(Exception e)
    {
        // TODO: Move to an interface
        Console.WriteLine(e.Message);
        if (e.InnerException != null)
            HandleException(e.InnerException);
    }

    public bool IsUp()
    {
        // TODO: implement logic to check if the service is up
        return false;
    }
}