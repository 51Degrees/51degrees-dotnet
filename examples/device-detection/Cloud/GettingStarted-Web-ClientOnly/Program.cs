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

namespace FiftyOne.DeviceDetection.Examples.Cloud.GettingStartedWeb.ClientOnly
{
    public class Program
    {
        /// <summary>
        /// What the example writes when no resource key is set.
        /// </summary>
        private const string MissingKeyMessage =
            "No resource key was given in the environment variable " +
            $"'{ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR}'. The page asks the " +
            "51Degrees cloud service for its properties with that key. A " +
            "free resource key can be created at " +
            "https://51degrees.com/configure/Wkqxf3Bs?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-cloud-gettingstarted-web-clientonly-program.cs&utm_term=resource-key-required. " +
            "With a paid subscription, a key created at " +
            "https://51degrees.com/configure/hYzn3TV3?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-cloud-gettingstarted-web-clientonly-program.cs&utm_term=resource-key-required " +
            "includes every property the page shows.";

        public static void Main(string[] args)
        {
            // Start the server and then wait for the task to finish.
            Run(args).Wait();
        }

        /// <summary>
        /// Used by unit tests to run the example in an almost identical manner
        /// to a developer using the example. Returns the task that the web 
        /// server is running in so that the test can trigger the cancellation
        /// token and then wait for the server to shutdown before finishing.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="stopToken"></param>
        /// <returns></returns>
        public static Task Run(
            string[] args,
            CancellationToken stopToken = default)
        {
            if (string.IsNullOrWhiteSpace(ExampleUtils.GetResourceKeyFromEnv()))
            {
                // There is no resource key, so the message says where to
                // create one and the example ends without a server.
                Console.WriteLine(MissingKeyMessage);
                return Task.CompletedTask;
            }

            var builder = WebApplication.CreateBuilder(args);

            // Setup urls for the web server.
            builder.WebHost.UseUrls(Constants.AllUrls);

            // Add services to the container.
            builder.Services.AddRazorPages().AddRazorRuntimeCompilation();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment() == false)
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.MapRazorPages();

            return app.RunAsync(stopToken);
        }
    }
}