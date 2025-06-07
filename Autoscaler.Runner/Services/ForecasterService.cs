using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
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

    public virtual async Task<bool> Forecast(Guid serviceId, TimeSpan forecastHorizon)
    {
        var urlPrefix = $"{AppSettings.Autoscaler.Apis.Forecaster.Url}/predict/{serviceId}";
        var res = await Client.PostAsync($"{urlPrefix}/{forecastHorizon.TotalSeconds}", new StringContent(""));
        Logger.LogDebug($"Forecaster forecast response: {await res.Content.ReadAsStringAsync()}");

        if (!res.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to forecast the data");
            return false;
        }

        var settings = await SettingsRepository.GetSettingsForServiceAsync(serviceId);
        await Wait(urlPrefix, TimeSpan.FromMilliseconds(settings.TrainInterval));

        return true;
    }

    public virtual async Task<bool> Retrain(Guid serviceId, TimeSpan forecastHorizon)
    {
        var urlPrefix = $"{AppSettings.Autoscaler.Apis.Forecaster.Url}/train/{serviceId}";
        var res = await Client.PostAsync($"{urlPrefix}/{forecastHorizon.TotalSeconds}", new StringContent(""));

        if (!res.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to retrain the model");
            return false;
        }

        // wait for finishing
        var settings = await SettingsRepository.GetSettingsForServiceAsync(serviceId);
        await Wait(urlPrefix, TimeSpan.FromMilliseconds(settings.TrainInterval));

        return true;
    }

    private async Task Wait(string urlPrefix, TimeSpan trainInterval)
    {
        var clock = new Stopwatch();
        clock.Start();
        var status = HttpStatusCode.Accepted;
        while (status != HttpStatusCode.OK)
        {
            // Let the trainer or predicter start
            await Task.Delay(10000);
            // Kill trainer or predicter if it takes too long
            if (clock.Elapsed > trainInterval)
            {
                await Client.GetAsync($"{urlPrefix}/kill");
                break;
            }

            var res = await Client.GetAsync($"{urlPrefix}");
            status = res.StatusCode;
        }
    }
}