using Icepick.Extensions;
using Syringe;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace Icepick.Mods
{
	public class SDKInjector
	{
		[StructLayout( LayoutKind.Sequential, Pack = 8 )]
		struct SDKSettings
		{
			[CustomMarshalAs( CustomUnmanagedType.LPStr )] public string BasePath;
			public bool DeveloperMode;
		};

		private const int OriginInjectionTimeout = 30;
		private const int SteamInjectionTimeout = 60; // Steam needs to launch Origin, so give it longer to load everything

		private const string OriginProcessName = "Origin";
		private const string EADesktopProcessName = "EADesktop";
		private const string TitanfallProcessName = "Titanfall2";
		private const string SteamProxyProcessName = "EASteamProxy";
		private const string LaunchViaSteamUrl = "steam://run/1237970";

		public const string SDKDllName = "TTF2SDK.dll";
		private const string SDKDataPath = @"data\";
		private const string InitializeFunction = "InitialiseSDK";

		public delegate void InjectorEventDelegate( string message = null );
		public static event InjectorEventDelegate OnLaunchingProcess;
		public static event InjectorEventDelegate OnInjectingIntoProcess;
		public static event InjectorEventDelegate OnInjectionComplete;
		public static event InjectorEventDelegate OnInjectionException;

        public static async void LaunchAndInject()
        {
            // 可选：通知 UI 正在监听
            OnLaunchingProcess?.Invoke();

            // 直接监听 Titanfall2.exe 的进程并注入
            await WatchAndInject(TitanfallProcessName, 30000);// 30秒内检测是否启动成功
        }


        protected static async Task WatchAndInject(string gamePath, int injectionTimeout)
        {
            string gameProcessName = System.IO.Path.GetFileNameWithoutExtension(gamePath);
            DateTime startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalSeconds < injectionTimeout)
            {
                Process[] ttfProcesses = Process.GetProcessesByName(gameProcessName);
                if (ttfProcesses.Length > 0)
                {
                    Process ttfProcess = ttfProcesses[0];
                    try
                    {
                        foreach (ProcessModule module in ttfProcess.Modules)
                        {
                            if (module.ModuleName == "tier0.dll")
                            {
                                InjectSDK(ttfProcess);
                                return;
                            }
                        }
                    }
                    catch (Win32Exception e)
                    {
                        OnInjectionException?.Invoke(e.Message + ", Error Code " + e.NativeErrorCode);
                    }
                    catch (Exception e)
                    {
                        OnInjectionException?.Invoke(e.Message);
                    }
                }

                await Task.Delay(500);
            }

            string timeoutError = $"Timed out after {injectionTimeout} seconds. Could not find Titanfall 2 process.";
            OnInjectionException?.Invoke(timeoutError);
            MessageBox.Show(timeoutError, "Injection Failed", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }


        protected static void InjectSDK( Process targetProcess )
		{
			if( OnInjectingIntoProcess != null )
			{
				OnInjectingIntoProcess();
			}

			Injector syringe = new Injector( targetProcess );
			syringe.SetDLLSearchPath( AppDomain.CurrentDomain.BaseDirectory );
			syringe.InjectLibrary( SDKDllName );

			SDKSettings settings = new SDKSettings();
			settings.BasePath = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, SDKDataPath );
			settings.DeveloperMode = Api.IcepickRegistry.ReadEnableDeveloperMode();
			syringe.CallExport( SDKDllName, InitializeFunction, settings );

			if( OnInjectionComplete != null )
			{
				OnInjectionComplete();
			}
		}

	}
}
