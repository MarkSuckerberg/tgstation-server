﻿using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Tgstation.Server.Api.Models.Internal;
using Tgstation.Server.Host.Components.Chat;
using Tgstation.Server.Host.Components.Deployment;
using Tgstation.Server.Host.Components.Deployment.Remote;
using Tgstation.Server.Host.Components.Events;
using Tgstation.Server.Host.Components.Session;
using Tgstation.Server.Host.Core;
using Tgstation.Server.Host.IO;
using Tgstation.Server.Host.Jobs;

namespace Tgstation.Server.Host.Components.Watchdog
{
	/// <summary>
	/// A <see cref="IWatchdog"/> that, instead of killing servers for updates, uses the wonders of symlinks to swap out changes without killing DreamDaemon.
	/// </summary>
	class WindowsWatchdog : BasicWatchdog
	{
		/// <summary>
		/// The <see cref="SwappableDmbProvider"/> for <see cref="WatchdogBase.LastLaunchParameters"/>.
		/// </summary>
		protected SwappableDmbProvider? ActiveSwappable { get; private set; }

		/// <summary>
		/// The <see cref="IIOManager"/> for the <see cref="WindowsWatchdog"/> pointing to the Game directory.
		/// </summary>
		protected IIOManager GameIOManager { get; }

		/// <summary>
		/// The <see cref="ISymlinkFactory"/> for the <see cref="WindowsWatchdog"/>.
		/// </summary>
		readonly ISymlinkFactory symlinkFactory;

		/// <summary>
		/// The active <see cref="SwappableDmbProvider"/> for <see cref="WatchdogBase.ActiveLaunchParameters"/>.
		/// </summary>
		SwappableDmbProvider? pendingSwappable;

		/// <summary>
		/// The <see cref="IDmbProvider"/> the <see cref="WindowsWatchdog"/> was started with.
		/// </summary>
		IDmbProvider? startupDmbProvider;

		/// <summary>
		/// Initializes a new instance of the <see cref="WindowsWatchdog"/> class.
		/// </summary>
		/// <param name="chat">The <see cref="IChatManager"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="sessionControllerFactory">The <see cref="ISessionControllerFactory"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="dmbFactory">The <see cref="IDmbFactory"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="sessionPersistor">The <see cref="ISessionPersistor"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="jobManager">The <see cref="IJobManager"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="serverControl">The <see cref="IServerControl"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="asyncDelayer">The <see cref="IAsyncDelayer"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="diagnosticsIOManager">The <see cref="IIOManager"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="eventConsumer">The <see cref="IEventConsumer"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="remoteDeploymentManagerFactory">The <see cref="IRemoteDeploymentManagerFactory"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="gameIOManager">The value of <see cref="GameIOManager"/>.</param>
		/// <param name="symlinkFactory">The value of <see cref="symlinkFactory"/>.</param>
		/// <param name="logger">The <see cref="ILogger"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="initialSettings">The <see cref="DreamDaemonSettings"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="instance">The <see cref="Api.Models.Instance"/> for the <see cref="WatchdogBase"/>.</param>
		/// <param name="autoStart">The autostart value for the <see cref="WatchdogBase"/>.</param>
		public WindowsWatchdog(
			IChatManager chat,
			ISessionControllerFactory sessionControllerFactory,
			IDmbFactory dmbFactory,
			ISessionPersistor sessionPersistor,
			IJobManager jobManager,
			IServerControl serverControl,
			IAsyncDelayer asyncDelayer,
			IIOManager diagnosticsIOManager,
			IEventConsumer eventConsumer,
			IRemoteDeploymentManagerFactory remoteDeploymentManagerFactory,
			IIOManager gameIOManager,
			ISymlinkFactory symlinkFactory,
			ILogger<WindowsWatchdog> logger,
			Models.DreamDaemonSettings initialSettings,
			Models.Instance instance,
			bool autoStart)
			: base(
				chat,
				sessionControllerFactory,
				dmbFactory,
				sessionPersistor,
				jobManager,
				serverControl,
				asyncDelayer,
				diagnosticsIOManager,
				eventConsumer,
				remoteDeploymentManagerFactory,
				logger,
				initialSettings,
				instance,
				autoStart)
		{
			try
			{
				GameIOManager = gameIOManager ?? throw new ArgumentNullException(nameof(gameIOManager));
				this.symlinkFactory = symlinkFactory ?? throw new ArgumentNullException(nameof(symlinkFactory));
			}
			catch
			{
				// Async dispose is for if we have controllers running, not the case here
				DisposeAsync().AsTask().GetAwaiter().GetResult();
				throw;
			}
		}

		/// <inheritdoc />
		protected override async Task DisposeAndNullControllersImpl()
		{
			await base.DisposeAndNullControllersImpl().ConfigureAwait(false);

			// If we reach this point, we can guarantee PrepServerForLaunch will be called before starting again.
			ActiveSwappable = null;
			pendingSwappable?.Dispose();
			pendingSwappable = null;

			startupDmbProvider?.Dispose();
			startupDmbProvider = null;
		}

