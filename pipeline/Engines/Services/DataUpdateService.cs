/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using FiftyOne.Common.Wrappers.IO;
using FiftyOne.Pipeline.Core.Services;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Exceptions;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if !NET8_0_OR_GREATER
using ICSharpCode.SharpZipLib.GZip;
#endif

namespace FiftyOne.Pipeline.Engines.Services
{
    /// <summary>
    /// Service that manages updates to data files that are used by
    /// <see cref="IAspectEngine"/> instances.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/data-updates.md">Specification</see>
    /// </summary>
    public class DataUpdateService : IDataUpdateService
	{
		#region Fields
		private ILogger<DataUpdateService> _logger;

		/// <summary>
		/// The HttpClient to use when checking for updates.
		/// </summary>
		private HttpClient _httpClient;

        // System wrappers
        private IFileSystem _fileSystem;

        // Random number generator, which is not safe to use from several
        // timer threads at once, so it is only used under its own lock.
        private readonly Random _rnd = new Random();
        private readonly object _rndLock = new object();

        /// <summary>
        /// The write time .NET gives a file that is not there.
        /// </summary>
        private static readonly DateTime MissingFileWriteTime =
            DateTime.FromFileTimeUtc(0);

		// All registered configurations
		private List<AspectEngineDataFile> _configurations;

		// The factory function used to create Timer objects.
		private Func<TimerCallback, object, TimeSpan, Timer> _timerFactory;

		/// <summary>
		/// The event handler fired when a call to CheckForUpdate is completed.
		/// </summary>
		public event EventHandler<DataUpdateCompleteArgs> CheckForUpdateComplete;
		/// <summary>
		/// The event handler fired when a call to CheckForUpdate is started.
		/// </summary>
		public event EventHandler<DataUpdateEventArgs> CheckForUpdateStarted;
		#endregion

		/// <summary>
		/// Constructor for a service that makes its own
		/// <see cref="HttpClient"/>, which does not follow redirects.
		/// </summary>
		/// <param name="logger">
		/// The logger to use
		/// </param>
		public DataUpdateService(
			ILogger<DataUpdateService> logger) :
			this(logger, CreateHttpClient(), null, null)
		{ }

