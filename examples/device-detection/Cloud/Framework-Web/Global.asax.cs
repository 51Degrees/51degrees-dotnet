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

using FiftyOne.DeviceDetection.Cloud.FlowElements;
using FiftyOne.DeviceDetection.Examples;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using System;
using System.IO;
using System.Web;

namespace Framework_Web
{
    public class Global : HttpApplication
    {
        /// <summary>
        /// What the options file holds in place of a resource key until one
        /// is entered.
        /// </summary>
        private const string KeyPlaceholder = "!!ENTER_YOUR_RESOURCE_KEY_HERE!!";

        /// <summary>
        /// The options the site runs with when no resource key is set. They
        /// leave out the cloud engines, so the site starts and the page says
        /// how to get a key.
        /// </summary>
        private const string OptionsWithoutKey =
            "{ \"PipelineOptions\": { \"Elements\": [] } }";

        /// <summary>
        /// True when neither the options file nor the environment holds a
        /// resource key.
        /// </summary>
        public static bool ResourceKeyMissing { get; private set; }

        void Application_Start(object sender, EventArgs e)
        {
            UseResourceKey(HttpContext.Current.Server.MapPath("~/App_Data"));

            // Make sure the assemblies that are needed by the pipeline are loaded into the
            // app domain.
            // This is needed in order from BuildFromConfiguration to be able to find the
            // relevant builder types when using reflection.
            AppDomain.CurrentDomain.Load(typeof(CloudRequestEngine).Assembly.GetName());
            AppDomain.CurrentDomain.Load(typeof(DeviceDetectionCloudEngine).Assembly.GetName());
            AppDomain.CurrentDomain.Load(typeof(JavaScriptBuilderElement).Assembly.GetName());
            AppDomain.CurrentDomain.Load(typeof(JsonBuilderElement).Assembly.GetName());
            AppDomain.CurrentDomain.Load(typeof(SequenceElementBuilder).Assembly.GetName());
        }

        /// <summary>
        /// Uses the resource key in the options file when one has been
        /// entered there, or else the one in the environment variable
        /// named by <see cref="ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR"/>.
        /// The integration reads its options from one folder, so for a key
        /// from the environment, or no key at all, the options are written
        /// to a folder of the site's own under the temporary folder and the
        /// integration reads them from there.
        /// </summary>
        private static void UseResourceKey(string appData)
        {
            var options = File.ReadAllText(Path.Combine(appData, "51Degrees.json"));
            if (options.Contains(KeyPlaceholder) == false)
            {
                return;
            }
            var key = ExampleUtils.GetResourceKeyFromEnv();
            ResourceKeyMissing = string.IsNullOrWhiteSpace(key);
            var folder = Path.Combine(
                Path.GetTempPath(),
                "51Degrees",
                "DeviceDetection-Cloud-Framework-Web");
            Directory.CreateDirectory(folder);
            File.WriteAllText(
                Path.Combine(folder, "51Degrees.json"),
                ResourceKeyMissing ?
                    OptionsWithoutKey :
                    options.Replace(KeyPlaceholder, key));
            FiftyOne.Pipeline.Web.Framework.Configuration.Extensions
                .BaseDirectory = () => folder;
        }
    }
}