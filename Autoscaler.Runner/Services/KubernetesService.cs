using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Autoscaler.Runner.Services;

public class KubernetesService
{
    readonly HttpClient _client;
    readonly Tuple<string, string>? _authHeader;
    readonly string _addr;
    private readonly bool _useMockData;

    public KubernetesService(string addr,bool useMockData)
    {
        _addr = addr;
        _useMockData = useMockData;
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
    }

    public async Task Update(string endpoint, object body)
    {
        try
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Patch,
                RequestUri = new Uri(_addr + endpoint),
                Content = new StringContent(JsonConvert.SerializeObject(body),
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

    public async Task<JsonObject?> Get(string endpoint)
    {

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
    
    public async Task<int> GetReplicas(string deploymentName)
    {
        var json = await Get($"/apis/apps/v1/namespaces/default/deployments/{deploymentName}/scale");
        if (json == null)
            return 0;
        var spec = json["spec"];
        if (spec == null)
            return 0;
        var replicas = spec["replicas"];
        if (replicas == null)
            return 0;
        return (int)replicas;
    }

    static void HandleException(Exception e)
    {
        // TODO: Move to an interface
        Console.WriteLine(e.Message);
        if (e.InnerException != null)
            HandleException(e.InnerException);
    }
}