		/// <summary>
		/// Make the <see cref="HttpClient"/> the service uses when it is
		/// given none. It does not follow redirects, so that the answer
		/// which lists the products the license keys hold reaches the
		/// service. That answer has a Location header, which a client
		/// that follows redirects requests in place of the list.
		/// </summary>
		/// <returns>
		/// A new <see cref="HttpClient"/>.
		/// </returns>
		internal static HttpClient CreateHttpClient()
		{
#pragma warning disable CA2000 // Dispose objects before losing scope
			// The client owns its handler, and the caller owns the client.
			return new HttpClient(new HttpClientHandler()
			{
				AllowAutoRedirect = false,
			});
#pragma warning restore CA2000 // Dispose objects before losing scope
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="logger">
		/// The logger to use
		/// </param>
		/// <param name="httpClient">
		/// The <see cref="HttpClient"/> to use when requesting an update
		/// from a URL.
		/// Note that only one HttpClient instance should be used throughout
		/// the application, as described in the documentation:
		/// https://msdn.microsoft.com/library/system.net.http.httpclient(v=vs.110).aspx
		/// The client must not follow redirects (its handler has
		/// AllowAutoRedirect set to false) when the license keys hold
		/// more than one product. Otherwise the client requests the
		/// address in the Location header of the answer that lists the
		/// products, and the service never sees the list.
		/// </param>
		public DataUpdateService(
			ILogger<DataUpdateService> logger,
			HttpClient httpClient) : this(logger, httpClient, null, null)
		{ }

        /// <summary>
        /// Internal constructor. Should only be called directly 
        /// by unit tests 
        /// </summary>
        /// <param name="logger">
        /// The logger to use
        /// </param>
        /// <param name="httpClient">
        /// The <see cref="HttpClient"/> to use when requesting an update
        /// from a URL.
        /// Note that only one HttpClient instance should be used throughout 
        /// the application, as described in the documentation: 
        /// https://msdn.microsoft.com/library/system.net.http.httpclient(v=vs.110).aspx
        /// </param>
        /// <param name="fileSystemWrapper">
        /// A wrapper for file system.
        /// In normal operation this will usually be a <see cref="RealFileSystem"/>
        /// instance. If null is passed, a new <see cref="RealFileSystem"/>
        /// instance is created.
        /// </param>
        /// <param name="timerFactory">
        /// A factory method for creating <see cref="Timer"/> instances.
        /// Parameters are: callback method, state, time until callback 
        /// triggered.
        /// </param>
        internal DataUpdateService(
			ILogger<DataUpdateService> logger,
			HttpClient httpClient,
			IFileSystem fileSystemWrapper,
			Func<TimerCallback, object, TimeSpan, Timer> timerFactory)
		{
			_logger = logger;
			_httpClient = httpClient;
			_configurations = new List<AspectEngineDataFile>();

			if(fileSystemWrapper == null)
			{
                _fileSystem = new RealFileSystem();
			}
			else
			{
                _fileSystem = fileSystemWrapper;
			}
			if (timerFactory == null)
			{
				_timerFactory = TimerFactory;
			}
			else
			{
				_timerFactory = timerFactory;
			}
		}

		#region Public methods

		/// <summary>
		/// Enables logging debug information (intermediate states and results).
		/// </summary>
		public bool DebugLoggingEnabled { get; set; }

        /// <summary>
        /// Called when <see cref="CheckForUpdate(object)"/> starts
        /// </summary>
        public event Action OnTimeredCheckForUpdateEntered;

        /// <summary>
        /// Called when <see cref="CheckForUpdate(object)"/> will exit
        /// </summary>
        public event Action OnTimeredCheckForUpdateWillExit;

        /// <summary>
        /// Called when <see cref="DataFileUpdated(object, FileSystemEventArgs)"/> starts
        /// </summary>
        public event Action OnDataFileUpdatedEntered;

        /// <summary>
        /// Called when <see cref="DataFileUpdated(object, FileSystemEventArgs)"/> will exit
        /// </summary>
        public event Action OnDataFileUpdatedWillExit;

		/// <summary>
		/// Register an data file for automatic updates.
		/// </summary>
		/// <param name="dataFile">
		/// The details of the data file to register.
		/// </param>
		public void RegisterDataFile(AspectEngineDataFile dataFile)
		{
			if (dataFile == null)
			{
				throw new ArgumentNullException(nameof(dataFile));
			}

			// A disposed data file is not registered again, so a check that
			// was running when the file was disposed does not bring it back.
			// A file disposed while this runs is caught by the checks before
			// the check on start-up and under its timer lock and the list's
			// lock below, though a check on start-up that has already begun
			// still runs.
			if (dataFile.TimerClosed)
			{
				LogDebugMessage(
					() => "dataFile is disposed so is not registered.",
					dataFile);
				return;
			}

			bool alreadyRegistered = dataFile.IsRegistered;
			bool setTimer = true;
			dataFile.SetDataUpdateService(this);
            LogInfoMessage(Messages.MessageAutoUpdateRegisterDataFile, dataFile);

            if (dataFile != null)
			{
				LogDebugMessage(() => "dataFile is not null.", dataFile);

				// If the data file is configured to refresh the data
				// file on startup then download an update immediately.
				// We also want to do this synchronously so that execution
				// will block until the engine is ready.
				if (dataFile.Configuration.UpdateOnStartup &&
					alreadyRegistered == false &&
					dataFile.TimerClosed == false)
                {
                    LogInfoMessage(Messages.MessageAutoUpdateOnStartup, dataFile);
                    var result = CheckForUpdate(dataFile, true);
					LogDebugMessage(() => $"{nameof(CheckForUpdate)} resulted in {result}", dataFile);
					if (result == AutoUpdateStatus.AUTO_UPDATE_SUCCESS)
					{
						// If the update was successful then the timer 
						// and watcher will already have been set up if 
						// needed.
						setTimer = false;
					}
					else if (result != AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED)
					{
						var msg = string.Format(CultureInfo.InvariantCulture,
							Messages.ExceptionAutoUpdateOnStartupFailed,
							dataFile.Identifier,
							Enum.GetName(typeof(AutoUpdateStatus), result));
						throw new DataUpdateException(msg, result);
					}
				}
				if(setTimer)
				{
					LogDebugMessage(() => $"{nameof(setTimer)} is {setTimer}", dataFile);

					// Only create an automatic update timer if auto updates are 
					// enabled for this engine and there is not already an associated 
					// timer. The timer lock is held from that check until the timer
					// is stored, because the timer can fire as soon as it is
					// created and its check must find it.
					if (dataFile.AutomaticUpdatesEnabled)
					{
						lock (dataFile.TimerLock)
						{
							// The file can be disposed after the check above.
							if (dataFile.Timer == null &&
								dataFile.TimerClosed == false)
							{
								LogInfoMessage(
									Messages.MessageAutoUpdateCreateTimer, dataFile);

								TimeSpan timeToUpdate = GetInterval(
									dataFile.Configuration);
								if (dataFile.UpdateAvailableTime > DateTime.UtcNow)
								{
									timeToUpdate = dataFile.UpdateAvailableTime
										.Subtract(DateTime.UtcNow);
									timeToUpdate = ApplyIntervalRandomisation(
										timeToUpdate, dataFile.Configuration);
								}
								// Create a timer that will go off when the engine
								// expects updated data to be available.
								var timer = _timerFactory(
									CheckForUpdate,
									dataFile,
									timeToUpdate);
								dataFile.Timer = timer;
							}
						}
					}

					// If file system watcher is enabled then set it up. The
					// watcher runs on its own setting, whatever automatic
					// updates are set to and with or without a licence key,
					// because watching a file downloads nothing.
					if (dataFile.Configuration.FileSystemWatcherEnabled &&
						dataFile.FileWatcher == null &&
						string.IsNullOrEmpty(dataFile.DataFilePath) == false)
                    {
						// Under the timer lock, as Dispose marks the file closed
						// under it before it disposes the watcher.
						lock (dataFile.TimerLock)
						{
							if (dataFile.TimerClosed == false)
							{
								LogInfoMessage(Messages.MessageAutoUpdateCreateWatcher, dataFile);

								FileSystemWatcher watcher = new FileSystemWatcher(
									Path.GetDirectoryName(dataFile.DataFilePath),
									Path.GetFileName(dataFile.DataFilePath));

								//on macos NotifyFilters.FileName filter and Created subscription are needed
								//because it seems to recreate the file when copying over
								watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
								watcher.Changed += DataFileUpdated;
								watcher.Created += DataFileUpdated;
								watcher.EnableRaisingEvents = true;
								dataFile.FileWatcher = watcher;
							}
						}
					}

					lock (_configurations)
					{
						// Add the configuration to the list of configurations,
						// unless the file was disposed while it was registered.
						// Dispose marks it closed before it takes this lock to
						// remove it, so this check and that removal cannot both
						// miss it.
						if (_configurations.Contains(dataFile) == false &&
							dataFile.TimerClosed == false)
						{
							_configurations.Add(dataFile);
						}
					}
				}
			}
		}

		/// <summary>
		/// Unregister a data file.
		/// </summary>
		/// <param name="dataFile">
		/// The data file to unregister
		/// </param>
		public void UnRegisterDataFile(AspectEngineDataFile dataFile)
		{
			lock (_configurations)
			{
				var configs = _configurations.Where(c => c == dataFile).ToList();
				foreach (var config in configs)
				{
					_configurations.Remove(config);
				}
			}
			// Stop the timer so that the file is not checked again.
			if (dataFile != null)
			{
				ClearTimer(dataFile);
			}
		}

        /// <summary>
        /// Check if there are updates for the specified engine.
        /// </summary>
        /// <param name="engine">
        /// The engine to check for
        /// </param>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file to check for.
        /// If the engine has only one data file then this parameter is ignored.
        /// </param>
        /// <returns>
        /// The result of the update check. For a data file whose engine has
        /// been built, a dispose before any try at refreshing the engine
        /// gives not needed, and the engine is left alone. Nothing is
        /// downloaded if the file was disposed before the call. A download
        /// under way when it was disposed is not copied over the data file,
        /// though a dispose after the download has been checked lets the copy
        /// write it. A dispose during a try waits for that try to end, and
        /// the check gives success if it worked. If that try failed, or the
        /// dispose came after a failed try, the tries end and the failure is
        /// thrown as a refresh failure. A dispose after the engine is
        /// refreshed leaves the success. Any failure before the refresh is
        /// thrown whether or not the file was disposed.
        /// </returns>
        /// <exception cref="DataUpdateException">
        /// Thrown if some problem occurs during the update process.
        /// </exception>
		/// <exception cref="ArgumentNullException">
		/// Thrown if the parameter is null
		/// </exception>
		/// <remarks>
		/// A refresh of the engine waits for one already in progress, which
		/// includes the file watcher's wait for a changed data file to be
		/// readable.
		/// </remarks>
		public AutoUpdateStatus CheckForUpdate(
			IOnPremiseAspectEngine engine, 
			string dataFileIdentifier = null)
		{
			if (engine == null)
			{
				throw new ArgumentNullException(nameof(engine));
			}

			AutoUpdateStatus result;
			var dataFile = engine.GetDataFileMetaData(dataFileIdentifier);
			if(dataFile != null)
			{ 
				result = CheckForUpdate(dataFile, true);
			}
			else
			{
				result = AutoUpdateStatus.AUTO_UPDATE_NO_CONFIGURATION;
			}
			return result;
		}

		/// <summary>
		/// Update the specified data file from a <see cref="MemoryStream"/>
		/// </summary>
		/// <param name="dataFile">
		/// The data file to update.
		/// </param>
		/// <param name="data">
		/// The data to update with.
		/// </param>
		/// <returns>
		/// Success. Any failure is thrown.
		/// </returns>
		/// <exception cref="DataUpdateException">
		/// Thrown if some problem occurs during the update process.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		/// Thrown if arguments are null.
		/// </exception>
		/// <exception cref="ObjectDisposedException">
		/// Thrown if the data file has been disposed. The engine is not
		/// refreshed, and the data file is not written unless it was
		/// disposed during this call.
		/// </exception>
		/// <remarks>
		/// The refresh waits for one already in progress, which includes the
		/// file watcher's wait for a changed data file to be readable.
		/// </remarks>
		public AutoUpdateStatus UpdateFromMemory(AspectEngineDataFile dataFile,
            MemoryStream data)
		{
			if (dataFile == null)
			{
				throw new ArgumentNullException(nameof(dataFile));
			}
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}
			if (dataFile.TimerClosed)
			{
				throw DataFileDisposed(dataFile);
			}

			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
            if (string.IsNullOrEmpty(dataFile.DataFilePath) == false)
            {
                // The engine has an associated data file so update it first.
                try
                {
                    _fileSystem.File.WriteAllBytes(dataFile.DataFilePath, data.GetBuffer());
				}
#pragma warning disable CA1031 // Do not catch general exception types
				// Catch any exceptions as this is a non-critical 
				// operation and we don't want it affecting anything else.
				catch (Exception ex)
				{
                    _logger.LogWarning($"An error occurred when writing to " +
                        $"'{dataFile.DataFilePath}'. The engine will be updated " +
                        $"to use the new data but the file on disk will still " +
                        $"contain old data.", ex);
				}
#pragma warning restore CA1031 // Do not catch general exception types
			}

			if (dataFile.Engine != null)
            {
                try
                {
                    // Refresh the engine using the new data.
                    result = RefreshEngine(
                        dataFile,
                        () => dataFile.Engine.RefreshData(
                            dataFile.Identifier, data));
                }
                catch (Exception ex)
                {
                    result = AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED;
                    throw new DataUpdateException($"An error occurred when applying a " +
                        $"data update to engine '{dataFile.Engine.GetType().Name}'.", ex, result);
                }
                // Disposed during this call, so the engine was left alone.
                if (result != AutoUpdateStatus.AUTO_UPDATE_SUCCESS)
                {
                    throw DataFileDisposed(dataFile);
                }
            }
            else
            {
                result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
            }

            return result;
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Default method used to create Timer instances when a timer factory
        /// is not provided in the constructor.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <param name="dueTime"></param>
        /// <returns></returns>
        private Timer TimerFactory(TimerCallback callback, object state, TimeSpan dueTime)
		{
			return new Timer(callback, state, dueTime, TimeSpan.FromMilliseconds(-1));
		}

		/// <summary>
		/// Called when the 'CheckForUpdate' method is finished.
		/// </summary>
		/// <param name="args"></param>
		private void OnUpdateComplete(DataUpdateCompleteArgs args)
		{
			CheckForUpdateComplete?.Invoke(this, args);
		}

		/// <summary>
		/// Called when the 'CheckForUpdate' method is started.
		/// </summary>
		/// <param name="args"></param>
		private void OnUpdateStarted(DataUpdateEventArgs args)
		{
			CheckForUpdateStarted?.Invoke(this, args);
		}

		/// <summary>
		/// Event handler that is called when the data file is updated.
		/// </summary>
		/// <remarks>
		/// The <see cref="FileSystemWatcher"/> will raise multiple events 
		/// in many cases, for example, if a file is copied over an existing 
		/// file then 3 'changed' events will be raised.
		/// This handler deals with the extra events by using synchronisation 
		/// with a double-check lock to ensure that the update will only be 
		/// done once.
		/// The watcher raises this event on a thread it owns. An exception
		/// that escaped would end the process on Windows and macOS, and stop
		/// the watcher on Linux, so every exception is logged here instead.
		/// </remarks>
		/// <param name="sender">
		/// The <see cref="FileSystemWatcher"/> sender of the event.
		/// </param>
		/// <param name="e">
		/// The event arguments.
		/// </param>
		private void DataFileUpdated(object sender, FileSystemEventArgs e)
		{
			try
            {
                OnDataFileUpdatedEntered?.Invoke();
            }
            catch
            {
                // nop -- ignore all errors
            }

			AspectEngineDataFile dataFile = null;
			try
			{
				// Get the associated update configuration
				try
				{
					dataFile = _configurations
						.Where(c => c.FileWatcher != null &&
							c.FileWatcher == sender)
						.SingleOrDefault();
				}
				catch (InvalidOperationException)
				{
					dataFile = null;
				}

				DataFileUpdatedInternal(sender, e, dataFile);
				// A write that lands while the event is handled can raise no
				// event of its own, as the watcher on macOS merges changes to
				// one file it has not yet delivered. So the write time is
				// read again, and a later write is handled here.
				while (WrittenSinceLastUpdate(dataFile, e.FullPath))
				{
					DataFileUpdatedInternal(sender, e, dataFile);
				}
            }
#pragma warning disable CA1031 // Do not catch general exception types
			catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
			{
				LogUpdateException(ex, dataFile);
			}
			finally
			{
                try
                {
                    OnDataFileUpdatedWillExit?.Invoke();
                }
                catch
                {
                    // nop -- ignore all errors
                }
            }
		}

        /// <summary>
        /// Refreshes the engine from the file the watcher reported, then
        /// raises <see cref="CheckForUpdateComplete"/> with the result.
        /// </summary>
        /// <remarks>
        /// A failed update is logged before the event is raised, as on the
        /// timer route. The file's write time is recorded first, so the same
        /// file is not tried again and a file written later is.
        /// </remarks>
        private void DataFileUpdatedInternal(object sender, FileSystemEventArgs e, AspectEngineDataFile dataFile)
        {
            AutoUpdateStatus status = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;

			OnUpdateStarted(new DataUpdateEventArgs()
			{
				DataFile = dataFile
			});

			if (dataFile != null)
			{
				try
				{
					// Get the last modified time of the new data file
					DateTime dataTime = _fileSystem.File.GetLastWriteTimeUtc(e.FullPath);
					// A file that is not there is not newer, so there is
					// nothing to apply.
					if (dataTime <= MissingFileWriteTime)
					{
						status = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
					}
					// Use a lock with a double check on file time to make
					// sure we only run the update once even if multiple
					// events fire for a single file.
					else if (dataFile.LastUpdateFileModifiedTime < dataTime)
					{
						lock (dataFile.UpdateSyncLock)
						{
							if (dataFile.LastUpdateFileModifiedTime < dataTime)
							{
								dataFile.LastUpdateFileModifiedTime = dataTime;

								// Make sure we can actually open the file for reading
								// before notifying the engine, otherwise the copy
								// may still be in progress. Dispose waits for this
								// lock, so stop waiting once the file is closed.
								bool fileLockable = false;
								bool fileGone = false;
								DateTime timeout = DateTime.UtcNow.AddMinutes(10);

								while (fileLockable == false &&
									fileGone == false &&
									dataFile.TimerClosed == false &&
									timeout > DateTime.UtcNow)
								{
									try
									{
										using (_fileSystem.File.OpenRead(e.FullPath)) { }
										fileLockable = true;
									}
									catch (FileNotFoundException)
									{
										// Gone since the event, so there is
										// nothing to wait for.
										fileGone = true;
									}
									catch (DirectoryNotFoundException)
									{
										fileGone = true;
									}
									catch (IOException)
									{
										// Wait for half a second before trying again.
										Task.Delay(500).Wait();
									}
								}

								if (fileGone || dataFile.TimerClosed)
								{
									status = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
								}
								else if (fileLockable)
								{
									// Complete the update
									status = UpdatedDataAvailable(dataFile);
								}
								else
								{
									status = AutoUpdateStatus.AUTO_UPDATE_TIMEOUT;
								}
							}
						}
					}
					else
					{
						status = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
					}
				}
#pragma warning disable CA1031 // Do not catch general exception types
				catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
				{
					status = ex is DataUpdateException dataUpdateException ?
						dataUpdateException.Status :
						AutoUpdateStatus.AUTO_UPDATE_UNKNOWN_ERROR;
					LogUpdateException(ex, dataFile);
				}
			}

			OnUpdateComplete(new DataUpdateCompleteArgs()
			{
				DataFile = dataFile,
				Status = status
			});
		}

        /// <summary>
        /// True when the file was written after the write time the last
        /// update recorded, and the data file is still open.
        /// </summary>
        private bool WrittenSinceLastUpdate(
            AspectEngineDataFile dataFile,
            string path)
        {
            if (dataFile == null || dataFile.TimerClosed)
            {
                return false;
            }
            var written = _fileSystem.File.GetLastWriteTimeUtc(path);
            return written > MissingFileWriteTime &&
                written > dataFile.LastUpdateFileModifiedTime;
        }

		private void DebugDescribeException(Exception x, bool deep = false)
		{
			if (!DebugLoggingEnabled)
			{
				return;
			}
			Func<string>[] reportPoints = {
				() => $"type: {x.GetType().Name}",
                () => $"message: {x.Message}",
                () => $"inner exception: {x.InnerException}",
                () => $"trace: {x.StackTrace}",
                () => $"full: {x}",
            };
			foreach (var p in reportPoints)
			{
				try
				{
					LogDebugMessage(() => $"Exception (deep={deep}) {p()}", null);
				}
				catch (Exception e)
				{
					if (!deep)
					{
						DebugDescribeException(e, true);
					}
				}
			}
		}

		/// <summary>
		/// Logs an exception from an automatic update with the exception
		/// attached, so the log says why the update failed.
		/// </summary>
		/// <remarks>
		/// Runs on the threads of the timer and the file system watcher,
		/// where an exception that escapes ends the process, so a logger
		/// that throws is ignored.
		/// </remarks>
		private void LogUpdateException(
			Exception exception,
			AspectEngineDataFile dataFile)
		{
			try
			{
				if (exception is DataUpdateException)
				{
					_logger.LogError(exception, Messages.ExceptionAutoUpdate);
				}
				else
				{
					_logger.LogError(exception, string.Format(
						CultureInfo.InvariantCulture,
						Messages.MessageAutoUpdateUnhandledError,
						dataFile?.EngineType?.Name ?? "Unknown"));
				}
			}
			catch
			{
				// nop -- a logger that throws must not end the process
			}
		}

		/// <summary>
		/// Private method called by update timers when an update is believed
		/// to be available.
		/// </summary>
		/// <param name="state">
		/// The <see cref="AspectEngineDataFile"/>
		/// </param>
		private void CheckForUpdate(object state)
        {
			try
			{
				OnTimeredCheckForUpdateEntered?.Invoke();
            }
			catch
			{
				// nop --- ignore all errors
			}

			var dataFile = state as AspectEngineDataFile;
			try
			{
				// This method is called from a background thread so
				// we need to make sure any exceptions that occur
				// are handled here.
				var onUpdateCompleteReports = new List<DataUpdateCompleteArgs>();
				try
				{
					if (state == null)
					{
						throw new ArgumentNullException(nameof(state));
					}

					CheckForUpdate(state, false, onUpdateCompleteReports.Add);
				}
				catch (DataUpdateException ex)
				{
					LogDebugMessage(() => $"Exception of type '{ex.GetType().Name}' received into {nameof(DataUpdateException)} clause.", null);
					DebugDescribeException(ex);
					LogUpdateException(ex, dataFile);
				}
#pragma warning disable CA1031 // Do not catch general exception types
				// We want to catch any possible exception here so that
				// the relevant details can be logged.
				catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
				{
					LogDebugMessage(() => $"Exception of type '{ex.GetType().Name}' received into {nameof(Exception)} clause.", null);
					DebugDescribeException(ex);
					LogUpdateException(ex, dataFile);
				}

				LogDebugMessage(() => $"At midpoint of {nameof(CheckForUpdate)} -- will call {nameof(OnUpdateComplete)} with {onUpdateCompleteReports.Count} reports", null);

				try
				{
					foreach (var report in onUpdateCompleteReports)
					{
						OnUpdateComplete(report);
					}
				}
				catch (Exception ex)
				{
					LogUpdateException(ex, dataFile);
				}

				LogDebugMessage(() => $"Exiting timer-bound {nameof(CheckForUpdate)}", null);
			}
#pragma warning disable CA1031 // Do not catch general exception types
			// An exception from the debug logging above, which must not end
			// the process either.
			catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
			{
				LogUpdateException(ex, dataFile);
			}
			finally
			{
				try
				{
					OnTimeredCheckForUpdateWillExit?.Invoke();
				}
				catch
				{
					// nop --- ignore all errors
				}
			}
        }

		/// <summary>
		/// Private method that performs the following actions:
		/// 1. Checks for an update to the data file on disk.
		/// 2. Checks for an update using the update URL.
		/// 3. Refresh engine with new data if available.
		/// 4. Schedule the next update check if needed.
		/// </summary>
		/// <param name="state">
		/// The <see cref="AspectEngineDataFile"/>
		/// </param>
		/// <param name="manualUpdate">
		/// True if this update was requested by the user and or is running 
		/// synchronously from the 'main' thread. 
		/// (For example, when the update on startup option is used)
		/// False if it was triggered by a timer expiring and is therefore
		/// running in a background thread.
		/// </param>
		private AutoUpdateStatus CheckForUpdate(object state, bool manualUpdate)
			=> CheckForUpdate(state, manualUpdate, OnUpdateComplete);
        private AutoUpdateStatus CheckForUpdate(object state, bool manualUpdate, Action<DataUpdateCompleteArgs> onUpdateComplete)
        {
            AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
            AspectEngineDataFile dataFile = state as AspectEngineDataFile;
            LogDebugMessage(() => $"Starting {nameof(CheckForUpdate)} with {nameof(result)} = {result}", dataFile);
            bool newDataAvailable = false;

			OnUpdateStarted(new DataUpdateEventArgs()
			{
				DataFile = dataFile
			});

			try
			{
				// A disposed file's engine is gone, so there is nothing to
				// download or apply.
				if (dataFile != null && dataFile.TimerClosed)
				{
					result = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
				}
				else if (dataFile != null)
				{
					LogInfoMessage(Messages.MessageAutoUpdateCheck, dataFile);

					// Only check the file system if the file system watcher
					// is not enabled and the engine is using a temporary file.
					if (dataFile.Configuration.FileSystemWatcherEnabled == false &&
						string.IsNullOrEmpty(dataFile.DataFilePath) == false &&
						string.IsNullOrEmpty(dataFile.TempDataFilePath) == false)
					{
						LogInfoMessage(Messages.MessageAutoUpdateCheckFileSystem, dataFile);

						// We use last write time as creation time can be 
						// unreliable for this check.
						// For example, if a file exists with created date 
						// yesterday and a new file is manually copied over 
						// the top then the creation date will not change.
						var fileWriteTime = _fileSystem.File.GetLastWriteTimeUtc(
							dataFile.DataFilePath);
						var tempFileWriteTime = _fileSystem.File.GetLastWriteTimeUtc(
							dataFile.TempDataFilePath);

						// If the data file is newer than the temp file currently
						// being used by the engine the we need to tell the engine
						// to refresh itself.
						if (fileWriteTime > tempFileWriteTime)
						{
							newDataAvailable = true;
						}
					}
					LogDebugMessage(() => $"{nameof(newDataAvailable)} is yet {newDataAvailable}", dataFile);

					if (newDataAvailable == false &&
						string.IsNullOrEmpty(dataFile.Configuration.DataUpdateUrl) == false)
					{
						result = CheckForUpdateFromUrl(dataFile);
						LogDebugMessage(() => $"{nameof(CheckForUpdateFromUrl)} resulted in {result}", dataFile);
						newDataAvailable =
							result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS ||
							result == AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
					}
					LogDebugMessage(() => $"{nameof(newDataAvailable)} is {newDataAvailable}", dataFile);

					if (newDataAvailable == false)
					{
						result = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
						LogDebugMessage(() => $"{nameof(result)} is {result}", dataFile);
					}
					else if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
					{
						// Data update was available but engine has not 
						// yet been refreshed.
						result = UpdatedDataAvailable(dataFile);
						LogDebugMessage(() => $"{nameof(UpdatedDataAvailable)} resulted in {result}", dataFile);
					}

					if (result == AutoUpdateStatus.AUTO_UPDATE_SUCCESS)
					{
						// Clear the timer ready for a new one to be created.
						ClearTimer(dataFile);
						// Re-register the engine with the data update service 
						// so it knows when next set of data should be available.
						RegisterDataFile(dataFile);
					}
				}
			}
			// Catch exceptions to make sure that status is set correctly 
			// when firing the UpdateComplete event, then rethrow them
			// so that the caller has visibility of the exception.
			catch (DataUpdateException dex)
			{
				result = dex.Status;
				throw;
			}
			catch (Exception)
			{
				result = AutoUpdateStatus.AUTO_UPDATE_UNKNOWN_ERROR;
				throw;
			}
			finally
			{
				LogDebugMessage(() => $"Entering {nameof(CheckForUpdate)}-finally with {nameof(newDataAvailable)} = {newDataAvailable}, {nameof(result)} = {result}", dataFile);
				if (result != AutoUpdateStatus.AUTO_UPDATE_SUCCESS)
				{
					// No update was applied, because none was available or
					// because applying it failed.
					// If this was a manual call to update then do nothing.
					// If it was triggered by the timer expiring then modify
					// the timer to check again after the configured interval.
					// This will repeat until an update is applied.
					if (manualUpdate == false &&
						dataFile != null)
					{
						lock (dataFile.TimerLock)
						{
							if (dataFile.Timer != null)
							{
								dataFile.Timer.Change(
									GetInterval(dataFile.Configuration),
									TimeSpan.FromMilliseconds(-1));
							}
						}
					}
				}

				onUpdateComplete(new DataUpdateCompleteArgs()
				{
					DataFile = dataFile,
					Status = result
				});
			}

            return result;
        }

		/// <summary>
		/// Download an update for the specified data file from its update URL.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> containing the 
		/// update configuration settings.
		/// </param>
		/// <returns>
		/// An <see cref="AutoUpdateStatus"/> value indicating the result
		/// of the update.
		/// </returns>
		private AutoUpdateStatus CheckForUpdateFromUrl(AspectEngineDataFile dataFile)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
            LogDebugMessage(() => $"Starting {nameof(CheckForUpdateFromUrl)} with {nameof(result)} = {result}", dataFile);

			if (dataFile.Configuration.MemoryOnly)
			{
                LogDebugMessage(() => $"{nameof(dataFile.Configuration.MemoryOnly)} is {dataFile.Configuration.MemoryOnly}", dataFile);
                
				// Perform the update entirely in memory.

				// The uncompressed stream may be read by the engine at a
				// later time. In that case we must not dispose of it 
				// within this method. As such, we cannot use a simple
				// 'using' statement.
#pragma warning disable CA2000 // Dispose objects before losing scope
				var uncompressedStream = new MemoryStream();
#pragma warning restore CA2000 // Dispose objects before losing scope
				bool disposeUncompressedStream = true;
				try
				{
					using (var compressedStream = new MemoryStream())
					{
						result = CheckForUpdateFromUrl(dataFile,
							compressedStream,
							uncompressedStream);
                        LogDebugMessage(() => $"{nameof(CheckForUpdateFromUrl)} resulted in {result}", dataFile);
						if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
						{
							if (KeepIfNoEngine(dataFile, () =>
								dataFile.Configuration.DataStream =
									uncompressedStream) == false)
							{
								try
								{
									LogInfoMessage(Messages.MessageAutoUpdateRefreshEngine, dataFile);
									// Tell the engine to refresh itself with
									// the new data.
									result = RefreshEngine(
										dataFile,
										() => dataFile.Engine.RefreshData(
											dataFile.Identifier,
											uncompressedStream));
								}
								catch (Exception ex)
								{
									result = AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED;
									string msg = string.Format(
										CultureInfo.InvariantCulture,
										Messages.ExceptionAutoUpdateRefreshEngine,
										dataFile.Engine.GetType().Name);
									throw new DataUpdateException(msg, ex, result);
								}
							}
							else
							{
								// No associated engine at the moment, so the
								// data stream was kept for the engine to
								// load.
								result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
								// Make sure the stream will not be disposed 
								// before the engine is created with it.
								disposeUncompressedStream = false;
							}
						}
					}
				}
				finally
				{
					if (disposeUncompressedStream)
					{
						uncompressedStream?.Dispose();
					}
				}
			}
			else
			{
                if (string.IsNullOrEmpty(dataFile.TempDataDirPath))
                {
                    throw new DataUpdateException($"The data file " +
                        $"'{dataFile.Identifier}' is checking for updates but " +
                        $"does not have a temporary file path configured.", 
						AutoUpdateStatus.AUTO_UPDATE_TEMP_PATH_NOT_SET); 
                }

                // There is a data file path so use the temporary
                // file location to store data while we work on downloading
                // and decompressing it.
                string compressedTempFile = Path.Combine(dataFile.TempDataDirPath,
					$"{dataFile.Identifier}-{Guid.NewGuid()}.tmp");
				string uncompressedTempFile = Path.Combine(dataFile.TempDataDirPath,
					$"{dataFile.Identifier}-{Guid.NewGuid()}.tmp");
                if(_fileSystem.Directory.Exists(dataFile.TempDataDirPath) == false)
                {
                    _fileSystem.Directory.CreateDirectory(dataFile.TempDataDirPath);
                }                

				try
				{
					using (var compressedStream = _fileSystem.File.Create(compressedTempFile))
					using (var uncompressedStream = _fileSystem.File.Create(uncompressedTempFile))
					{
						result = CheckForUpdateFromUrl(dataFile,
							compressedStream,
							uncompressedStream);
                        LogDebugMessage(() => $"{nameof(CheckForUpdateFromUrl)} resulted in {result}", dataFile);
					}

					// Disposed during the download, so there is no engine to
					// apply it to and the data file is left as it is. Dispose
					// does not wait for the copy below, so a dispose after
					// this check still lets the copy write the data file,
					// and the refresh then leaves the engine alone.
					if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS &&
						dataFile.TimerClosed)
					{
						result = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
					}

					if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
					{
						// If this engine has a data file watcher then we need to
						// disable it while the update is occurring.
						if (dataFile != null &&
							dataFile.FileWatcher != null)
						{
							dataFile.FileWatcher.EnableRaisingEvents = false;
                            LogDebugMessage(() => $"Disabled {nameof(dataFile.FileWatcher)} events", dataFile);
						}

						try
						{
                            LogDebugMessage(() => $"Will copy {uncompressedTempFile} into {dataFile.DataFilePath}", dataFile);
                            // Copy the uncompressed file to the engine's 
                            // data file location
                            _fileSystem.File.Copy(uncompressedTempFile,
								dataFile.DataFilePath, true);
                            LogDebugMessage(() => $"Did copy {uncompressedTempFile} into {dataFile.DataFilePath}", dataFile);
							// Ensure creation time of the file is set
							// correctly so that the 'If-Modified-Since' 
							// header will be set to the expected value.
							_fileSystem.File.SetCreationTimeUtc(
								dataFile.DataFilePath, DateTime.UtcNow);
                            LogDebugMessage(() => $"Did update timestamp of {dataFile.DataFilePath}", dataFile);
						}
						catch (Exception ex)
						{
							result = AutoUpdateStatus.AUTO_UPDATE_NEW_FILE_CANT_RENAME;
                            throw new DataUpdateException($"An error occurred when copying a " +
								$"data file to replace the existing one at " +
								$"'{dataFile.DataFilePath}'.", ex, result);
						}
						finally
						{
							// Make sure to enable the file watcher again
							// if needed. Dispose closes the file under the
							// timer lock before it disposes the watcher, so
							// while the file is still open here its watcher
							// is not yet disposed.
							if (dataFile != null &&
								dataFile.FileWatcher != null)
							{
								var restored = false;
								lock (dataFile.TimerLock)
								{
									if (dataFile.TimerClosed == false)
									{
										dataFile.FileWatcher.EnableRaisingEvents = true;
										restored = true;
									}
								}
								if (restored)
								{
                                    LogDebugMessage(() => $"Restored {nameof(dataFile.FileWatcher)} events", dataFile);
								}
							}
						}
					}
				}
				finally
				{
                    LogDebugMessage(() => $"Entering {nameof(CheckForUpdateFromUrl)}-finally", dataFile);
					// Make sure the temp files are cleaned up
					if (_fileSystem.File.Exists(compressedTempFile))
					{
                        _fileSystem.File.Delete(compressedTempFile);
					}
					if (_fileSystem.File.Exists(uncompressedTempFile))
					{
                        _fileSystem.File.Delete(uncompressedTempFile);
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Download an update for the specified data file from its update URL.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> containing the 
		/// update configuration settings.
		/// </param>
		/// <param name="compressedDataStream">
		/// A <see cref="Stream"/> to write the data to as it is downloaded.
		/// </param>
		/// <param name="uncompressedDataStream">
		/// A <see cref="Stream"/> to write the uncompressed data to once
		/// download is complete.
		/// If the <see cref="DataFileConfiguration.DecompressContent"/>
		/// flag is set to false then this can be null.
		/// </param>
		/// <returns>
		/// An <see cref="AutoUpdateStatus"/> value indicating the result
		/// of the update.
		/// </returns>
		private AutoUpdateStatus CheckForUpdateFromUrl(
			AspectEngineDataFile dataFile,
			Stream compressedDataStream,
			Stream uncompressedDataStream)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;

            string expectedMd5Hash = null;
			// Check if there is an update and download it if there is                   
			result = DownloadFile(dataFile, compressedDataStream, out expectedMd5Hash);
            LogDebugMessage(() => $"{nameof(DownloadFile)} resulted in {result}, {nameof(expectedMd5Hash)} = {expectedMd5Hash}", dataFile);
			// Check data integrity
			if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS &&
				dataFile.Configuration.VerifyMd5)
			{
                LogDebugMessage(() => $"Will call {nameof(VerifyMd5)}", dataFile);
				result = VerifyMd5(dataFile, expectedMd5Hash, compressedDataStream);
                LogDebugMessage(() => $"{nameof(VerifyMd5)} resulted in {result}", dataFile);
			}
			// decompress the file
			if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
			{
				if (dataFile.Configuration.DecompressContent)
				{
                    LogDebugMessage(() => $"Will call {nameof(Decompress)}", dataFile);
					result = Decompress(
						compressedDataStream, uncompressedDataStream);
                    LogDebugMessage(() => $"{nameof(Decompress)} resulted in {result}", dataFile);
				}
				else
				{
					// If decompression is not needed then just replace
					// the uncompressed stream object with the 'compressed' one
					compressedDataStream.Seek(0, 0);
					compressedDataStream.CopyTo(uncompressedDataStream);
                    // Reset the position to the start of the uncompressed
                    // stream
                    uncompressedDataStream.Seek(0, SeekOrigin.Begin);
                }
			}

			return result;
		}

		/// <summary>
		/// Called when a data update is available and the file at
		/// engine.DataFilePath contains this new data. Refreshes the engine,
		/// trying up to 10 times, and leaves the update timer alone. Gives
		/// not needed, with no refresh, once the file is disposed, unless an
		/// earlier try failed, whose failure is then thrown.
		/// </summary>
		/// <param name="dataFile"></param>
		/// <returns></returns>
		private AutoUpdateStatus UpdatedDataAvailable(
			AspectEngineDataFile dataFile)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;

			Exception exception = null;
			int tries = 0;

            if (KeepIfNoEngine(dataFile, null) == false)
            {
                LogInfoMessage(Messages.MessageAutoUpdateRefreshEngine, dataFile);

                // Try to update the file multiple times to ensure the file is not 
                // locked.
                while (result != AutoUpdateStatus.AUTO_UPDATE_SUCCESS && tries < 10)
				{
					try
					{
						var refreshed = RefreshEngine(
							dataFile,
							() => dataFile.Engine.RefreshData(
								dataFile.Identifier));
						if (refreshed == AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED)
						{
							// Disposed. A failure from an earlier try is
							// still thrown below.
							if (exception == null)
							{
								return refreshed;
							}
							break;
						}
						result = refreshed;
					}
#pragma warning disable CA1031 // Do not catch general exception types
					// The code executed by the RefreshData method could
					// be anything at all. Third parties can create their
					// own implementations so we need to be able to handle
					// any exception.
					catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
					{
						exception = ex;
						result = AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED;
						Thread.Sleep(200);
					}
					tries++;
				}

				if (result != AutoUpdateStatus.AUTO_UPDATE_SUCCESS)
				{
                    throw new DataUpdateException($"An error occurred when applying a " +
							$"data update to engine '{dataFile.Engine.GetType().Name}' " +
							$"after {tries} tries.", exception, result);
				}
			}
			else
			{
				// Engine not yet set, so the new file was kept for the
				// engine to load. We can consider the update a success.
				result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
			}

			return result;
		}

