using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.ComponentModel;
using System.IO;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using WatercoolerTemp.Core;
using WatercoolerTemp.ViewModels;

namespace WatercoolerTemp.Views;

public partial class MainWindow : Window
{
    private readonly Forms.NotifyIcon trayIcon;
    private readonly Forms.ToolStripMenuItem showMenuItem;
    private readonly Forms.ToolStripMenuItem startMenuItem;
    private readonly Forms.ToolStripMenuItem stopMenuItem;
    private readonly Drawing.Icon trayIconImage;
    private readonly MainViewModel viewModel;
    private bool isClosing;

    public MainViewModel ViewModel => viewModel;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainViewModel();
        DataContext = viewModel;

        showMenuItem = new Forms.ToolStripMenuItem("Mostrar janela");
        startMenuItem = new Forms.ToolStripMenuItem("Iniciar monitoramento");
        stopMenuItem = new Forms.ToolStripMenuItem("Parar monitoramento");
        showMenuItem.Click += (_, _) => ShowWindowFromTray();
        startMenuItem.Click += (_, _) => viewModel.StartMonitoring();
        stopMenuItem.Click += async (_, _) => await viewModel.StopMonitoringAsync();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(showMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(startMenuItem);
        menu.Items.Add(stopMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        var exitMenuItem = new Forms.ToolStripMenuItem("Sair");
        exitMenuItem.Click += (_, _) => Close();
        menu.Items.Add(exitMenuItem);

        trayIconImage = new Drawing.Icon(Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "AquaControl.ico"));

        trayIcon = new Forms.NotifyIcon
        {
            Icon = trayIconImage,
            Text = "Aqua Control",
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.DoubleClick += (_, _) =>
        {
            if (IsVisible && WindowState != WindowState.Minimized)
                HideToTray();
            else
                ShowWindowFromTray();
        };
        // Clique simples também restaura: é a convenção da maioria dos
        // utilitários de bandeja do Windows.
        trayIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
                ShowWindowFromTray();
        };
        viewModel.PropertyChanged += ViewModelPropertyChanged;
        viewModel.HighTemperatureAlert += ShowHighTemperatureAlert;
        UpdateTrayState();
    }

    private void DragWindow(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeWindow(object sender, RoutedEventArgs eventArgs)
    {
        WindowState = WindowState.Minimized;
    }

    private void ResizeWindow(object sender, DragDeltaEventArgs eventArgs)
    {
        Width = Math.Max(MinWidth, Width + eventArgs.HorizontalChange);
        Height = Math.Max(MinHeight, Height + eventArgs.VerticalChange);
    }

    private void CloseWindow(object sender, RoutedEventArgs eventArgs)
    {
        Close();
    }

    public void HideToTray()
    {
        WindowState = WindowState.Normal;
        Hide();
    }

    public void ShowWindowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        UpdateTrayState();
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(MainViewModel.DisplayedTemperature)
            or nameof(MainViewModel.IsConnected)
            or nameof(MainViewModel.IsMonitoring))
        {
            UpdateTrayState();
        }
    }

    private void UpdateTrayState()
    {
        // O tooltip não pode continuar anunciando a última temperatura quando
        // o cooler foi desconectado: isso dava a impressão de leitura ativa.
        string state = !viewModel.IsConnected
            ? "nenhum water cooler conectado"
            : viewModel.IsMonitoring
                ? $"{viewModel.TemperatureText} °C"
                : "monitoramento pausado";

        trayIcon.Text = $"Aqua Control — {state}";
        showMenuItem.Enabled = !IsVisible || WindowState == WindowState.Minimized;
        startMenuItem.Enabled = viewModel.IsConnected && !viewModel.IsMonitoring;
        stopMenuItem.Enabled = viewModel.IsMonitoring;
    }

    private void ShowHighTemperatureAlert(int temperature)
    {
        trayIcon.ShowBalloonTip(
            5000,
            "Aqua Control - Temperatura alta",
            $"A temperatura do processador atingiu {temperature} °C.",
            Forms.ToolTipIcon.Warning);
    }

    private void WindowClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (isClosing)
            return;

        // A janela não fecha direto: mostra a confirmação dentro do próprio
        // tema. Se veio do menu da bandeja, traz a janela para a frente antes.
        eventArgs.Cancel = true;

        if (!IsVisible)
            ShowWindowFromTray();

        if (ExitOverlay.Visibility == Visibility.Visible)
            return;

        ContentFrame.IsEnabled = false;
        ExitOverlay.Visibility = Visibility.Visible;
        CancelExitButton.Focus();
    }

    private void CancelExit(object sender, RoutedEventArgs eventArgs)
    {
        HideExitConfirmation();
    }

    private async void ConfirmExit(object sender, RoutedEventArgs eventArgs)
    {
        if (isClosing)
            return;

        isClosing = true;
        ExitOverlay.Visibility = Visibility.Collapsed;

        try
        {
            await viewModel.DisposeAsync();
        }
        catch (Exception exception)
        {
            AppLogger.Error("Falha ao encerrar o monitoramento", exception);
        }
        finally
        {
            viewModel.PropertyChanged -= ViewModelPropertyChanged;
            viewModel.HighTemperatureAlert -= ShowHighTemperatureAlert;
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayIconImage.Dispose();
            Close();
        }
    }

    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape && ExitOverlay.Visibility == Visibility.Visible)
        {
            HideExitConfirmation();
            eventArgs.Handled = true;
        }
    }

    private void HideExitConfirmation()
    {
        ExitOverlay.Visibility = Visibility.Collapsed;
        ContentFrame.IsEnabled = true;
    }
}