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
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using System;
using System.Diagnostics;
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
        /// How long the browser may take to load a page before the driver
        /// reports a page load timeout. It must be shorter than the 60
        /// seconds the Selenium client waits for any command. geckodriver's
        /// own default is 300 seconds, so in run 44756128766 the client gave
        /// up on a navigation after 60 seconds while geckodriver went on
        /// waiting, and the next test's commands queued behind it: that
        /// test, which skips at once on Firefox, took 30 s instead of the
        /// 2 to 3 s the same test took in run 44697200170. With this limit
        /// geckodriver ends the load itself and answers. Measured: the
        /// Firefox tests that open /static.html took 2 to 3 s in all,
        /// server start and reset included, and the example server served
        /// the page in 1.5 s. 30 s is ten times the slowest of those. The
        /// "Build log: Selenium ... navigation" lines record every load so
        /// the limit can be checked against each run.
        /// </summary>
        protected static readonly TimeSpan PAGE_LOAD_TIMEOUT =
            TimeSpan.FromSeconds(30);

        /// <summary>
        /// Opens a page and writes how long it took to the build log, or
        /// how long it ran before it failed and what the page had reached.
        /// </summary>
        /// <param name="url">
        /// The page to open.
        /// </param>
        /// <param name="step">
        /// What the navigation is for, written in the build log line.
        /// </param>
        protected static void NavigateTo(string url, string step)
        {
            var loading = Stopwatch.StartNew();
            try
            {
                Driver.Navigate().GoToUrl(url);
            }
            catch (WebDriverException exception)
            {
                Console.WriteLine(
                    $"Build log: Selenium {BrowserName} {step}, the " +
                    $"navigation to {url} failed after " +
                    $"{loading.ElapsedMilliseconds} ms " +
                    $"({exception.GetType().Name}), page state: " +
                    $"{ReadPageState()}.");
                throw;
            }
            Console.WriteLine(
                $"Build log: Selenium {BrowserName} {step}, the navigation " +
                $"to {url} took {loading.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Writes how long a wait in a test took to the build log.
        /// </summary>
        /// <param name="step">
        /// What was waited for.
        /// </param>
        /// <param name="waiting">
        /// Started when the wait began.
        /// </param>
        /// <param name="outcome">
        /// How the wait ended, for example "done" or "timed out".
        /// </param>
        protected static void LogWait(
            string step,
            Stopwatch waiting,
            string outcome)
        {
            Console.WriteLine(
                $"Build log: Selenium {BrowserName} {step}, the wait " +
                $"{outcome} after {waiting.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Reads how far the page got and which resources it was still
        /// loading, so a navigation that fails says what it was waiting
        /// for. A diagnostic only, so it never throws.
        /// </summary>
        private static string ReadPageState()
        {
            try
            {
                return (string)((IJavaScriptExecutor)Driver).ExecuteScript(@"
                    var entries = performance.getEntriesByType('resource')
                        .map(function (e) {
                            return e.name + ' ' + Math.round(e.duration) +
                                ' ms' + (e.responseEnd > 0 ? '' : ' (open)');
                        });
                    return 'readyState ' + document.readyState +
                        ', resources [' + entries.join('; ') + ']';");
            }
            catch (Exception exception)
            {
                return $"unavailable: {exception.Message}";
            }
        }

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
            ServerTask = StartServerFunc(StopSource.Token);
            ResetBrowserState();
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
            var resetting = Stopwatch.StartNew();
            NavigateTo("about:blank", "reset");
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
            var switching = Stopwatch.StartNew();
            var previous = Driver.CurrentWindowHandle;
            Driver.SwitchTo().NewWindow(WindowType.Tab);
            var fresh = Driver.CurrentWindowHandle;
            Driver.SwitchTo().Window(previous);
            Driver.Close();
            Driver.SwitchTo().Window(fresh);
            Console.WriteLine(
                $"Build log: Selenium {BrowserName} reset, the new tab took " +
                $"{switching.ElapsedMilliseconds} ms and the whole reset " +
                $"{resetting.ElapsedMilliseconds} ms.");
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
                Driver = new ChromeDriver(chromeOptions);
            }
            catch (WebDriverException exception)
            {
                SkipBecauseBrowserUnavailable("Chrome", exception);
            }
            BrowserName = "Chrome";
            Network = GetNetwork(Driver).Result;
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
                Driver = new EdgeDriver(edgeOptions);
            }
            catch (WebDriverException exception)
            {
                SkipBecauseBrowserUnavailable("Edge", exception);
            }
            BrowserName = "Edge";
            Network = GetNetwork(Driver).Result;
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
            // Return from a navigation once the document is parsed, not
            // when every resource has loaded. The client-only pages load
            // the 51Degrees cloud script with async, which still holds back
            // the load event, so a slow cloud response holds the navigation.
            // In run 44756128766 a navigation to /static.html did not return
            // within the client's 60 seconds, although the example server
            // had sent the page in 1.5 s. No test needs the load event:
            // each waits for the thing it checks, such as the high entropy
            // values, the cookie, the cloud JSON response or the
            // 'complete' value, within TEST_TIMEOUT.
            firefoxOptions.PageLoadStrategy = PageLoadStrategy.Eager;
            try
            {
                Driver = new FirefoxDriver(firefoxOptions);
            }
            catch (WebDriverException exception)
            {
                SkipBecauseBrowserUnavailable("Firefox", exception);
            }
            Driver.Manage().Timeouts().PageLoad = PAGE_LOAD_TIMEOUT;
            BrowserName = "Firefox";
            Network = GetNetwork(Driver).Result;
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
                // Subscribed here, once, while the browser has one tab and
                // no page, rather than by the test after a reset has just
                // closed a tab. A failure here leaves cookies usable and
                // only the tests that need responses inconclusive.
                var subscribing = Stopwatch.StartNew();
                try
                {
                    await network.StartRecordingResponsesAsync();
                    Console.WriteLine(
                        $"Build log: Selenium {BrowserName} set-up, the " +
                        "BiDi response subscription took " +
                        $"{subscribing.ElapsedMilliseconds} ms.");
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        $"Build log: Selenium {BrowserName} set-up, the " +
                        "BiDi response subscription failed after " +
                        $"{subscribing.ElapsedMilliseconds} ms " +
                        $"({exception.GetType().Name}).");
                }
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
