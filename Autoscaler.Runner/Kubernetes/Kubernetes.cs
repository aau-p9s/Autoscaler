using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Autoscaler.Runner.Kubernetes;

// TODO:  make an actual more complete mapping of kubernetes responses
using KubernetesResponse = JsonObject;

public class Kubernetes : IAPI {
    readonly HttpClientHandler Handler;
    readonly HttpClient Client;
    readonly Tuple<string, string>? AuthHeader;
    readonly string Addr;

    public Kubernetes(string addr) {
        Addr = addr;
        Handler = new() {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (_,_,_,_) => true
        };
        Client = new(Handler);
        if (File.Exists("/var/run/secrets/kubernetes.io/serviceaccount/token")) {
            StreamReader stream = new("/var/run/secrets/kubernetes.io/serviceaccount/token");
            AuthHeader = new("Authorization", $"Bearer {stream.ReadToEnd()}");
        } else {
            AuthHeader = null;
        }
        if(!IsUp()) {
            Console.WriteLine("Kubernetes shouldn't be down");
            Environment.Exit(1);
        }
    }

    public async Task<bool> Update(string endpoint, object body) {
        if(!IsUp()) {
            return false;
        }
        try {
            var request = new HttpRequestMessage {
                Method = HttpMethod.Patch,
                RequestUri = new Uri(Addr + endpoint),
                Content = new StringContent(JsonSerializer.Serialize(body), new MediaTypeHeaderValue("application/merge-patch+json"))
            };
            
            if (AuthHeader != null)
                request.Headers.Add(AuthHeader.Item1, AuthHeader.Item2);
            var response = await Client.SendAsync(request);
            return response.IsSuccessStatusCode;
        } catch (HttpRequestException e) {
            Console.WriteLine("Kubernetes seems to be down");
            HandleException(e);
        }
        return false;
    }

    public async Task<KubernetesResponse> Get(string endpoint) {
        if(!IsUp()) {
            return new();
        }
        var request = new HttpRequestMessage {
            Method = HttpMethod.Get,
            RequestUri = new Uri(Addr + endpoint)
        };
        if (AuthHeader != null) {
            request.Headers.Add(AuthHeader.Item1, AuthHeader.Item2);
        }
        HttpResponseMessage response;
        try {
            response = await Client.SendAsync(request);
        } catch(HttpRequestException e) {
            Console.WriteLine("Kubernetes seems to be down");
            HandleException(e);
            return null;
        }
        return await response.Content.ReadFromJsonAsync<JsonObject>();
    }
    static void HandleException(Exception e) { // TODO: Move to an interface
        Console.WriteLine(e.Message);
        if (e.InnerException != null)
            HandleException(e.InnerException);

    }
    
    public bool IsUp() { // TODO: implement actual checking function
        return false;
    }
}
