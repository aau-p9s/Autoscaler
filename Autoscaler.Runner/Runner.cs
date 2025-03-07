using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Persistence.HistoricRepository;
using Autoscaler.Persistence.ServicesRepository;
using Autoscaler.Persistence.SettingsRepository;
using Autoscaler.Runner.Entities;
using Autoscaler.Runner.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Autoscaler.Runner;

public class Runner
{
    private List<DeploymentEntity> _deployments;
    private readonly IServiceProvider _serviceProvider;
    private readonly ForecasterService _forecaster;
    private readonly PrometheusService _prometheus;
    private readonly KubernetesService _kubernetes;
    private readonly List<Thread> _runningThreads;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public Runner(string forecasterAddress, string kubernetesAddress, string prometheusAddress,
        IServiceProvider serviceProvider, bool developmentMode = false)
    {
        _forecaster = new();
        _kubernetes = new(kubernetesAddress, developmentMode);
        _prometheus = new(prometheusAddress, developmentMode);
        _serviceProvider = serviceProvider;
        _deployments = new List<DeploymentEntity>();
        _runningThreads = new List<Thread>();
        _cancellationTokenSource = new CancellationTokenSource();
        Console.WriteLine("Created runner");
    }

    public async Task MainLoop()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var servicesRepository = scope.ServiceProvider.GetRequiredService<IServicesRepository>();
            var settingsRepository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            
            var services = await servicesRepository.GetAllServicesAsync();
            foreach (var service in services)
            {
                var settings = await settingsRepository.GetSettingsForServiceAsync(service.Id);
                _deployments.Add(new DeploymentEntity(service, settings));
            }
        }

        foreach (var deployment in _deployments)
        {
            var thread = new Thread(() => DeploymentMonitorLoop(deployment, _cancellationTokenSource.Token));
            thread.Name = $"Monitor-{deployment.Service.Name}";
            thread.IsBackground = true;
            _runningThreads.Add(thread);
            thread.Start();
            Console.WriteLine($"Started monitoring thread for {deployment.Service.Name}");
        }

        foreach (var thread in _runningThreads)
        {
            thread.Join();
        }
    }

    private async void DeploymentMonitorLoop(DeploymentEntity deployment, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var startTime = DateTime.Now;
                
                try
                {
                    Console.WriteLine($"Checking deployment {deployment.Service.Name}");
                    
                    // Create a new scope for each iteration to get fresh repository instances
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var historicRepository = scope.ServiceProvider.GetRequiredService<IHistoricRepository>();
                        
                        var data = await _prometheus.QueryRange(
                            deployment.Service.Id,
                            deployment.Service.Name, 
                            DateTime.Now.AddHours(-12), 
                            DateTime.Now, 
                            deployment.Settings.ScalePeriod);

                        Console.WriteLine($"Data points for {deployment.Service.Name}: {data.HistoricData.Length}");

                        //var forecast = _forecaster.Forecast(data);

                        // Store historic data if needed
                        // await historicRepository.StoreHistoricDataAsync(...);

                        var replicas = await _kubernetes.GetReplicas(deployment.Service.Name);
                        if (true) // TODO: forecast.value > scaleUp
                            replicas++;
                        //if (false) // TODO: forecast.value <= scaleDown && replicas > 1
                        //    replicas--;
                        //if (false) // TODO: replicas < 1
                        //    replicas = 1;

                        Console.WriteLine($"Updating {deployment.Service.Name} to {replicas} replicas");

                        // Using JsonObject instead of Dictionary
                        var jsonObject = new
                        {
                            spec = new
                            {
                                replicas = replicas
                            }
                        };

                        await _kubernetes.Update($"/apis/apps/v1/namespaces/default/deployments/{deployment.Service.Name}/scale", 
                            jsonObject);
                    }

                    // Calculate delay based on the processing time
                    var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                    var delay = Math.Max(0, deployment.Settings.ScalePeriod - processingTime);
                    
                    if (true) // TODO: forecast.timestamp > (Datetime.Now - delay)
                    {
                        Console.WriteLine($"Thread {Thread.CurrentThread.Name} sleeping for {delay}ms");
                        await Task.Delay((int)delay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error monitoring {deployment.Service.Name}: {ex.Message}");
                    // Sleep for a short period before retrying
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.Name} was canceled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception in thread {Thread.CurrentThread.Name}: {ex}");
        }
    }

    public void Stop()
    {
        Console.WriteLine("Stopping all monitoring threads");
        _cancellationTokenSource.Cancel();
    }
}