		/// <summary>
		/// When the data file names no engine, runs keep, which keeps the
		/// new data where an engine loads it from, and counts the data as
		/// kept, under the file's update lock. An engine names itself on the
		/// file under the same lock once it has loaded its data, and loads
		/// again if data was kept during its load. So new data is either
		/// loaded by the engine that is loading, or applied by the caller
		/// to the engine the file names.
		/// </summary>
		/// <param name="dataFile">
		/// The data file the new data is for.
		/// </param>
		/// <param name="keep">
		/// Keeps the new data, or null when it is already where the engine
		/// loads it from.
		/// </param>
		/// <returns>
		/// True if the data was kept, false if the file names an engine,
		/// which the caller refreshes.
		/// </returns>
		private static bool KeepIfNoEngine(
			AspectEngineDataFile dataFile,
			Action keep)
		{
			lock (dataFile.UpdateSyncLock)
			{
				if (dataFile.Engine != null)
				{
					return false;
				}
				keep?.Invoke();
				dataFile.CountDataKept();
				return true;
			}
		}

		/// <summary>
		/// Refreshes the data file's engine under the file's update lock,
		/// unless the file has been disposed. Dispose waits for that lock,
		/// so the engine is never freed while it refreshes, and once freed
		/// it is never refreshed.
		/// </summary>
		/// <returns>
		/// Success, or not needed when the file has been disposed and the
		/// engine was left alone.
		/// </returns>
		private static AutoUpdateStatus RefreshEngine(
			AspectEngineDataFile dataFile,
			Action refresh)
		{
			lock (dataFile.UpdateSyncLock)
			{
				if (dataFile.TimerClosed)
				{
					return AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
				}
				refresh();
				return AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
			}
		}

