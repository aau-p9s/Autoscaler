using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using Autoscaler.Lib.Forecasts;

namespace Autoscaler.Runner.Kubernetes;

class Prometheus
{
    private readonly HttpClient client;
    readonly string Addr;

    public Prometheus(string addr)
    {
        Addr = addr;
        client = new HttpClient();
    }

    public async Task<IEnumerable<Historical>> QueryRange(string queryString, DateTime start, DateTime end, int period)
    {
        var query = $"query={EncodeQuery(queryString)}&start={ToRFC3339(start)}&end={ToRFC3339(end)}&step={period/1000}s";
        List<Historical> result_list = new();
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync($"{Addr}/api/v1/query_range?{query}");
        }
        catch
        {
            Console.WriteLine("prometheus seems to be down...");
            goto end;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        if (json == null)
            goto end;
        var data = json["data"];
        if (data == null)
            goto end;
        var result = data["result"];
        if (result == null)
            goto end;
        foreach (var item in result.AsArray())
        {
            if (item == null)
                continue;
            var valuesObj = item["values"];
            if (valuesObj == null)
                continue;
            var values = valuesObj.AsArray();
            foreach (var value in values)
            {
                if (value == null)
                    continue;

                try
                {
                    result_list.Add(new(new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds((double)value[0]), double.Parse((string)value[1])));
                }
                catch (NullReferenceException e)
                {
                    Console.WriteLine("nullreferenceexception: " + e);
                }
            }
        }

        // todo: maybe let prometheus descide what max is if possible?
        end:
        return result_list;
    }

    public string EncodeQuery(string target)
    {
        return HttpUtility.UrlEncode(target);
    }

    private string ToRFC3339(DateTime date)
    {
        return date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffK");
    }
}
