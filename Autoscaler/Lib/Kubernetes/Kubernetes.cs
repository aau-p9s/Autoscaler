using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Autoscaler.Lib.Kubernetes;

class Kubernetes
{
    readonly HttpClientHandler handler;
    readonly HttpClient client;
    readonly Tuple<string, string>? authHeader;
    readonly string Addr;

    public Kubernetes(string addr)
    {
        Addr = addr;
        handler = new()
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            // TODO: Handle actual certificate
            ServerCertificateCustomValidationCallback = (_, _, _, _) => { return true; }
        };
        client = new(handler);
        if (File.Exists("/var/run/secrets/kubernetes.io/serviceaccount/token"))
        {
            StreamReader stream = new("/var/run/secrets/kubernetes.io/serviceaccount/token");
            authHeader = new("Authorization", $"Bearer {stream.ReadToEnd()}");
        }
        else
            authHeader = null;
    }
    //public JsonObject Recv(Uri uri) {
    //}

    public async Task<bool> Patch(string endpoint, object body)
    {
        try
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Patch,
                RequestUri = new Uri(Addr + endpoint),
                Content = new StringContent(JsonSerializer.Serialize(body),
                    new MediaTypeHeaderValue("application/merge-patch+json"))
            };
            if (authHeader != null)
                request.Headers.Add(authHeader.Item1, authHeader.Item2);
            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return true;
            else
                return false;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("no api seems to be available, running offline...");
            Console.WriteLine(e.Message);
            if (e.InnerException != null)
                Console.WriteLine(e.InnerException.Message);
        }

        //should not be possible to reach this point
        return false;
    }

    public async Task<int> Replicas(string deployment)
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(Addr + $"/apis/apps/v1/namespaces/default/deployments/{deployment}/scale"),
        };
        if (authHeader != null)
            request.Headers.Add(authHeader.Item1, authHeader.Item2);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request);
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("kubernetes seems to be down...");
            return 0;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        if (json == null)
            return 0;
        var spec = json["spec"];
        if (spec == null)
            return 0;
        var replicasObj = spec["replicas"];
        if (replicasObj == null)
            return 0;
        var replicas = (int)replicasObj;
        return replicas;
    }
}