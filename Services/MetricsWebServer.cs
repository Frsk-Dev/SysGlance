using System.Net;
using System.Text;
using System.Text.Json;

namespace SysGlance.Services;

/// <summary>
/// Local HTTP server that serves metric data and an HTML dashboard for the Xeneon Edge widget.
/// </summary>
internal sealed class MetricsWebServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly Func<SystemMetrics> getMetrics;
    private readonly Func<AppSettings> getSettings;
    private readonly int port;
    private CancellationTokenSource? cts;
    private Task? listenTask;
    private bool disposed;

    /// <summary>
    /// Initializes the web server for the given port and metric delegates.
    /// </summary>
    public MetricsWebServer(int port, Func<SystemMetrics> getMetrics, Func<AppSettings> getSettings)
    {
        this.port = port;
        this.getMetrics = getMetrics;
        this.getSettings = getSettings;
    }

    /// <summary>
    /// Starts listening for HTTP requests on the configured port.
    /// </summary>
    public void Start()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            cts = new CancellationTokenSource();
            listener.Prefixes.Clear();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();
            listenTask = Task.Run(() => ListenLoop(cts.Token));
        }
        catch (HttpListenerException)
        {
            // Port unavailable or permission denied.
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Stops the HTTP listener and cancels the listen loop.
    /// </summary>
    public void Stop()
    {
        cts?.Cancel();

        try
        {
            listener.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }

        try
        {
            listenTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Expected on cancellation.
        }
    }

    /// <summary>
    /// Disposes the listener and cancellation token.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();
        listener.Close();
        cts?.Dispose();
    }

    /// <summary>
    /// Main loop that accepts and dispatches incoming HTTP requests.
    /// </summary>
    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                HttpListenerContext context = await listener.GetContextAsync().WaitAsync(token);
                HandleRequest(context);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Routes the request to the appropriate handler.
    /// </summary>
    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            string path = context.Request.Url?.AbsolutePath ?? "/";

            if (path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
            {
                ServeMetricsJson(context.Response);
            }
            else
            {
                ServeDashboardHtml(context.Response);
            }
        }
        catch
        {
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // Response may already be closed.
            }
        }
    }

    /// <summary>
    /// Serves the JSON metrics endpoint with CORS headers.
    /// </summary>
    private void ServeMetricsJson(HttpListenerResponse response)
    {
        AppSettings currentSettings = getSettings();
        SystemMetrics currentMetrics = getMetrics();

        var metricsList = new List<object>();

        foreach (string key in currentSettings.MetricOrder)
        {
            if (!currentSettings.IsVisible(key))
            {
                continue;
            }

            float? rawValue = MetricFormatter.GetMetricValue(key, currentMetrics);
            string formattedValue = MetricFormatter.FormatMetricValue(key, currentMetrics);
            string label = AppSettings.GetOverlayLabel(key);
            string color = currentSettings.GetColor(key);

            metricsList.Add(new
            {
                key,
                label,
                value = formattedValue,
                rawValue = rawValue ?? 0f,
                color
            });
        }

        string json = JsonSerializer.Serialize(new
        {
            metrics = metricsList,
            bgColor = currentSettings.DashboardBgColor,
            cardColor = currentSettings.DashboardCardColor,
            fontScale = currentSettings.DashboardFontScale / 100.0
        });
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        response.ContentType = "application/json; charset=utf-8";
        response.Headers.Add("Access-Control-Allow-Origin", $"http://localhost:{port}");
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }

    /// <summary>
    /// Serves the HTML dashboard page.
    /// </summary>
    private void ServeDashboardHtml(HttpListenerResponse response)
    {
        string html = XeneonDashboardHtml.Generate(port);
        byte[] buffer = Encoding.UTF8.GetBytes(html);

        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }
}
