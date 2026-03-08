using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Aloe.Apps.WindowsServiceMonitorClient.Models;
using Aloe.Apps.WindowsServiceMonitorLib.Models;

namespace Aloe.Apps.WindowsServiceMonitorClient.Services;

public class WindowsServiceMonitorHttpClient : IDisposable
{
    private readonly WindowsServiceMonitorClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;

    public WindowsServiceMonitorHttpClient(WindowsServiceMonitorClientOptions options)
    {
        this._options = options;
        this._cookieContainer = new CookieContainer();

        var handler = new HttpClientHandler
        {
            CookieContainer = this._cookieContainer,
            UseCookies = true
        };

        this._httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(this._options.ServerUrl)
        };
    }

    public async Task<List<ServiceInfo>?> GetServicesAsync()
    {
        try
        {
            var response = await this._httpClient.GetAsync("/api/services");

            if (!response.IsSuccessStatusCode)
                return null;

            var services = await response.Content.ReadFromJsonAsync<List<ServiceInfo>>();
            return services;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
    }
}