		private static ObjectDisposedException DataFileDisposed(
			AspectEngineDataFile dataFile) =>
			new ObjectDisposedException(
				dataFile.GetType().Name,
				$"The data file '{dataFile.Identifier}' has been disposed, " +
				"so its engine cannot be updated.");

		/// <summary>
		/// Clear the timer ready for a new one to be created.
		/// </summary>
		private static void ClearTimer(AspectEngineDataFile dataFile)
		{
			if (dataFile != null)
			{
				lock (dataFile.TimerLock)
				{
					if (dataFile.Timer != null)
					{
						// Dispose of the old timer object
						dataFile.Timer.Dispose();
						dataFile.Timer = null;
					}
				}
			}
		}

		/// <summary>
		/// Get the most recent data file available from the configured 
		/// update URL.
		/// If the data currently used by the engine is the newest available
		/// then nothing will be downloaded.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> to use.
		/// </param>
		/// <param name="tempStream">
		/// The stream to write the data to.
		/// </param>
		/// <param name="expectedMd5Hash">
		/// Used to output the md5 hash for the file from the 
		/// 'Content-MD5' header.
		/// </param>
		/// <returns>
		/// An <see cref="AutoUpdateStatus"/> value indicating the result
		/// </returns>
		private AutoUpdateStatus DownloadFile(
			AspectEngineDataFile dataFile,
			Stream tempStream,
			out string expectedMd5Hash)
		{
			return DownloadFile(
				dataFile, tempStream, out expectedMd5Hash,
				dataFile.FormattedUri, true, 0);
		}