		/// <inheritdoc />
		protected override async Task<MonitorAction> HandleNormalReboot(CancellationToken cancellationToken)
		{
			if (pendingSwappable != null)
			{
				var updateTask = BeforeApplyDmb(pendingSwappable.CompileJob, cancellationToken);
				Logger.LogTrace("Replacing activeSwappable with pendingSwappable...");
				Server?.ReplaceDmbProvider(pendingSwappable);
				ActiveSwappable = pendingSwappable;
				pendingSwappable = null;

				await updateTask.ConfigureAwait(false);
			}
			else
				Logger.LogTrace("Nothing to do as pendingSwappable is null.");

			return MonitorAction.Continue;
		}

		/// <inheritdoc />
		protected override async Task HandleNewDmbAvailable(CancellationToken cancellationToken)
		{
			IDmbProvider compileJobProvider = DmbFactory.LockNextDmb(1);
			bool canSeamlesslySwap = true;

			var activeCompileJob = ActiveCompileJob;
			if (activeCompileJob == null)
				throw new InvalidOperationException("ActiveCompileJob is null!");

			if (compileJobProvider.CompileJob.ByondVersion != activeCompileJob.ByondVersion)
			{
				// have to do a graceful restart
				Logger.LogDebug(
					"Not swapping to new compile job {compileJobId} as it uses a different BYOND version ({compileJobByondVer}) than what is currently active {activeByondVer}. Queueing graceful restart instead...",
					compileJobProvider.CompileJob.Id,
					compileJobProvider.CompileJob.ByondVersion,
					activeCompileJob.ByondVersion);
				canSeamlesslySwap = false;
			}

			if (compileJobProvider.CompileJob.DmeName != activeCompileJob.DmeName)
			{
				Logger.LogDebug(
					"Not swapping to new compile job {compileJobId} as it uses a different .dmb name ({oldDmeName}) than what is currently active {newDmeName}. Queueing graceful restart instead...",
					compileJobProvider.CompileJob.Id,
					compileJobProvider.CompileJob.DmeName,
					activeCompileJob.DmeName);
				canSeamlesslySwap = false;
			}

			if (!canSeamlesslySwap)
			{
				compileJobProvider.Dispose();
				await base.HandleNewDmbAvailable(cancellationToken).ConfigureAwait(false);
				return;
			}

			SwappableDmbProvider? windowsProvider = null;
			bool suspended = false;
			try
			{
				windowsProvider = new SwappableDmbProvider(compileJobProvider, GameIOManager, symlinkFactory);

				Logger.LogDebug("Swapping to compile job {compileJobId}...", windowsProvider.CompileJob.Id);
				try
				{
					var controller = Server;
					if (controller != null)
					{
						controller.Suspend();
						suspended = true;
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex, "Exception while suspending server!");
				}

				await windowsProvider.MakeActive(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Exception while swapping");
				IDmbProvider providerToDispose = windowsProvider ?? compileJobProvider;
				providerToDispose.Dispose();
				throw;
			}

			// Let this throw hard if it fails
			if (suspended)
				Server?.Unsuspend();

			pendingSwappable?.Dispose();
			pendingSwappable = windowsProvider;
		}

		/// <inheritdoc />
		protected sealed override async Task<IDmbProvider> PrepServerForLaunch(IDmbProvider dmbToUse, CancellationToken cancellationToken)
		{
			if (ActiveSwappable != null)
				throw new InvalidOperationException("Expected activeSwappable to be null!");
			if (startupDmbProvider != null)
				throw new InvalidOperationException("Expected startupDmbProvider to be null!");

			Logger.LogTrace("Prep for server launch. pendingSwappable is {maybeNot}available", pendingSwappable == null ? "not " : String.Empty);

			// Add another lock to the startup DMB because it'll be used throughout the lifetime of the watchdog
			startupDmbProvider = await DmbFactory.FromCompileJob(dmbToUse.CompileJob, cancellationToken).ConfigureAwait(false);

			var activeSwappable = pendingSwappable ?? new SwappableDmbProvider(dmbToUse, GameIOManager, symlinkFactory);
			ActiveSwappable = activeSwappable;
			pendingSwappable = null;

			try
			{
				await InitialLink(activeSwappable, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// We won't worry about disposing activeSwappable here as we can't dispose dmbToUse here.
				Logger.LogTrace(ex, "Initial link error, nulling ActiveSwappable");
				ActiveSwappable = null;
				throw;
			}

			return ActiveSwappable;
		}

		/// <summary>
		/// Create the initial link to the live game directory using <see cref="ActiveSwappable"/>.
		/// </summary>
		/// <param name="activeSwappable">The current value of <see cref="ActiveSwappable"/>.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation.</param>
		/// <returns>A <see cref="Task"/> representing the running operation.</returns>
		protected virtual Task InitialLink(SwappableDmbProvider activeSwappable, CancellationToken cancellationToken)
		{
			Logger.LogTrace("Symlinking compile job...");
			return activeSwappable.MakeActive(cancellationToken);
		}
	}
}
