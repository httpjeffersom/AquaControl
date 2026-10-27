using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WatercoolerTemp.Core;

namespace WatercoolerTemp.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private const int IntervalMs = 1000;
    private const int ReconnectIntervalMs = 5000;
    private const int HighTemperatureThreshold = 90;
    private const int AlertResetThreshold = 80;
    private readonly DispatcherTimer displayTimer;
    private readonly DispatcherTimer reconnectTimer;
    private WatercoolerDeviceManager? deviceManager;
    private bool monitoringRequested = true;
    private bool isConnecting;
    private bool isDisposing;
    private string status = "DESCONECTADO";
    private string message = "PRONTO";
    private string deviceSummary = "NENHUM DISPOSITIVO";
    private double targetTemperature;
    private double displayedTemperature;
    private bool isConnected;
    private bool isMonitoring;
    private int? minimumTemperature;
    private int? maximumTemperature;
    private int? cpuUsage;
    private double averageTemperature;
    private int temperatureSamples;
    private bool highTemperatureAlertShown;

    public MainViewModel()
    {
        displayTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        displayTimer.Tick += AnimateDisplay;
        reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ReconnectIntervalMs) };
        reconnectTimer.Tick += async (_, _) => await AttemptConnectAsync();

        reconnectTimer.Start();
        _ = AttemptConnectAsync();
    }

    public bool IsStartupEnabled
    {
        get => StartupManager.IsEnabled;
        set
        {
            if (value == IsStartupEnabled)
                return;

            try
            {
                StartupManager.SetEnabled(value);
                OnPropertyChanged();
            }
            catch (Exception exception)
            {
                Message = exception.Message;
                OnPropertyChanged();
            }
        }
    }

    public bool IsMonitoring
    {
        get => isMonitoring;
        private set
        {
            SetField(ref isMonitoring, value);
        }
    }

    public string Status
    {
        get => status;
        private set => SetField(ref status, value);
    }

    public string Message
    {
        get => message;
        private set => SetField(ref message, value);
    }

    public string DeviceSummary
    {
        get => deviceSummary;
        private set => SetField(ref deviceSummary, value);
    }

    public string MinimumTemperatureText => minimumTemperature is null ? "--" : $"{minimumTemperature} °C";
    public string MaximumTemperatureText => maximumTemperature is null ? "--" : $"{maximumTemperature} °C";
    public string AverageTemperatureText => temperatureSamples == 0 ? "--" : $"{averageTemperature:0} °C";
    public string CpuUsageText => cpuUsage is null ? "--%" : $"{cpuUsage}%";
    public System.Windows.Media.Brush CpuUsageBrush => cpuUsage switch
    {
        null => DisconnectedBrush,
        >= 90 => HighCpuUsageBrush,
        >= 80 => ElevatedCpuUsageBrush,
        _ => ConnectedBrush
    };

    public double DisplayedTemperature
    {
        get => displayedTemperature;
        private set
        {
            if (SetField(ref displayedTemperature, value))
            {
                OnPropertyChanged(nameof(TemperatureText));
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(IndicatorBrush));
            }
        }
    }

    public string TemperatureText => $"{Math.Round(DisplayedTemperature):0}";
    public double Progress => Math.Clamp(DisplayedTemperature, 0, 100) / 100;
    public System.Windows.Media.Brush IndicatorBrush => CreateBrush(GetTemperatureColor(DisplayedTemperature));
    public System.Windows.Media.Brush StatusBrush => IsConnected ? ConnectedBrush : DisconnectedBrush;
    public bool IsConnected
    {
        get => isConnected;
        private set
        {
            if (SetField(ref isConnected, value))
            {
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<int>? HighTemperatureAlert;

    private async Task AttemptConnectAsync()
    {
        if (isConnecting || isDisposing)
            return;

        isConnecting = true;
        try
        {
            deviceManager ??= CreateDeviceManager();

            bool wasConnected = IsConnected;
            await deviceManager.ReconcileAsync();

            if (monitoringRequested && deviceManager.IsConnected && !deviceManager.IsMonitoring)
                deviceManager.StartMonitoring();

            if (!wasConnected && deviceManager.IsConnected)
                ResetStatistics();

            UpdateConnectionState();
        }
        catch (Exception exception)
        {
            AppLogger.Error("Falha ao localizar ou conectar ao watercooler", exception);
            UpdateConnectionState();
        }
        finally
        {
            isConnecting = false;
        }
    }

    private WatercoolerDeviceManager CreateDeviceManager()
    {
        var manager = new WatercoolerDeviceManager();
        manager.TemperatureRead += OnTemperatureRead;
        manager.CpuUsageRead += OnCpuUsageRead;
        manager.TemperatureSent += OnTemperatureSent;
        manager.TemperatureUnavailable += OnTemperatureUnavailable;
        manager.DevicesChanged += OnDevicesChanged;
        manager.Error += OnManagerError;
        return manager;
    }

    private void OnDevicesChanged()
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(UpdateConnectionState);
            return;
        }

        UpdateConnectionState();
    }

    private void UpdateConnectionState()
    {
        bool connected = deviceManager?.IsConnected == true;
        bool monitoring = deviceManager?.IsMonitoring == true;
        bool connectionChanged = IsConnected != connected;
        bool monitoringChanged = IsMonitoring != monitoring;

        IsConnected = connected;
        IsMonitoring = monitoring;
        DeviceSummary = deviceManager?.DeviceSummary ?? "NENHUM DISPOSITIVO";

        if (connectionChanged || monitoringChanged)
        {
            Status = connected ? "CONECTADO" : "DESCONECTADO";
            Message = !connected
                ? "AGUARDANDO WATERCOOLER"
                : monitoring
                    ? "MONITORAMENTO ATIVO"
                    : "MONITORAMENTO PAUSADO";
        }
    }

    public void StartMonitoring()
    {
        if (isDisposing)
            return;

        monitoringRequested = true;
        deviceManager?.StartMonitoring();
        UpdateConnectionState();
    }

    public async Task StopMonitoringAsync(bool reportErrors = true)
    {
        monitoringRequested = false;
        if (deviceManager is null)
            return;

        try
        {
            await deviceManager.StopMonitoringAsync();
        }
        catch (Exception exception)
        {
            if (reportErrors)
                Message = exception.Message;
        }

        UpdateConnectionState();
    }

    private void OnTemperatureRead(int temperature)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            targetTemperature = Math.Clamp(temperature, 0, 100);
            EvaluateTemperatureAlert(temperature);
            temperatureSamples++;
            averageTemperature = ((averageTemperature * (temperatureSamples - 1)) + temperature) / temperatureSamples;
            minimumTemperature = minimumTemperature is null ? temperature : Math.Min(minimumTemperature.Value, temperature);
            maximumTemperature = maximumTemperature is null ? temperature : Math.Max(maximumTemperature.Value, temperature);
            displayTimer.Start();
            OnPropertyChanged(nameof(MinimumTemperatureText));
            OnPropertyChanged(nameof(MaximumTemperatureText));
            OnPropertyChanged(nameof(AverageTemperatureText));
        });
    }

    private void OnCpuUsageRead(int? usage)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            cpuUsage = usage;
            OnPropertyChanged(nameof(CpuUsageText));
            OnPropertyChanged(nameof(CpuUsageBrush));
        });
    }

    private void OnTemperatureSent(int temperature, byte[] pacote, string deviceName)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            string suffix = deviceManager?.ConnectedDeviceCount > 1
                ? $" • {deviceName}"
                : string.Empty;
            Message = $"TEMPERATURA ENVIADA  {temperature:00} °C{suffix}";
        });
    }

    private void EvaluateTemperatureAlert(int temperature)
    {
        if (temperature <= AlertResetThreshold)
        {
            highTemperatureAlertShown = false;
            return;
        }

        if (temperature >= HighTemperatureThreshold && !highTemperatureAlertShown)
        {
            highTemperatureAlertShown = true;
            HighTemperatureAlert?.Invoke(temperature);
        }
    }

    private void OnTemperatureUnavailable()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => Message = "SENSOR DA CPU INDISPONÍVEL");
    }

    private void OnManagerError(Exception exception)
    {
        // O gerenciador já removeu a sessão com falha. Se ainda houver outro
        // water cooler ativo, o programa continua normalmente com ele.
        AppLogger.Error("Monitoramento de um watercooler desconectado", exception);
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (deviceManager?.IsConnected != true)
            {
                Status = "ERRO DE CONEXÃO";
                Message = "TENTANDO RECONECTAR";
            }
        });
    }

    private void AnimateDisplay(object? sender, EventArgs eventArgs)
    {
        double difference = targetTemperature - DisplayedTemperature;
        if (Math.Abs(difference) < 0.05)
        {
            DisplayedTemperature = targetTemperature;
            displayTimer.Stop();
            return;
        }

        DisplayedTemperature += difference * 0.14;
    }

    private static System.Windows.Media.Color GetTemperatureColor(double temperature)
    {
        if (temperature <= 70)
            return System.Windows.Media.Color.FromRgb(83, 235, 151);
        if (temperature <= 80)
            return Interpolate(System.Windows.Media.Color.FromRgb(83, 235, 151), System.Windows.Media.Color.FromRgb(245, 213, 73), (temperature - 70) / 10);
        if (temperature <= 90)
            return Interpolate(System.Windows.Media.Color.FromRgb(245, 213, 73), System.Windows.Media.Color.FromRgb(245, 137, 54), (temperature - 80) / 10);

        return Interpolate(System.Windows.Media.Color.FromRgb(245, 137, 54), System.Windows.Media.Color.FromRgb(255, 65, 75), (temperature - 90) / 10);
    }

    private static System.Windows.Media.Color Interpolate(System.Windows.Media.Color from, System.Windows.Media.Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return System.Windows.Media.Color.FromRgb(
            (byte)(from.R + (to.R - from.R) * amount),
            (byte)(from.G + (to.G - from.G) * amount),
            (byte)(from.B + (to.B - from.B) * amount));
    }

    public async ValueTask DisposeAsync()
    {
        isDisposing = true;
        reconnectTimer.Stop();

        if (deviceManager is not null)
        {
            deviceManager.TemperatureRead -= OnTemperatureRead;
            deviceManager.CpuUsageRead -= OnCpuUsageRead;
            deviceManager.TemperatureSent -= OnTemperatureSent;
            deviceManager.TemperatureUnavailable -= OnTemperatureUnavailable;
            deviceManager.DevicesChanged -= OnDevicesChanged;
            deviceManager.Error -= OnManagerError;

            await deviceManager.DisposeAsync();
            deviceManager = null;
        }

        displayTimer.Stop();
    }

    private void ResetStatistics()
    {
        minimumTemperature = null;
        maximumTemperature = null;
        averageTemperature = 0;
        temperatureSamples = 0;
        OnPropertyChanged(nameof(MinimumTemperatureText));
        OnPropertyChanged(nameof(MaximumTemperatureText));
        OnPropertyChanged(nameof(AverageTemperatureText));
    }

    private static readonly System.Windows.Media.Brush ConnectedBrush = CreateBrush(System.Windows.Media.Color.FromRgb(83, 235, 151));
    private static readonly System.Windows.Media.Brush DisconnectedBrush = CreateBrush(System.Windows.Media.Color.FromRgb(104, 116, 113));
    private static readonly System.Windows.Media.Brush ElevatedCpuUsageBrush = CreateBrush(System.Windows.Media.Color.FromRgb(245, 137, 54));
    private static readonly System.Windows.Media.Brush HighCpuUsageBrush = CreateBrush(System.Windows.Media.Color.FromRgb(255, 65, 75));

    private static System.Windows.Media.Brush CreateBrush(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}