		/// <summary>
		/// The response status that lists the products the license keys
		/// hold, when the request named none and they hold several.
		/// </summary>
		private const int MultipleChoices = 300;

		/// <summary>
		/// The most redirects that one check follows itself.
		/// </summary>
		private const int MaxRedirects = 20;

		/// <summary>
		/// The most bytes read of a response that lists products.
		/// </summary>
		private const int MaxProductListBytes = 65536;

		/// <summary>
		/// The name of the query parameter that names a product.
		/// </summary>
		private const string ProductParameter = "Product";

		/// <summary>
		/// Get the value of the Product parameter in the query of a URL.
		/// The name of the parameter is matched ignoring case.
		/// </summary>
		/// <param name="url">
		/// The URL to read.
		/// </param>
		/// <returns>
		/// The product named, or null if the URL has no Product parameter
		/// or its value is empty.
		/// </returns>
		private static string ProductOf(string url)
		{
			var query = url.IndexOf('?');
			if (query < 0)
			{
				return null;
			}
			var end = url.IndexOf('#', query);
			var parameters = end < 0 ?
				url.Substring(query + 1) :
				url.Substring(query + 1, end - query - 1);
			foreach (var parameter in parameters.Split('&'))
			{
				if (parameter.StartsWith(
					ProductParameter + "=",
					StringComparison.OrdinalIgnoreCase))
				{
					var value = Uri.UnescapeDataString(
						parameter.Substring(ProductParameter.Length + 1));
					if (value.Length > 0)
					{
						return value;
					}
				}
			}
			return null;
		}

