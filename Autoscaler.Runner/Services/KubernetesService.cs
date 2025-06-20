using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Runner.Entities;
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

        private async Task Update(string endpoint, object body)
        {
            Logger.LogDebug($"Kubernetes endpoint: {endpoint}");
            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Patch,
                    RequestUri = new Uri(AppSettings.Autoscaler.Apis.Kubernetes.Url + endpoint),
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

        
        public virtual async Task SetReplicas(DeploymentEntity deployment, int replicas)
        {
            // Create the JSON object for scaling.
            var jsonObject = new
            {
                spec = new
                {
                    replicas
                }
            };
            await Update(
                $"/apis/apps/v1/namespaces/default/deployments/{deployment.Service.Name}/scale",
                jsonObject);
        }

        private async Task<JObject?> Get(string endpoint)
        {
            Logger.LogDebug($"Kubernetes endpoint: {endpoint}");

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(AppSettings.Autoscaler.Apis.Kubernetes.Url + endpoint)
            };
            request.Headers.Add(AuthHeader.Item1, AuthHeader.Item2);

            var response = await Client.SendAsync(request);

            var responseString = await response.Content.ReadAsStringAsync();

            Logger.LogDebug($"Kubernetes response raw: {responseString}");

            return JObject.Parse(responseString);
        }

        public virtual async Task<int> GetReplicas(DeploymentEntity deployment)
        {
            var json = await Get($"/apis/apps/v1/namespaces/default/deployments/{deployment.Service.Name}/scale");
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

        protected virtual async Task<JObject?> GetPodsAsync(DeploymentEntity deployment)
        {
            // Assuming pods are labeled with "app" equal to the service name.
            string encodedServiceName = Uri.EscapeDataString(deployment.Service.Name);
            var endpoint = $"/api/v1/namespaces/default/pods?labelSelector=app%3D{encodedServiceName}";
            return await Get(endpoint);
        }

        public async Task<TimeSpan> GetPodStartupTimePercentileAsync(DeploymentEntity deployment, double percentile = 90)
        {
            // Retrieve pod data.
            JObject? podsJson = await GetPodsAsync(deployment);
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
        
        
        public virtual async Task<List<string>> GetDeployments()
        {
            var exclusions = new List<string>()
            {
                "forecaster",
                "postgres",
                "coredns",
                "local-path-provisioner",
                "metrics-server",
                "traefik",
                "grafana",
                "prometheus-kube-state-metrics",
                "prometheus-prometheus-pushgateway",
                "prometheus-server"
            };
            var deployments = new List<string>();
            var response = await Get("/apis/apps/v1/deployments") ??
                           throw new ArgumentNullException("", "Kubernetes response is null");
            var items = response["items"] ??
                        throw new ArgumentNullException(nameof(response), "Response from kubernetes was null");

            foreach (var item in items)
            {
                var metadata = item["metadata"] ??
                               throw new ArgumentNullException(nameof(item), "Item did not have metadata");
                var name = metadata["name"] ??
                           throw new ArgumentNullException(nameof(metadata), "Metadata did not have a name");
                deployments.Add(name.ToString());
            }

            return deployments.Where(deployment => !exclusions.Contains(deployment)).ToList();
        }
    }
    
}