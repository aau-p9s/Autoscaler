using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Autoscaler.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Autoscaler.Runner.Services
{
    public class KubernetesService(
        AppSettings appSettings,
        ILogger logger)
    {
        private HttpClient Client => new(new HttpClientHandler()
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });

        private Tuple<string, string> AuthHeader => new("Authorization", $"Bearer {
            new StreamReader("/var/run/secrets/kubernetes.io/serviceaccount/token").ReadToEnd()
        }");

        protected ILogger Logger => logger;
        protected AppSettings AppSettings => appSettings;

        public virtual async Task Update(string endpoint, object body)
        {
            Logger.LogDebug($"Kubernetes endpoint: {endpoint}");
            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Patch,
                    RequestUri = new Uri(AppSettings.Autoscaler.Apis.Kubernetes + endpoint),
                    Content = new StringContent(JsonConvert.SerializeObject(body),
                        new MediaTypeHeaderValue("application/merge-patch+json"))
                };

                request.Headers.Add(AuthHeader.Item1, AuthHeader.Item2);
                var response = await Client.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();

                Logger.LogDebug($"Kubernetes response raw: {responseString}");
            }
            catch (HttpRequestException e)
            {
                Logger.LogError("Kubernetes seems to be down");
                Utils.HandleException(e, Logger);
            }
        }

        public virtual async Task<JObject?> Get(string endpoint)
        {
            Logger.LogDebug($"Kubernetes endpoint: {endpoint}");

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(AppSettings.Autoscaler.Apis.Kubernetes + endpoint)
            };
            request.Headers.Add(AuthHeader.Item1, AuthHeader.Item2);

            var response = await Client.SendAsync(request);

            var responseString = await response.Content.ReadAsStringAsync();

            Logger.LogDebug($"Kubernetes response raw: {responseString}");

            return JObject.Parse(responseString);
        }

        public virtual async Task<int> GetReplicas(string deploymentName)
        {
            var json = await Get($"/apis/apps/v1/namespaces/default/deployments/{deploymentName}/scale");
            Logger.LogDebug($"Json response: {json}");
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

        protected virtual async Task<JObject?> GetPodsAsync(string serviceName)
        {
            // Assuming pods are labeled with "app" equal to the service name.
            string encodedServiceName = Uri.EscapeDataString(serviceName);
            var endpoint = $"/api/v1/namespaces/default/pods?labelSelector=app%3D{encodedServiceName}";
            return await Get(endpoint);
        }

        public async Task<TimeSpan> GetPodStartupTimePercentileAsync(string serviceName, double percentile = 90)
        {
            // Retrieve pod data.
            JObject? podsJson = await GetPodsAsync(serviceName);
            Logger.LogDebug($"pods json: {podsJson}");
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
                    Logger.LogError($"Error processing pod: {ex.Message}");
                }
            }

            if (startupTimesSeconds.Count == 0)
            {
                return TimeSpan.FromMinutes(1);
            }

            startupTimesSeconds.Sort();
            double percentileValue = CalculatePercentile(startupTimesSeconds, percentile);
            return TimeSpan.FromMinutes(Math.Ceiling(percentileValue / 60.0));
        }

        private double CalculatePercentile(List<double> sortedValues, double percentile)
        {
            // percentile is between 0 and 100.
            var n = sortedValues.Count;
            if (n == 0)
                return 0;
            var rank = (percentile / 100.0) * (n - 1);
            var lowerIndex = (int)Math.Floor(rank);
            var upperIndex = (int)Math.Ceiling(rank);
            if (lowerIndex == upperIndex)
            {
                return sortedValues[lowerIndex];
            }

            double weight = rank - lowerIndex;
            return sortedValues[lowerIndex] * (1 - weight) + sortedValues[upperIndex] * weight;
        }
    }
}