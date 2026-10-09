using System;
using System.Threading;
using System.Windows;
using WatercoolerTemp.Core;
using WatercoolerTemp.Views;

namespace WatercoolerTemp;

public partial class App : System.Windows.Application
{
	private const string InstanceName = "WatercoolerTemp.SingleInstance";
	private Mutex? instanceMutex;
	private EventWaitHandle? activationEvent;
	private RegisteredWaitHandle? activationRegistration;
	private bool ownsInstanceMutex;

	protected override void OnStartup(StartupEventArgs eventArgs)
	{
		base.OnStartup(eventArgs);

		try
		{
			instanceMutex = new Mutex(true, InstanceName, out bool isFirstInstance);
			ownsInstanceMutex = isFirstInstance;
			if (!isFirstInstance)
			{
				using EventWaitHandle signal = EventWaitHandle.OpenExisting(InstanceName + ".Activate");
				signal.Set();
				Shutdown();
				return;
			}

			activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Activate");
			activationRegistration = ThreadPool.RegisterWaitForSingleObject(
				activationEvent,
				(_, _) => Dispatcher.BeginInvoke(new Action(() => (MainWindow as MainWindow)?.ShowWindowFromTray())),
				null,
				Timeout.Infinite,
				false);

			MainWindow = new MainWindow();
			MainWindow.Show();
			if (StartupManager.IsStartupLaunch(eventArgs.Args))
				(MainWindow as MainWindow)?.HideToTray();
		}
		catch (Exception exception)
		{
			System.Windows.MessageBox.Show(
				exception.ToString(),
				"Falha ao iniciar o WatercoolerTemp",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			Shutdown(1);
		}
	}

	protected override void OnExit(ExitEventArgs eventArgs)
	{
		activationRegistration?.Unregister(null);
		activationEvent?.Dispose();
		if (ownsInstanceMutex)
			instanceMutex?.ReleaseMutex();
		instanceMutex?.Dispose();
		base.OnExit(eventArgs);
	}
}