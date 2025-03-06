using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Autoscaler.Runner.Kubernetes;

// TODO:  make an actual more complete mapping of kubernetes responses
using KubernetesResponse = JsonObject;

public class Kubernetes : IAPI
{
    readonly HttpClient _client;
    readonly Tuple<string, string>? _authHeader;
    readonly string _addr;

    public Kubernetes(string addr)
    {
        _addr = addr;
        HttpClientHandler handler = new()
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _client = new(handler);
        if (File.Exists("/var/run/secrets/kubernetes.io/serviceaccount/token"))
        {
            StreamReader stream = new("/var/run/secrets/kubernetes.io/serviceaccount/token");
            _authHeader = new("Authorization", $"Bearer {stream.ReadToEnd()}");
        }
        else
        {
            _authHeader = null;
        }

        if (!IsUp())
        {
            Console.WriteLine("Kubernetes shouldn't be down");
            Environment.Exit(1);
        }
    }

    public async Task Update(string endpoint, object body)
    {
        if (!IsUp())
        {
            return;
        }

        try
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Patch,
                RequestUri = new Uri(_addr + endpoint),
                Content = new StringContent(JsonSerializer.Serialize(body),
                    new MediaTypeHeaderValue("application/merge-patch+json"))
            };

            if (_authHeader != null)
                request.Headers.Add(_authHeader.Item1, _authHeader.Item2);
            await _client.SendAsync(request);
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("Kubernetes seems to be down");
            HandleException(e);
        }
    }

    public async Task<KubernetesResponse?> Get(string endpoint)
    {
        if (!IsUp())
        {
            return new();
        }

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(_addr + endpoint)
        };
        if (_authHeader != null)
        {
            request.Headers.Add(_authHeader.Item1, _authHeader.Item2);
        }

        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(request);
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("Kubernetes seems to be down");
            HandleException(e);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<JsonObject>();
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
        // TODO: implement actual checking function
        return false;
    }
}