		/// <summary>
		/// Get a URL with the Product parameter added as the last parameter
		/// of its query, percent-encoded. A Product parameter with an empty
		/// value is taken out, so the URL names the product once. The rest
		/// of the query is unchanged.
		/// </summary>
		/// <param name="url">
		/// The URL to add to.
		/// </param>
		/// <param name="product">
		/// The product to name.
		/// </param>
		/// <returns>
		/// The URL that asks for the product.
		/// </returns>
		private static Uri WithProduct(Uri url, string product)
		{
			var text = url.GetLeftPart(UriPartial.Query);
			var query = text.IndexOf('?');
			var path = query < 0 ? text : text.Substring(0, query);
			var parameters = query < 0 ?
				new List<string>() :
				text.Substring(query + 1).Split('&')
					.Where(p => p.Length > 0 && string.Equals(
						p, ProductParameter + "=",
						StringComparison.OrdinalIgnoreCase) == false)
					.ToList();
			parameters.Add(
				ProductParameter + "=" + Uri.EscapeDataString(product));
			return new Uri(path + "?" + string.Join("&", parameters));
		}

		/// <summary>
		/// Get the product names offered in a response that lists the URLs
		/// available to the license keys.
		/// </summary>
		/// <param name="response">
		/// The response to read.
		/// </param>
		/// <returns>
		/// The value of the Product parameter of each URL listed that has
		/// one, in the order of the response, or null if the response has
		/// no such list.
		/// </returns>
		private static List<string> ReadProductsOffered(
			HttpResponseMessage response)
		{
			var uris = new List<string>();
			try
			{
				string body;
				using (var stream = response.Content.ReadAsStreamAsync().Result)
				using (var bytes = new MemoryStream())
				{
					var buffer = new byte[4096];
					int read;
					while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
					{
						bytes.Write(buffer, 0, read);
						if (bytes.Length > MaxProductListBytes)
						{
							return null;
						}
					}
					body = Encoding.UTF8.GetString(bytes.ToArray());
				}
#if NET8_0_OR_GREATER
				using (var document = System.Text.Json.JsonDocument.Parse(body))
				{
					if (document.RootElement.ValueKind !=
							System.Text.Json.JsonValueKind.Object ||
						document.RootElement.TryGetProperty(
							"AvailableUris", out var list) == false ||
						list.ValueKind != System.Text.Json.JsonValueKind.Array)
					{
						return null;
					}
					foreach (var item in list.EnumerateArray())
					{
						if (item.ValueKind == System.Text.Json.JsonValueKind.String)
						{
							uris.Add(item.GetString());
						}
					}
				}
#else
				var list = Newtonsoft.Json.Linq.JObject.Parse(body)
					["AvailableUris"] as Newtonsoft.Json.Linq.JArray;
				if (list == null)
				{
					return null;
				}
				foreach (var item in list)
				{
					if (item.Type == Newtonsoft.Json.Linq.JTokenType.String)
					{
						uris.Add((string)item);
					}
				}
#endif
			}
#pragma warning disable CA1031 // Do not catch general exception types
			// A body that cannot be read as the list is not one.
			catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
			{
				return null;
			}

			var products = new List<string>();
			foreach (var uri in uris)
			{
				var product = ProductOf(uri);
				if (product != null)
				{
					products.Add(product);
				}
			}
			return products;
		}

		/// <summary>
		/// Get the name the update service gives the product of the data
		/// file the engine has loaded.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> to use.
		/// </param>
		/// <returns>
		/// The product name, or null if it is not known.
		/// </returns>
		private static string ProductNeeded(AspectEngineDataFile dataFile)
		{
			try
			{
				var product = dataFile.DataUpdateProduct;
				return string.IsNullOrEmpty(product) ? null : product;
			}
#pragma warning disable CA1031 // Do not catch general exception types
			// The engine can be any implementation. A product that cannot
			// be read is a product that is not known.
			catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
			{
				return null;
			}
		}

		/// <summary>
		/// The error for a product that the update service does not offer.
		/// </summary>
		private static DataUpdateException ProductNotOffered(
			AspectEngineDataFile dataFile,
			string needed,
			string shownUrl,
			IEnumerable<string> offered)
		{
			return new DataUpdateException(
				$"Product '{needed}' is not offered by data update " +
				$"service at '{shownUrl}' for engine " +
				$"'{dataFile.EngineType?.Name}'. Products offered: " +
				$"'{string.Join(",", offered)}'",
				AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR);
		}

		/// <summary>
		/// Get the URL to request next when the response to a request is
		/// not the one to act on. That is the case when the license keys
		/// hold several products and the request named none, and when the
		/// response is a redirect that the HTTP client did not follow.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> to use.
		/// </param>
		/// <param name="response">
		/// The response to the request.
		/// </param>
		/// <param name="url">
		/// The URL requested.
		/// </param>
		/// <param name="answered">
		/// The URL the response came from, which differs from the URL
		/// requested when the HTTP client followed a redirect.
		/// </param>
		/// <param name="shownUrl">
		/// The URL requested, as it is shown in messages.
		/// </param>
		/// <param name="chooseProduct">
		/// True if a product may be chosen for this request.
		/// </param>
		/// <param name="redirects">
		/// The number of redirects this check has followed itself.
		/// </param>
		/// <param name="chooseProductNext">
		/// Set to true if a product may be chosen for the next request.
		/// </param>
		/// <returns>
		/// The URL to request next, or null to act on this response.
		/// </returns>
		/// <exception cref="DataUpdateException">
		/// Thrown if the product of the data file is not offered.
		/// </exception>
		private Uri GetNextUrl(
			AspectEngineDataFile dataFile,
			HttpResponseMessage response,
			Uri url,
			Uri answered,
			string shownUrl,
			bool chooseProduct,
			int redirects,
			out bool chooseProductNext)
		{
			chooseProductNext = false;
			var status = (int)response.StatusCode;

			// A product named in the URL requested is the one asked for,
			// so no other is chosen.
			if (chooseProduct && ProductOf(url.AbsoluteUri) == null)
			{
				// An HTTP client that follows redirects has followed the
				// Location header of the answer that lists the products,
				// which names the first of them. The answer in hand comes
				// from that address and not from the update URL, so it is
				// not used, whatever its status and whichever product the
				// address names.
				var followed = answered != null && answered != url ?
					ProductOf(answered.AbsoluteUri) : null;
				if (followed != null)
				{
					var needed = ProductNeeded(dataFile);
					if (needed == null)
					{
						throw ProductNotOffered(
							dataFile, string.Empty, shownUrl,
							new[] { followed });
					}
					LogProductRequested(dataFile, needed, shownUrl);
					return WithProduct(url, needed);
				}

				if (status == MultipleChoices)
				{
					var offered = ReadProductsOffered(response);
					if (offered != null && offered.Count > 0)
					{
						// The HTTP client can have followed a redirect to
						// the address that answered 300. The request for
						// the product goes to that address, and the
						// messages name it.
						var from = answered ?? url;
						var shownFrom = from == url ? shownUrl :
							LicenseKeyMask.MaskUri(
								from,
								dataFile.Configuration.DataUpdateLicenseKeys);
						var needed = ProductNeeded(dataFile);
						var chosen = needed == null ? null :
							offered.FirstOrDefault(p => string.Equals(
								p, needed, StringComparison.OrdinalIgnoreCase));
						if (chosen == null)
						{
							throw ProductNotOffered(
								dataFile, needed ?? string.Empty, shownFrom,
								offered);
						}
						LogProductRequested(dataFile, chosen, shownFrom);
						return WithProduct(from, chosen);
					}
				}
			}

			// A redirect that the HTTP client did not follow is followed
			// here, so a client set not to follow redirects still reaches
			// an update URL that has moved. A redirect from https to http
			// is not followed.
			if ((status == 301 || status == 302 || status == 303 ||
					status == 307 || status == 308) &&
				response.Headers.Location != null &&
				redirects < MaxRedirects)
			{
				var from = answered ?? url;
				var to = response.Headers.Location.IsAbsoluteUri ?
					response.Headers.Location :
					new Uri(from, response.Headers.Location);
				if (from.Scheme == Uri.UriSchemeHttps &&
					to.Scheme == Uri.UriSchemeHttp)
				{
					return null;
				}
				chooseProductNext = chooseProduct;
				return to;
			}

			return null;
		}

