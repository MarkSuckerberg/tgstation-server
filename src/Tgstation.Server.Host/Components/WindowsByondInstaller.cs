﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Host.IO;

namespace Tgstation.Server.Host.Components
{
	/// <summary>
	/// <see cref="IByondInstaller"/> for windows systems
	/// </summary>
	sealed class WindowsByondInstaller : IByondInstaller
	{
		/// <summary>
		/// The URL format string for getting BYOND windows version {0}.{1} zipfile
		/// </summary>
		const string ByondRevisionsURL = "https://secure.byond.com/download/build/{0}/{0}.{1}_byond.zip";
		/// <summary>
		/// BYOND's DreamDaemon config file in the cfg modification directory
		/// </summary>
		const string ByondDDConfig = "byond/config/daemon.txt";
		/// <summary>
		/// Setting to add to <see cref="ByondDDConfig"/> to suppress an invisible user prompt for running a trusted mode .dmb
		/// </summary>
		const string ByondNoPromptTrustedMode = "trusted-check 0";
		/// <summary>
		/// The directory that contains the BYOND directx redistributable
		/// </summary>
		const string ByondDXDir = "byond/directx";

		/// <inheritdoc />
		public string DreamDaemonName => "dreamdaemon.exe";

		/// <inheritdoc />
		public string DreamMakerName => "dm.exe";

		/// <summary>
		/// The <see cref="IIOManager"/> for the <see cref="WindowsByondInstaller"/>
		/// </summary>
		readonly IIOManager ioManager;

		/// <summary>
		/// If DirectX was installed
		/// </summary>
		bool installedDirectX;

		/// <summary>
		/// Construct a <see cref="WindowsByondInstaller"/>
		/// </summary>
		/// <param name="ioManager">The value of <see cref="ioManager"/></param>
		public WindowsByondInstaller(IIOManager ioManager)
		{
			this.ioManager = ioManager ?? throw new ArgumentNullException(nameof(ioManager));

			installedDirectX = false;
		}

		/// <inheritdoc />
		public Task CleanCache(CancellationToken cancellationToken) => ioManager.DeleteDirectory(ioManager.ConcatPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "byond/cache"), cancellationToken);

		/// <inheritdoc />
		public Task<byte[]> DownloadVersion(Version version, CancellationToken cancellationToken)
		{
			var url = String.Format(CultureInfo.InvariantCulture, ByondRevisionsURL, version.Major, version.Major);

			return ioManager.DownloadFile(new Uri(url), cancellationToken);
		}

		/// <inheritdoc />
		public async Task InstallByond(string path, Version version, CancellationToken cancellationToken)
		{
			var setNoPromptTrustedModeTask = ioManager.WriteAllBytes(ByondDDConfig, Encoding.UTF8.GetBytes(ByondNoPromptTrustedMode), cancellationToken);

			//after this version lummox made DD depend of directx lol
			if (version.Major >= 512 && version.Minor >= 1427 && Monitor.TryEnter(this))
				try
				{
					if (!installedDirectX)
						//always install it, it's pretty fast and will do better redundancy checking than us
						using (var p = new Process())
						{
							p.StartInfo.Arguments = "/silent";
							var rbdx = ioManager.ConcatPath(path, ByondDXDir);
							p.StartInfo.FileName = rbdx + "/DXSETUP.exe";
							p.StartInfo.UseShellExecute = false;
							p.StartInfo.WorkingDirectory = rbdx;
							p.EnableRaisingEvents = true;
							var tcs = new TaskCompletionSource<object>();
							p.Exited += (a, b) => tcs.SetResult(null);
							try
							{
								p.Start();
								using (cancellationToken.Register(() =>
								{
									p.Kill();
									tcs.SetCanceled();
								}))
									await tcs.Task.ConfigureAwait(false);
							}
							finally
							{
								try
								{
									p.Kill();
									p.WaitForExit();
								}
								catch (InvalidOperationException) { }
							}

							if (p.ExitCode != 0)
								throw new Exception("Failed to install included DirectX! Exit code: " + p.ExitCode);
							installedDirectX = true;
						}
				}
				finally
				{
					Monitor.Exit(this);
				}

			await setNoPromptTrustedModeTask.ConfigureAwait(false);
		}
	}
}
