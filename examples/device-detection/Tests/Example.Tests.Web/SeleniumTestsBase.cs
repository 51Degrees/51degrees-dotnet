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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.BiDi;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Web
{
    public class SeleniumTestsBase
    {
        static SeleniumTestsBase()
        {
            // A driver found on PATH is used even when it no longer matches
            // the installed browser, and CI images ship ones that lag behind
            // (msedgedriver 151 against Edge 153 on macOS x64). Skipping PATH
            // makes Selenium Manager fetch a matching driver. Set here rather
            // than only in ci/setup-environment.ps1 because other pipelines,
            // such as device-detection-dotnet's package tests, run these
            // tests without that script. The Selenium Manager process
            // inherits it from this one.
            if (Environment.GetEnvironmentVariable("SE_SKIP_DRIVER_IN_PATH")
                == null)
            {
                Environment.SetEnvironmentVariable(
                    "SE_SKIP_DRIVER_IN_PATH", "true");
            }
        }

        /// <summary>
        /// Number of seconds to wait for a response that might satisfy the
        /// test.
        /// </summary>
        protected static readonly TimeSpan TEST_TIMEOUT = 
            TimeSpan.FromSeconds(20);

        /// <summary>
        /// The driver being used for the active test. See 
        /// <see cref="InitializeChromeDriver"/>,
        /// <see cref="InitializeEdgeDriver"/>,
        /// <see cref="InitializeFirefoxDriver"/>.
        ///
        /// IMPORTANT: The driver and the browser state below are static and are
        /// created once per test class from a static [ClassInitialize] method,
        /// then disposed from a static [ClassCleanup] method. This is
        /// deliberate. The CI environment cannot start more than one driver in
        /// the same session, so a per-test [TestInitialize] driver fails the
        /// build. MSTest also requires class level fixture methods to be static,
        /// which is why these members must be static for [ClassInitialize] to
        /// reach them. Do NOT change these back to instance members driven from
        /// [TestInitialize]; doing so reintroduces the CI failure.
        /// </summary>
        protected static WebDriver Driver { get; private set; }

        /// <summary>
        /// Expected name of the browser reported by device detection.
        /// </summary>
        protected static string BrowserName;

        /// <summary>
        /// Expected browser version reported by device detection.
        /// </summary>
        protected static Version BrowserVersion;

        /// <summary>
        /// Cross browser network adapter, built on the WebDriver BiDi protocol
        /// so Chrome, Edge and Firefox all get real network inspection. Null
        /// only if a BiDi session could not be established, in which case the
        /// tests that need it report themselves inconclusive.
        /// </summary>
        protected static BiDiNetworkAdapter Network { get; private set; }

        /// <summary>
        /// The log file of the Chrome or Edge driver, written at the verbose
        /// level, or null for Firefox. Read only when a test fails, see
        /// <see cref="WriteDriverLog"/>.
        /// </summary>
        private static string DriverLogPath;

        /// <summary>
        /// The length of <see cref="DriverLogPath"/> when this test started,
        /// so a failure shows only what the driver did for this test.
        /// </summary>
        private long _driverLogStart;

        /// <summary>
        /// True once the driver log has been written for this test.
        /// </summary>
        private bool _driverLogWritten;

        /// <summary>
        /// Set by MSTest.
        /// </summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Used to stop the server when the test is finished.
        /// </summary>
        private readonly CancellationTokenSource StopSource = 
            new CancellationTokenSource();

        /// <summary>
        /// Function used to start the web server under test.
        /// </summary>
        private readonly Func<CancellationToken, Task> StartServerFunc;

        /// <summary>
        /// The task that is running the server.
        /// </summary>
        private Task ServerTask { get; set; }


        public SeleniumTestsBase(Func<CancellationToken, Task> startServer)
        {
            StartServerFunc = startServer;
        }

        [TestInitialize]
        public void TestServerInitialize()
        {
            _driverLogStart = DriverLogLength();
            ServerTask = StartServerFunc(StopSource.Token);
            try
            {
                ResetBrowserState();
            }
            catch
            {
                WriteDriverLog("resetting the browser");
                throw;
            }
        }

        /// <summary>
        /// Gives each test the browser as a new visitor would find it. The
        /// driver lives for the whole class, and 51Degrees.core.js keeps the
        /// JSON it fetched in session storage and its evidence in cookies.
        /// Without this a test that follows another on the same page finds
        /// the cached result, the script makes no request, and a test that
        /// waits for that request times out. A new tab has empty session
        /// storage, and through WebDriver BiDi the cookies of every site
        /// are deleted. Without BiDi the classic call deletes only the
        /// cookies of the page the tab is on, which on about:blank are
        /// none. The old tab leaves its page first, so no script sets a
        /// cookie again, and the cookies are deleted before any tab
        /// closes, so the command never meets a tab that is closing.
        /// </summary>
        private static void ResetBrowserState()
        {
            if (Driver == null)
            {
                return;
            }
            Driver.Navigate().GoToUrl("about:blank");
            var deleting = Stopwatch.StartNew();
            if (Network != null)
            {
                Network.DeleteAllCookiesAsync().Wait();
            }
            else
            {
                Driver.Manage().Cookies.DeleteAllCookies();
            }
            Console.WriteLine(
                "Build log: Selenium tests, the cookies were deleted in " +
                $"{deleting.ElapsedMilliseconds} ms.");
            var previous = Driver.CurrentWindowHandle;
            Driver.SwitchTo().NewWindow(WindowType.Tab);
            var fresh = Driver.CurrentWindowHandle;
            Driver.SwitchTo().Window(previous);
            Driver.Close();
            Driver.SwitchTo().Window(fresh);
        }

        /// <summary>
        /// Stops the per-test web server. The server is started once per test
        /// in <see cref="TestServerInitialize"/>, so it is stopped here. The
        /// driver is not touched here; it lives for the whole class and is
        /// disposed in <see cref="ClassCleanup"/>.
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            if (TestContext != null &&
                TestContext.CurrentTestOutcome != UnitTestOutcome.Passed &&
                TestContext.CurrentTestOutcome != UnitTestOutcome.Inconclusive)
            {
                WriteDriverLog("the test");
            }
            if (ServerTask != null)
            {
                StopSource.Cancel(true);
                ServerTask.Wait();
            }
        }

        /// <summary>
        /// Disposes the driver created once for the class. This must be a static
        /// [ClassCleanup] method because MSTest requires class level fixture
        /// methods to be static, and because the CI environment cannot start
        /// more than one driver in the same session, so the driver is created
        /// once per class rather than once per test. Do NOT move this teardown
        /// into [TestCleanup]; it pairs with the static [ClassInitialize] on
        /// each browser test class.
        /// </summary>
        // MSTest runs a [ClassCleanup] declared on a base class for its
        // derived test classes only when asked to. Without the arguments this
        // never ran, every class left its driver and browser running, and on
        // Windows those processes kept 'dotnet test' from exiting until the
        // job timed out.
        [ClassCleanup(
            InheritanceBehavior.BeforeEachDerivedClass,
            ClassCleanupBehavior.EndOfClass)]
        public static void ClassCleanup()
        {
            QuitDriver();
        }

        /// <summary>
        /// Quits and disposes the driver, if there is one, so that at most one
        /// driver and browser are ever running.
        /// </summary>
        private static void QuitDriver()
        {
            if (Driver == null)
            {
                return;
            }
            try
            {
                Driver.Quit();
            }
            catch (WebDriverException)
            {
                // The browser has already gone. Disposing below still stops
                // the driver process.
            }
            Driver.Dispose();
            Driver = null;
            Network = null;
            if (DriverLogPath != null)
            {
                try
                {
                    File.Delete(DriverLogPath);
                }
                catch (IOException)
                {
                    // Left in the temporary folder.
                }
                DriverLogPath = null;
            }
        }

        /// <summary>
        /// Has the Chrome or Edge driver write a verbose log, with a time on
        /// each line, to a new file in the temporary folder. It holds every
        /// DevTools command the driver and its BiDi mapper send to the
        /// browser and when each one was answered.
        /// </summary>
        private static void LogDriver(ChromiumDriverService service)
        {
            DriverLogPath = Path.Combine(
                Path.GetTempPath(),
                $"51degrees-driver-{Guid.NewGuid():N}.log");
            service.LogPath = DriverLogPath;
            service.EnableVerboseLogging = true;
            service.ReadableTimestamp = true;
        }

        private static long DriverLogLength()
        {
            try
            {
                return DriverLogPath != null && File.Exists(DriverLogPath)
                    ? new FileInfo(DriverLogPath).Length
                    : 0;
            }
            catch (IOException)
            {
                return 0;
            }
        }

        /// <summary>
        /// Writes what the driver logged since this test started, keeping
        /// the commands and their answers and leaving out the events, so a
        /// failure that comes down to the browser not answering shows which
        /// command it did not answer and for how long. Keys are replaced
        /// before anything is written. Only a diagnostic, so it never
        /// throws.
        /// </summary>
        /// <param name="what">What failed.</param>
        private void WriteDriverLog(string what)
        {
            if (_driverLogWritten || DriverLogPath == null)
            {
                return;
            }
            _driverLogWritten = true;
            Console.WriteLine(
                $"Build log: Selenium tests, {what} failed. The thread " +
                $"pool has {ThreadPool.ThreadCount} threads and " +
                $"{ThreadPool.PendingWorkItemCount} queued items.");
            try
            {
                var lines = new List<string>();
                using (var stream = new FileStream(
                    DriverLogPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    stream.Seek(
                        Math.Min(_driverLogStart, stream.Length),
                        SeekOrigin.Begin);
                    using var reader = new StreamReader(stream);
                    var entry = new StringBuilder();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (EntryStart.IsMatch(line) && entry.Length > 0)
                        {
                            AddSummary(lines, entry.ToString());
                            entry.Clear();
                        }
                        entry.AppendLine(line);
                    }
                    if (entry.Length > 0)
                    {
                        AddSummary(lines, entry.ToString());
                    }
                }
                const int head = 50, tail = 250;
                Console.WriteLine(
                    $"Build log: the driver's commands and answers for " +
                    $"this test, {lines.Count} lines:");
                var shown = lines.Count <= head + tail
                    ? lines
                    : lines.Take(head)
                        .Append($"... {lines.Count - head - tail} lines ...")
                        .Concat(lines.Skip(lines.Count - tail));
                foreach (var summary in shown)
                {
                    Console.WriteLine(Redact(summary));
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"Build log: the driver log could not be read: " +
                    exception.Message);
            }
        }

        /// <summary>
        /// The start of an entry in the driver log, which is its time.
        /// </summary>
        private static readonly Regex EntryStart = new Regex(
            @"^\[\d\d-\d\d-\d{4} [\d:.]+\]", RegexOptions.CultureInvariant);

        private static readonly Regex Method = new Regex(
            @"""method"":\s*""(?<m>[A-Za-z.]+)""", RegexOptions.CultureInvariant);

        private static readonly Regex BiDiMethod = new Regex(
            @"\\""id\\"":(?<id>\d+),\\""method\\"":\\""(?<m>[A-Za-z.]+)",
            RegexOptions.CultureInvariant);

        private static readonly Regex Id = new Regex(
            @"""id"":\s*(?<id>\d+)", RegexOptions.CultureInvariant);

        /// <summary>
        /// Adds one line for a driver log entry that is a command, an
        /// answer, or a DevTools message the BiDi mapper in the browser
        /// sent or was answered, and nothing for anything else. The mapper
        /// reports each DevTools message it sends and receives back to the
        /// driver, which logs it as an event.
        /// </summary>
        private static void AddSummary(List<string> lines, string entry)
        {
            var newLine = entry.IndexOf('\n');
            var first = (newLine < 0 ? entry : entry.Substring(0, newLine))
                .TrimEnd('\r');
            if (first.Length > 200)
            {
                first = first.Substring(0, 200);
            }
            var time = EntryStart.Match(first).Value;
            if (first.Contains(" COMMAND ") || first.Contains(" RESPONSE ") ||
                first.Contains("WebSocket Response:"))
            {
                lines.Add(first);
            }
            else if (first.Contains("WebSocket Command:"))
            {
                var bidi = BiDiMethod.Match(entry);
                lines.Add(bidi.Success
                    ? $"{time} BiDi command {bidi.Groups["id"].Value} " +
                        $"{bidi.Groups["m"].Value} sent to the mapper"
                    : first);
            }
            else if (first.Contains("Runtime.bindingCalled"))
            {
                if (entry.Contains("\"sendBidiResponse\""))
                {
                    lines.Add($"{time} the mapper answered a BiDi command");
                }
                else if (entry.Contains("cdp:SEND"))
                {
                    lines.Add($"{time} mapper sent DevTools " +
                        $"{Id.Match(entry).Groups["id"].Value} " +
                        Method.Match(entry).Groups["m"].Value);
                }
                else if (entry.Contains("cdp:RECV") &&
                    Method.IsMatch(entry) == false && Id.IsMatch(entry))
                {
                    lines.Add($"{time} mapper got the answer to DevTools " +
                        Id.Match(entry).Groups["id"].Value);
                }
            }
        }

        /// <summary>
        /// Replaces the value of every key in the environment, as the
        /// driver logs the address of each page and script, and a client
        /// only page loads a script named after its resource key.
        /// </summary>
        private static string Redact(string line)
        {
            foreach (var value in Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .Where(e => e.Key.ToString().StartsWith(
                    "_51DEGREES_", StringComparison.Ordinal) &&
                    e.Key.ToString().Contains(
                        "KEY", StringComparison.Ordinal))
                .Select(e => e.Value?.ToString())
                .Where(v => string.IsNullOrEmpty(v) == false && v.Length > 4))
            {
                line = line.Replace(value, "<key>", StringComparison.Ordinal);
            }
            return line;
        }


        /// <summary>
        /// Sets the <see cref="Driver"/> property for Chrome tests. If the 
        /// initilaization fails the test is flagged as inconclusive.
        /// </summary>
        protected static void InitializeChromeDriver()
        {
            QuitDriver();
            // If the driver and chrome versions are different it may cause
            // unexpected behaviour. 
            // See: https://sites.google.com/chromium.org/driver/downloads and
            // https://github.com/rosolko/WebDriverManager.Net
            var chromeOptions = new ChromeOptions();
            chromeOptions.AcceptInsecureCertificates = true;
            // Ask the driver for the BiDi WebSocket URL so the cross browser
            // network adapter can attach. Without this AsBiDiAsync has no
            // endpoint to connect to.
            chromeOptions.UseWebSocketUrl = true;
            chromeOptions.AddArgument("--headless=new");
            chromeOptions.AddArgument("--ignore-certificate-errors");
            chromeOptions.SetLoggingPreference(LogType.Browser, LogLevel.All);
            try
            {
                var service = ChromeDriverService.CreateDefaultService();
                LogDriver(service);
                Driver = new ChromeDriver(service, chromeOptions);
            }
            catch (WebDriverException exception)
            {
                SkipBecauseBrowserUnavailable("Chrome", exception);
            }
            Network = GetNetwork(Driver).Result;
            BrowserName = "Chrome";
            BrowserVersion = Version.Parse(
                (string)Driver.Capabilities["browserVersion"]);
        }

        /// <summary>
        /// Sets the <see cref="Driver"/> property for Edge tests. If the 
        /// initilaization fails the test is flagged as inconclusive.
        /// </summary>
        protected static void InitializeEdgeDriver()
        {
            QuitDriver();
            var edgeOptions = new EdgeOptions();
            edgeOptions.AcceptInsecureCertificates = true;
            // See the Chrome initializer: enables the BiDi WebSocket endpoint.
            edgeOptions.UseWebSocketUrl = true;
            edgeOptions.AddArgument("--headless=new");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) == true)
            {
                // Ubuntu 24.04 confines unprivileged user namespaces with
                // AppArmor, and the Edge package ships no profile of its own,
                // so the sandbox cannot start and the browser exits during
                // session creation. Chrome ships a profile, which is why only
                // Edge needs this.
                edgeOptions.AddArgument("--no-sandbox");
            }
            edgeOptions.SetLoggingPreference(LogType.Browser, LogLevel.All);
            try
            {
                var service = EdgeDriverService.CreateDefaultService();
                LogDriver(service);
                Driver = new EdgeDriver(service, edgeOptions);
            }
            catch (WebDriverException exception)
            {
                SkipBecauseBrowserUnavailable("Edge", exception);
            }
            Network = GetNetwork(Driver).Result;
            BrowserName = "Edge";
            BrowserVersion = Version.Parse(
                (string)Driver.Capabilities["browserVersion"]);
        }

        /// <summary>
        /// Sets the <see cref="Driver"/> property for Firefox tests. If the 
        /// initilaization fails the test is flagged as inconclusive.
        /// </summary>
        protected static void InitializeFirefoxDriver()
        {
            QuitDriver();
            var firefoxOptions = new FirefoxOptions();
            firefoxOptions.AcceptInsecureCertificates = true;
            firefoxOptions.AddArgument("--headless");
            // A resource key with the location product gives the browser a
            // script that asks for its position. Headless Chrome and Edge
            // deny that at once, but headless Firefox leaves the prompt open
            // with no one to answer it, so the client-only page never
            // completes. Deny it here as the other browsers do.
            firefoxOptions.SetPreference("permissions.default.geo", 2);
            // Firefox does not implement the Chrome DevTools Protocol, so the
            // network adapter uses the W3C BiDi protocol instead. Ask for the
            // BiDi WebSocket URL so AsBiDiAsync can attach.
            firefoxOptions.UseWebSocketUrl = true;
            firefoxOptions.SetLoggingPreference(LogType.Browser, LogLevel.All);
            try
            {
                Driver = new FirefoxDriver(firefoxOptions);
            }
            catch (WebDriverException exception)
            {
                SkipBecauseBrowserUnavailable("Firefox", exception);
            }
            Network = GetNetwork(Driver).Result;
            BrowserName = "Firefox";
            BrowserVersion = Version.Parse(
                (string)Driver.Capabilities["browserVersion"]);
        }

        /// <summary>
        /// Skips the test because the browser it needs could not be
        /// started, saying which browser it was and what the driver
        /// reported. The old messages named the wrong browser, so somebody
        /// reading a skipped Firefox test was told to install the Edge
        /// driver, and the reason the driver refused was thrown away
        /// entirely. A skip nobody can act on is no better than a test that
        /// never ran.
        /// </summary>
        /// <param name="browserName">
        /// The browser the test needs, for example "Chrome".
        /// </param>
        /// <param name="exception">
        /// What the driver threw.
        /// </param>
        protected static void SkipBecauseBrowserUnavailable(
            string browserName,
            WebDriverException exception)
        {
            var message =
                $"Skipped because a {browserName} driver could not be " +
                $"started, so this test did not run. Install {browserName} " +
                "and let Selenium Manager fetch the matching driver, or " +
                "put the driver on the PATH. The driver reported: " +
                exception.Message;

            // Written to the console as well as carried on the result,
            // because the console logger shows only the word "Skipped" and
            // a person looking at a build needs the reason.
            Console.WriteLine(message);
            Assert.Inconclusive(message);
        }

        /// <summary>
        /// Attaches a cross browser network adapter to the driver using the
        /// W3C WebDriver BiDi protocol. This replaces the old Chrome DevTools
        /// Protocol path, which only Chromium browsers implemented and which
        /// threw for Firefox because it does not implement <c>IDevTools</c>.
        /// BiDi is supported by Chrome, Edge and Firefox alike, so every
        /// browser now gets real network inspection.
        /// </summary>
        /// <param name="driver">
        /// The driver, which must have been created with
        /// <c>UseWebSocketUrl = true</c> so a BiDi session can be established.
        /// </param>
        /// <returns>
        /// The adapter, or null if a BiDi session could not be established, in
        /// which case the tests that need it report themselves inconclusive
        /// rather than failing every test in the class.
        /// </returns>
        private static async Task<BiDiNetworkAdapter> GetNetwork(
            IWebDriver driver)
        {
            try
            {
                var bidi = await driver.AsBiDiAsync();
                var network = new BiDiNetworkAdapter(bidi);
                // 51Degrees.core.js is built from the evidence in the request,
                // cookies included, and served with max-age=1800 and no
                // Vary on Cookie. A browser that cached it while the evidence
                // was complete serves it again to the next test, which then
                // has nothing to fetch, so every test bypasses the cache.
                await network.BypassCacheAsync();
                return network;
            }
            catch (Exception)
            {
                // The driver could not expose a BiDi session, for example
                // because it was created without UseWebSocketUrl. Returning
                // null leaves the network dependent tests inconclusive rather
                // than failing the whole class at driver creation.
                return null;
            }
        }
    }
}