		/// <summary>
		/// Log that the request is repeated for a product.
		/// </summary>
		private void LogProductRequested(
			AspectEngineDataFile dataFile, string product, string shownUrl)
		{
			_logger.LogInformation(
				$"Requesting product '{product}' from '{shownUrl}' for " +
				$"Engine '{dataFile.EngineType?.Name}'");
		}

		/// <summary>
		/// Get the most recent data file available from the URL given.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> to use.
		/// </param>
		/// <param name="tempStream">
		/// The stream to write the data to.
		/// </param>
		/// <param name="expectedMd5Hash">
		/// Used to output the md5 hash for the file from the
		/// 'Content-MD5' header.
		/// </param>
		/// <param name="url">
		/// The URL to request.
		/// </param>
		/// <param name="chooseProduct">
		/// True if the request may be repeated for the product of the
		/// loaded data file. False for a request that is such a repeat.
		/// </param>
		/// <param name="redirects">
		/// The number of redirects this check has followed itself.
		/// </param>
		/// <returns>
		/// An <see cref="AutoUpdateStatus"/> value indicating the result
		/// </returns>
		private AutoUpdateStatus DownloadFile(
			AspectEngineDataFile dataFile,
			Stream tempStream,
			out string expectedMd5Hash,
			Uri url,
			bool chooseProduct,
			int redirects)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
			expectedMd5Hash = null;
			// Set when another URL is to be requested in place of acting
			// on the response to this one.
			Uri nextUrl = null;
			bool chooseProductNext = false;
			int redirectsNext = redirects;

			// The URL carries the license keys, which must not appear in full
			// in a log entry or an error message.
			var licenseKeys = dataFile.Configuration.DataUpdateLicenseKeys;
			string shownUrl = LicenseKeyMask.MaskUri(url, licenseKeys);
            LogInfoMessage($"Checking for update from {shownUrl}", dataFile);

			using (HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, url))
			{

				// Get data file published date from meta-data.
				// If it's not been set for some reason then use the 
				// last write time of the file instead.
				DateTime publishDate = dataFile.DataPublishedDateTime;
				if (dataFile.DataPublishedDateTime <= DateTime.MinValue &&
					string.IsNullOrEmpty(dataFile.DataFilePath) == false &&
					 _fileSystem.File.Exists(dataFile.DataFilePath))
				{
					publishDate = _fileSystem.File.GetLastWriteTimeUtc(dataFile.DataFilePath);
				}

				// Set last-modified header to ensure that a file will only
				// be downloaded if it is newer than the data we already have.
				if (dataFile.Configuration.VerifyModifiedSince == true)
				{
					message.Headers.Add(
						 "If-Modified-Since",
						 publishDate.ToString("R", CultureInfo.InvariantCulture));
				}

				HttpResponseMessage response = null;
				try
				{
					// Send the message. Only the headers are read before the
					// call returns, so the body is copied to the temporary file
					// as it arrives and the client's buffer limit does not
					// apply to it. The client's timeout covers the headers here,
					// and each read of the body below.
					response = _httpClient.SendAsync(
						message,
						HttpCompletionOption.ResponseHeadersRead).Result;
				}
				catch (Exception ex)
				{
					result = AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR;
					throw new DataUpdateException($"Error accessing data update service at " +
						$"'{shownUrl}' for engine '{dataFile.EngineType?.Name}'",
						LicenseKeyMask.MaskException(ex, licenseKeys), result);
				}

				// The response holds its connection until it is disposed.
				using (response)
				{
					if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
					{
						if (response == null)
						{
							result = AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR;
							throw new DataUpdateException($"No response from data update service at " +
								$"'{shownUrl}' for engine '{dataFile.EngineType?.Name}'", result);
						}
						else
						{
							if ((nextUrl = GetNextUrl(
								dataFile,
								response,
								url,
								response.RequestMessage?.RequestUri ??
									message.RequestUri,
								shownUrl,
								chooseProduct,
								redirects,
								out chooseProductNext)) != null)
							{
								// The next URL is requested below, once this
								// response has been disposed of.
								if (chooseProductNext == chooseProduct)
								{
									redirectsNext = redirects + 1;
								}
							}
							else if (response.IsSuccessStatusCode)
							{
								// If the response is successful then save the content to a 
								// temporary file
								LogDebugMessage(() => $"Will call {nameof(response.Content.ReadAsStreamAsync)}", dataFile);
								using (var dataStream = ReadDownload(
									() => response.Content.ReadAsStreamAsync().Result,
									dataFile, shownUrl, licenseKeys))
	                            {
	                                LogDebugMessage(() => $"Will copy {nameof(dataStream)} into {nameof(tempStream)}", dataFile);
	                                var buffer = new byte[DownloadBufferBytes];
	                                int read;
	                                while ((read = ReadDownload(
	                                    () => ReadDownloadBlock(dataStream, buffer),
	                                    dataFile, shownUrl, licenseKeys)) > 0)
	                                {
	                                    tempStream.Write(buffer, 0, read);
	                                }
	                                LogDebugMessage(() => $"Did copy {nameof(dataStream)} into {nameof(tempStream)}", dataFile);
	                            }
								_logger.LogInformation(
									$"Downloaded new data from '{shownUrl}' for engine " +
									$"'{dataFile.EngineType?.Name}'");
	                            LogDebugMessage(() => $"Will test for {nameof(dataFile.Configuration.VerifyMd5)}", dataFile);
	                            if (dataFile.Configuration.VerifyMd5)
	                            {
	                                LogDebugMessage(() => $"Did test for {nameof(dataFile.Configuration.VerifyMd5)}", dataFile);
	                                IEnumerable<string> values;
									if (response.Content.Headers.TryGetValues("Content-MD5", out values))
									{
										expectedMd5Hash = values.SingleOrDefault();
										var lastHash = expectedMd5Hash;
	                                    LogDebugMessage(() => $"{nameof(expectedMd5Hash)} = {lastHash}", dataFile);
	                                }
									else
									{
										_logger.LogWarning(
											$"No MD5 hash included in data update response for " +
											$"'{shownUrl}'. Unable to verify data integrity");
									}
								}
							}
							else
							{
								switch (response.StatusCode)
								{
									// Note: needed because TooManyRequests is not available 
									// in some versions of the HttpStatusCode enum.
									case ((HttpStatusCode)429):
										result = AutoUpdateStatus.
											AUTO_UPDATE_ERR_429_TOO_MANY_ATTEMPTS;
										throw new DataUpdateException($"Too many requests to " +
											$"'{shownUrl}' for engine '{dataFile.EngineType?.Name}'", result);
									case HttpStatusCode.NotModified:
										result = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
										_logger.LogInformation($"No data newer than " +
											$"{publishDate} found at '{shownUrl}' for engine " +
											$"'{dataFile.EngineType?.Name}'"); ;
										break;
									case HttpStatusCode.Forbidden:
										result = AutoUpdateStatus.AUTO_UPDATE_ERR_403_FORBIDDEN;
										throw new DataUpdateException($"Access denied to data update service at " +
											$"'{shownUrl}' for engine '{dataFile.EngineType?.Name}'", result);
									default:
										result = AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR;
										// The error names the address that
										// answered, which differs from the URL
										// requested when the HTTP client
										// followed a redirect.
										var answeredBy =
											response.RequestMessage?.RequestUri;
										var shownAnswer =
											answeredBy == null || answeredBy == url ?
											shownUrl :
											LicenseKeyMask.MaskUri(
												answeredBy, licenseKeys);
										throw new DataUpdateException($"HTTP status code '{response.StatusCode}' " +
											$"from data update service at " +
											$"'{shownAnswer}' for engine '{dataFile.EngineType?.Name}'", result);
								}
							}
						}
					}
				}
			}

