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

using FiftyOne.Pipeline.Core.FlowElements;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace FiftyOne.Pipeline.Core.Tests.HelperClasses;

/// <summary>
/// Builds a <see cref="MultiplyByElement"/> and keeps it, so that a test
/// can check whether a pipeline build that failed disposed it.
/// </summary>
public class KeptElementBuilder
{
    /// <summary>
    /// Every element built since <see cref="Reset"/>.
    /// </summary>
    public static ConcurrentQueue<IFlowElement> Built { get; } = new();

    /// <summary>
    /// Set once an element has been built.
    /// </summary>
    public static ManualResetEventSlim AnyBuilt { get; } = new(false);

    /// <summary>
    /// Forgets the elements built by an earlier test.
    /// </summary>
    public static void Reset()
    {
        Built.Clear();
        AnyBuilt.Reset();
    }

    /// <summary>
    /// Builds and keeps a new element.
    /// </summary>
    /// <returns>The new element.</returns>
    public IFlowElement Build()
    {
        var element = new MultiplyByElement(2);
        Built.Enqueue(element);
        AnyBuilt.Set();
        return element;
    }
}

/// <summary>
/// Fails to build once a <see cref="KeptElementBuilder"/> has built an
/// element, whether the pipeline builder makes its elements one at a time
/// or in parallel.
/// </summary>
public class FailingElementBuilder
{
    /// <summary>
    /// How long to wait for the element before this one to be built.
    /// </summary>
    private const int WaitForBuiltSeconds = 10;

    /// <summary>
    /// Always throws.
    /// </summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="InvalidOperationException">Always.</exception>
    public IFlowElement Build()
    {
        KeptElementBuilder.AnyBuilt.Wait(
            TimeSpan.FromSeconds(WaitForBuiltSeconds));
        throw new InvalidOperationException(
            "This element always fails to build.");
    }
}
