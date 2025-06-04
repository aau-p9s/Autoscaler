using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autoscaler.Config;
using Autoscaler.Persistence.SettingsRepository;
using Microsoft.Extensions.Logging;

namespace Autoscaler.Runner.Services;

public class ForecasterService(
    AppSettings appSettings,
    ILogger logger,
    ISettingsRepository settingsRepository)
{
    protected AppSettings AppSettings => appSettings;
    private HttpClient Client => new();
    private ISettingsRepository SettingsRepository => settingsRepository;
    protected ILogger Logger => logger;

    public virtual async Task<bool> Forecast(Guid serviceId, int forecastHorizon)
    {
        var urlPrefix = $"{AppSettings.Autoscaler.Apis.Forecaster}/predict/{serviceId}";
        var res = await Client.PostAsync($"{urlPrefix}/{forecastHorizon}", new StringContent(""));
        Logger.LogDebug($"Forecaster forecast response: {await res.Content.ReadAsStringAsync()}");

        if (!res.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to forecast the data");
            return false;
        }

        var settings = await settingsRepository.GetSettingsForServiceAsync(serviceId);
        await Wait(urlPrefix, TimeSpan.FromMilliseconds(settings.TrainInterval));

        return true;
    }

    public virtual async Task<bool> Retrain(Guid serviceId, int forecastHorizon)
    {
        var urlPrefix = $"{AppSettings.Autoscaler.Apis.Forecaster}/train/{serviceId}";
        var res = await Client.PostAsync($"{urlPrefix}/{forecastHorizon}", new StringContent(""));

        if (!res.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to retrain the model");
            return false;
        }

        // wait for finishing
        var settings = await settingsRepository.GetSettingsForServiceAsync(serviceId);
        await Wait(urlPrefix, TimeSpan.FromMilliseconds(settings.TrainInterval));

        return true;
    }

    private async Task Wait(string urlPrefix, TimeSpan trainInterval)
    {
        var startTime = DateTime.Now;
        var status = HttpStatusCode.Accepted;
        while (status != HttpStatusCode.OK)
        {
            var elapsed = DateTime.Now - startTime;
            // Kill trainer or predicter if it takes too long
            if (elapsed > trainInterval)
            {
                await Client.GetAsync($"{urlPrefix}/kill");
                break;
            }

            var res = await Client.GetAsync($"{urlPrefix}/123");
            status = res.StatusCode;
            Thread.Sleep(1000);
        }
    }
}