			if (nextUrl != null)
			{
				result = DownloadFile(
					dataFile, tempStream, out expectedMd5Hash,
					nextUrl, chooseProductNext, redirectsNext);
			}

			return result;
		}

		/// <summary>
		/// The size in bytes of each read of a data file download.
		/// </summary>
		private const int DownloadBufferBytes = 81920;

		/// <summary>
		/// Reads the next block of a data file download. The read ends with
		/// a <see cref="TimeoutException"/> when no bytes arrive within the
		/// client's timeout, so a server that stops sending cannot hold the
		/// check for ever.
		/// </summary>
		private int ReadDownloadBlock(Stream stream, byte[] buffer)
		{
			var timeout = _httpClient.Timeout;
			using (var cancel = new CancellationTokenSource())
			{
				var read = stream.ReadAsync(
					buffer, 0, buffer.Length, cancel.Token);
				if (Task.WaitAny(new Task[] { read }, timeout) == -1)
				{
					cancel.Cancel();
					throw new TimeoutException(
						$"No data arrived within the client's timeout of {timeout}.");
				}
				return read.GetAwaiter().GetResult();
			}
		}

		/// <summary>
		/// Runs one read of a data file download. A read that fails, such as
		/// one of a body that ends early, is reported as the failure to
		/// access the data update service, with the cause attached, as the
		/// Errors section of the data updates specification gives it. Only
		/// reads go through here, so a failure to write the temporary file
		/// is not reported as one of the service's.
		/// </summary>
		private static T ReadDownload<T>(
			Func<T> read,
			AspectEngineDataFile dataFile,
			string shownUrl,
			IEnumerable<string> licenseKeys)
		{
			try
			{
				return read();
			}
			catch (Exception ex)
			{
				throw new DataUpdateException($"Error accessing data update service at " +
					$"'{shownUrl}' for engine '{dataFile.EngineType?.Name}'",
					LicenseKeyMask.MaskException(ex, licenseKeys),
					AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR);
			}
		}

		/// <summary>
		/// Reads a source GZip stream and writes the uncompressed data to 
		/// destination stream.
		/// </summary>
		/// <param name="compressedDataStream">
		/// Stream containing GZipped data to be uncompressed
		/// </param>
		/// <param name="uncompressedDataStream">
		/// Stream to write the uncompressed data to.
		/// </param>
		/// <returns>The current state of the update process.</returns>
		private static AutoUpdateStatus Decompress(
			Stream compressedDataStream,
			Stream uncompressedDataStream)
		{
			AutoUpdateStatus status = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
			compressedDataStream.Position = 0;
#if !NET8_0_OR_GREATER
			using (var fis = new GZipInputStream(
				compressedDataStream)
			{
                IsStreamOwner = false,
            })
#else
			using (var fis = new GZipStream(
				compressedDataStream, CompressionMode.Decompress, true))
#endif
			{
				fis.CopyTo(uncompressedDataStream);
            }
            // Reset the position to the start of the uncompressed stream
            uncompressedDataStream.Seek(0, SeekOrigin.Begin);
            return status;
		}

		/// <summary>
		/// Check that the specified file matches the specified md5 hash
		/// </summary>
		/// <param name="dataFile">
		/// The meta-data relating to the data file to verify
		/// </param>
		/// <param name="serverHash">
		/// The expected md5 hash value
		/// </param>
		/// <param name="compressedDataStream">
		/// The stream containing the data to hash
		/// </param>
		/// <returns>
		/// True if the hashes match, false if not.
		/// </returns>
		private AutoUpdateStatus VerifyMd5(
			AspectEngineDataFile dataFile, 
			string serverHash, Stream compressedDataStream)
		{
			AutoUpdateStatus status = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
            LogDebugMessage(() => $"Starting {nameof(CheckForUpdate)} with {nameof(status)} = {status}", dataFile);
			string downloadHash = GetMd5(compressedDataStream);
            LogDebugMessage(() => $"{nameof(GetMd5)} resulted in {downloadHash}", dataFile);
			if (serverHash == null ||
				string.Equals(serverHash, downloadHash, StringComparison.Ordinal) == false)
			{
				status = AutoUpdateStatus.AUTO_UPDATE_ERR_MD5_VALIDATION_FAILED;
                LogDebugMessage(() => $"Set {nameof(status)} to {status}", dataFile);
                throw new DataUpdateException(
					$"Integrity check failed. MD5 hash in HTTP response " +
					$"'{serverHash}' for '{dataFile.EngineType?.Name}'" +
                    $"data update does not match calculated hash for the " +
					$"downloaded file '{downloadHash}'.", status);
			} 
			return status;
		}

		/// <summary>
		/// Calculates the MD5 hash of the given data array.
		/// </summary>
		/// <param name="compressedDataStream">
		/// The stream containing the data to hash
		/// </param>
		/// <returns>The MD5 hash of the given data.</returns>
		private string GetMd5(Stream compressedDataStream)
		{
            LogDebugMessage(() => $"Starting {nameof(GetMd5)}", null);
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
			// TODO: Add support for a better hashing algorithm such as SHA512
			using (MD5 md5Hash = MD5.Create())
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
			{
                LogDebugMessage(() => $"Created {nameof(MD5)} instance", null);
				compressedDataStream.Position = 0;
                LogDebugMessage(() => $"Did reset {nameof(compressedDataStream.Position)}", null);
				return GetMd5(md5Hash, compressedDataStream);
			}
		}

		/// <summary>
		/// Calculates the MD5 hash of the given data array.
		/// </summary>
		/// <param name="stream">calculate MD5 of this stream</param>
		/// <param name="md5Hash">instance of MD5 hash calculator</param>
		/// <returns>The MD5 hash of the given data.</returns>
		private string GetMd5(MD5 md5Hash, Stream stream)
		{
            LogDebugMessage(() => $"Starting {nameof(GetMd5)}", null);
			// Convert the input string to a byte array and compute the hash.
			byte[] data = md5Hash.ComputeHash(stream);

            LogDebugMessage(() => $"Did call {nameof(md5Hash.ComputeHash)}", null);
			// Create a new stringbuilder to collect the bytes
			// and create a string.
			StringBuilder sb = new StringBuilder();

			// Loop through each byte of the hashed data 
			// and format each one as a hexadecimal string.
			for (int i = 0; i < data.Length; i++)
			{
				sb.Append(data[i].ToString("x2", CultureInfo.InvariantCulture));
			}

			// Return the hexadecimal string.
			var result = sb.ToString();
            LogDebugMessage(() => $"{nameof(GetMd5)} will return {result}", null);
            return result;
		}
		
		/// <summary>
		/// Get an interval in the form of a <see cref="TimeSpan"/> based
		/// on the configuration object provided.
		/// If configured, a random additional number of seconds is added 
		/// between 0 and the specified maximum.
		/// </summary>
		/// <param name="config">
		/// The configuration to use to generate the interval
		/// </param>
		/// <returns>
		/// A TimeSpan representing time interval.
		/// </returns>
		private TimeSpan GetInterval(IDataFileConfiguration config)
		{
			int seconds = 0;
			if (config.PollingIntervalSeconds > 0)
			{
				seconds = config.PollingIntervalSeconds;
			}
			return ApplyIntervalRandomisation(
				TimeSpan.FromSeconds(seconds), config);
		}

		/// <summary>
		/// Add a random amount of time to the specified interval
		/// </summary>
		/// <param name="interval">
		/// The <see cref="TimeSpan"/> to add a random amount of time to.
		/// </param>
		/// <param name="config">
		/// The <see cref="IDataFileConfiguration"/> object that 
		/// specifies the maximum number of seconds to add.
		/// </param>
		private TimeSpan ApplyIntervalRandomisation(TimeSpan interval, 
			IDataFileConfiguration config)
		{
			int seconds = 0;
			if (config.MaxRandomisationSeconds > 0)
			{
				double fraction;
				lock (_rndLock)
				{
					fraction = _rnd.NextDouble();
				}
				seconds = (int)(fraction * config.MaxRandomisationSeconds);
			}
			return interval.Add(TimeSpan.FromSeconds(seconds));
		}

        private void LogInfoMessage(
            string message, 
            IAspectEngineDataFile dataFile)
			=> _logger.LogInformation(BuildLogMessage(message, dataFile));

		private void LogDebugMessage(
			Func<string> message,
			IAspectEngineDataFile dataFile)
		{
			if (DebugLoggingEnabled) {
				_logger.LogDebug(BuildLogMessage(message(), dataFile));
			}
		}

        private static string BuildLogMessage(
            string message,
            IAspectEngineDataFile dataFile)
        {
            StringBuilder fullMessage = new StringBuilder();
            if (dataFile != null)
            {
                fullMessage.Append($"Data file '{dataFile.Identifier}' ");
                fullMessage.Append($"for engine '{dataFile.EngineType?.Name}'");
            }
            fullMessage.Append(message);
            return fullMessage.ToString();
        }
		#endregion
	}
}
