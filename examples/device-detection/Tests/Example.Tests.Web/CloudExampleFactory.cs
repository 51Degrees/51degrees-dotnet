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

using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Web;

/// <summary>
/// The web application factory for a cloud example. The example reads its
/// resource key from the environment, when its host is built and, for some
/// examples, on each request, so the key from
/// <see cref="ExampleResourceKey.Name"/> stays in the example's variable
/// until the factory is disposed.
/// </summary>
/// <typeparam name="T">The example's entry point.</typeparam>
public class CloudExampleFactory<T> : WebApplicationFactory<T>
    where T : class
{
    private readonly ExampleResourceKey _key = ExampleResourceKey.Set();

    public CloudExampleFactory()
    {
        if (_key == null)
        {
            throw new InvalidOperationException(ExampleResourceKey.NoKey);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _key?.Dispose();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _key?.Dispose();
    }
}
