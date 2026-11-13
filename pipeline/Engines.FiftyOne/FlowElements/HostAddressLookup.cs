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

using System;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Runs a lookup of the machine's own address once, on the thread pool,
    /// so that a caller waits for it no longer than it chooses. A lookup
    /// that fails gives an empty string.
    /// </summary>
    internal sealed class HostAddressLookup
    {
        private readonly Lazy<Task<string>> _address;

        /// <param name="lookUp">
        /// Returns the address. It runs at most once.
        /// </param>
        internal HostAddressLookup(Func<string> lookUp)
        {
            _address = new Lazy<Task<string>>(() => Task.Run(() =>
            {
                try
                {
                    return lookUp() ?? string.Empty;
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }));
        }

        /// <summary>
        /// Starts the lookup if it has not started.
        /// </summary>
        internal void Start()
        {
            _ = _address.Value;
        }

        /// <summary>
        /// Returns the address, starting the lookup if it has not started.
        /// </summary>
        /// <param name="waitMilliseconds">
        /// The longest to wait for a lookup that has not finished.
        /// </param>
        /// <returns>
        /// The address, or an empty string if the lookup failed or did not
        /// finish within the wait.
        /// </returns>
        internal string Get(int waitMilliseconds)
        {
            var address = _address.Value;
            return address.Wait(waitMilliseconds) ?
                address.Result :
                string.Empty;
        }
    }
}
