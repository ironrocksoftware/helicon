
using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Configuration.Install;
using System.IO;
using System.Reflection;
using System.Threading;

namespace servx
{
	class Servx : ServiceBase
	{
		private static string exePath;
		private static string svcName;

		private static string xmlconfig;
		private static Thread thread;
		private static bool finalize;

		public Servx (string p_svcName, string p_xmlconfig)
		{
			this.ServiceName = p_svcName;
			this.CanStop = true;
			this.CanPauseAndContinue = false;
			this.AutoLog = false;

			svcName = p_svcName;
			xmlconfig = p_xmlconfig;
			thread = null;
		}

		private static void Log (string msg)
		{
			File.AppendAllText(exePath + ".txt", msg + "\n");
		}

		private static void RunCommand (string exe, string args)
		{
			System.Diagnostics.Process process = new System.Diagnostics.Process();
			System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
			startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
			startInfo.FileName = exe;
			startInfo.Arguments = args;
			process.StartInfo = startInfo;
			process.Start();
			process.WaitForExit();
		}

		public static void Main (string[] args)
		{
			exePath = Assembly.GetExecutingAssembly().Location;

			if (!System.Environment.UserInteractive)
            {
				if (args.Length < 2)
					return;

				ServiceBase.Run(new Servx(args[0], HexEncoding.FromHexString(args[1])));
                return;
            }

			if (args.Length < 2)
			{
				Console.WriteLine(
					"Use:\n"+
					"    servx install <svcname> <xmlconfig>\n"+
					"    servx uninstall <svcname>\n"+
					"    servx config <svcname> <xmlconfig>\n"
				);

				return;
			}

			if (args[0] == "install")
			{
				if (args.Length < 3)
				{
					Console.WriteLine("Error: Missing <xmlconfig> in install action.");
					return;
				}

				Environment.SetEnvironmentVariable("SERVX_ARG1", args[1]);

				try
				{
					ManagedInstallerClass.InstallHelper(new string[] { "/ServiceName="+args[1], exePath });

					RunCommand("SC.EXE", "config "+args[1]+" binPath=\""+exePath+" "+args[1]+" "+HexEncoding.ToHexString(args[2])+"\"");
				}
				catch (Exception e) {
					Console.WriteLine("Error: Unable to install service:\n" + e.Message);
				}
			}
			else if (args[0] == "config")
			{
				if (args.Length < 3)
				{
					Console.WriteLine("Error: Missing <xmlconfig> in config action.");
					return;
				}

				try
				{
					RunCommand("SC.EXE", "config "+args[1]+" binPath=\""+exePath+" "+args[1]+" "+HexEncoding.ToHexString(args[2])+"\"");
					Console.WriteLine("Service " + args[1] + " configured successfully.");
				}
				catch (Exception e) {
					Console.WriteLine("Error: Unable to configure service:\n" + e.Message);
				}
			}
			else if (args[0] == "uninstall")
			{
				Environment.SetEnvironmentVariable("SERVX_ARG1", args[1]);

				try {
					ManagedInstallerClass.InstallHelper(new string[] { "/ServiceName="+args[1], "/u", exePath });
				} catch (Exception e) {
					Console.WriteLine("Error: Unable to uninstall service:\n" + e.Message);
				}
			}
			else
				Console.WriteLine("Error: Unknown option '" + args[0] + "'");
		}

		protected override void OnStart(string[] args)
		{
			Log("Received START (" + svcName + ")");

			if (thread != null) return;

			thread = new Thread(MainThread);

			finalize = false;
			thread.Start();
		}

		protected override void OnStop()
		{
			Log("Received STOP (" + svcName + ")");

			if (thread == null) return;

			finalize = true;
			thread.Join();

			thread = null;
		}

		public static void MainThread()
		{
			Log("Started thread for service: " + svcName);

			try
			{
				string exeFolder = new FileInfo(exePath).DirectoryName;

				System.Diagnostics.Process process = new System.Diagnostics.Process();
				System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
	
				startInfo.FileName = exeFolder + "\\helicon.exe";
	
				if (xmlconfig.StartsWith("./") || xmlconfig.StartsWith(".\\"))
					xmlconfig = exeFolder + "\\" + xmlconfig.Substring(2);
	
				startInfo.UseShellExecute = false;
				startInfo.RedirectStandardError = true;
				startInfo.RedirectStandardInput = true;
				startInfo.RedirectStandardOutput = true;
				startInfo.CreateNoWindow = true;
				startInfo.ErrorDialog = false;
				startInfo.WindowStyle = ProcessWindowStyle.Hidden;

				startInfo.Arguments = xmlconfig;
				startInfo.WorkingDirectory = exeFolder;

				process.StartInfo = startInfo;

				while (!finalize)
				{
					process.Start();
					process.WaitForExit();
					System.Threading.Thread.Sleep(100);
				}
			}
			catch (Exception e)
			{
				Log("Error (" + svcName + "): " + e.Message);
			}

			Log("Finished thread of service: " + svcName);
		}
	}

	[RunInstaller(true)]
	public class ServxServiceInstaller : Installer
	{
		public ServxServiceInstaller()
		{
		    ServiceProcessInstaller processInstaller = new ServiceProcessInstaller();
		    ServiceInstaller serviceInstaller = new ServiceInstaller();

		    processInstaller.Account = ServiceAccount.LocalSystem;
		    serviceInstaller.StartType = ServiceStartMode.Manual;
		    serviceInstaller.ServiceName = Environment.GetEnvironmentVariable("SERVX_ARG1");

			if (EventLog.SourceExists("SERVX"))
				EventLog.DeleteEventSource("SERVX");

			EventLogInstaller log = new EventLogInstaller();
			log.Source = "SERVX";
			log.Log = "Application";

		    Installers.Add(serviceInstaller);
		    Installers.Add(processInstaller);
		    Installers.Add(log);
		}
	}
}