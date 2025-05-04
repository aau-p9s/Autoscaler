using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Autoscaler.Runner.Services
{
    public class KubernetesService
    {
        readonly HttpClient _client;
        readonly Tuple<string, string>? _authHeader;
        readonly string _addr;
        private readonly bool _useMockData;

        private readonly bool _debugLogging;

        public KubernetesService(string addr, bool useMockData, bool debugLogging)
        {
            _addr = addr;
            _useMockData = useMockData;
            _debugLogging = debugLogging;
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
            if (_useMockData)
            {
                Console.WriteLine("Using mock Kubernetes data...");
                return;
            }

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

        public async Task<JObject?> Get(string endpoint)
        {
            if (_useMockData)
            {
                Console.WriteLine("Using mock Kubernetes data...");
                var kubeRes =
                    await File.ReadAllTextAsync(
                        "./DevelopmentData/kubectl_GET__apis_apps_v1_namespaces_default_deployments.json");
                return JObject.Parse(kubeRes);
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

            var responseString = await response.Content.ReadAsStringAsync();
            
            if (_debugLogging) Console.WriteLine($"Kubernetes response raw: {responseString}");

            return JObject.Parse(responseString);
        }

        public async Task<int> GetReplicas(string deploymentName)
        {
            if (_useMockData)
            {
                Console.WriteLine("Using mock Kubernetes data...");
                var kubeRes =
                    await File.ReadAllTextAsync(
                        "./DevelopmentData/kubectl_GET__apis_apps_v1_namespaces_default_deployments.json");
                var dummyJson = JObject.Parse(kubeRes);
                if (dummyJson == null)
                    return 0;
                var dummySpec = dummyJson["spec"];
                if (dummySpec == null)
                    return 0;
                var dummyReplicas = dummySpec["replicas"];
                if (dummyReplicas == null)
                    return 0;
                return (int)dummyReplicas;
            }

            var json = await Get($"/apis/apps/v1/namespaces/default/deployments/{deploymentName}/scale");
            if (_debugLogging) Console.WriteLine(json);
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

        private async Task<JObject?> GetPodsAsync(string serviceName)
        {
            if (_useMockData)
            {
                Console.WriteLine("Using mock Kubernetes pod data...");
                var podsJson = await File.ReadAllTextAsync("./DevelopmentData/containers.json");
                if (_debugLogging) Console.WriteLine(podsJson);
                return JObject.Parse(podsJson);
            }

            // Assuming pods are labeled with "app" equal to the service name.
            string encodedServiceName = Uri.EscapeDataString(serviceName);
            var endpoint = $"/api/v1/namespaces/default/pods?labelSelector=app%3D{encodedServiceName}";
            return await Get(endpoint);
        }

        public async Task<TimeSpan> GetPodStartupTimePercentileAsync(string serviceName, double percentile = 90)
        {
            // Retrieve pod data.
            JObject? podsJson = await GetPodsAsync(serviceName);
            if (_debugLogging) Console.WriteLine(podsJson);
            if (podsJson == null)
            {
                return TimeSpan.FromMinutes(1);
            }

            var items = podsJson["items"] as JArray;
            if (items == null || items.Count == 0)
            {
                return TimeSpan.FromMinutes(1);
            }

            var startupTimesSeconds = new List<double>();

            foreach (var item in items)
            {
                try
                {
                    var metadata = item["metadata"];
                    var creationTimeStr = metadata?["creationTimestamp"]?.ToString();
                    if (string.IsNullOrEmpty(creationTimeStr))
                        continue;
                    DateTime creationTime = DateTime.Parse(creationTimeStr);

                    var conditions = item["status"]?["conditions"] as JArray;
                    if (conditions == null)
                        continue;
                    DateTime? readyTime = null;
                    foreach (var condition in conditions)
                    {
                        if (condition["type"]?.ToString() == "Ready" && condition["status"]?.ToString() == "True")
                        {
                            var readyTimeStr = condition["lastTransitionTime"]?.ToString();
                            if (!string.IsNullOrEmpty(readyTimeStr))
                            {
                                readyTime = DateTime.Parse(readyTimeStr);
                                break;
                            }
                        }
                    }

                    if (readyTime == null)
                        continue;
                    var startupTime = readyTime.Value - creationTime;
                    startupTimesSeconds.Add(startupTime.TotalSeconds);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing pod: {ex.Message}");
                }
            }

            if (startupTimesSeconds.Count == 0)
            {
                return TimeSpan.FromMinutes(1);
            }

            startupTimesSeconds.Sort();
            double percentileValue = CalculatePercentile(startupTimesSeconds, percentile);
            return TimeSpan.FromSeconds(Math.Ceiling(percentileValue));
        }

        private double CalculatePercentile(List<double> sortedValues, double percentile)
        {
            // percentile is between 0 and 100.
            int n = sortedValues.Count;
            if (n == 0)
                return 0;
            double rank = (percentile / 100.0) * (n - 1);
            int lowerIndex = (int)Math.Floor(rank);
            int upperIndex = (int)Math.Ceiling(rank);
            if (lowerIndex == upperIndex)
            {
                return sortedValues[lowerIndex];
            }

            double weight = rank - lowerIndex;
            return sortedValues[lowerIndex] * (1 - weight) + sortedValues[upperIndex] * weight;
        }

        static void HandleException(Exception e)
        {
            Console.WriteLine(e.Message);
            if (e.InnerException != null)
                HandleException(e.InnerException);
        }
    }
}