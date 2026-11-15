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

using FiftyOne.IpIntelligence.TestHelpers;
using FiftyOne.IpIntelligence.TestHelpers.Data;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace FiftyOne.IpIntelligence.OnPremise.Tests.Core.Data;

/// <summary>
/// Checks the reload test in <see cref="MetaDataTests"/> itself, with
/// hashing that takes no time and an engine that does nothing, so no
/// data file is needed.
/// </summary>
[TestClass]
public class ReloadTestHelperTests
{
    /// <summary>
    /// Hashes nothing, so each hashing task finishes at once, as on a
    /// fast machine with a fast profile.
    /// </summary>
    private sealed class InstantHasher : IMetaDataHasher
    {
        public int HashProperties(int hash, IWrapper wrapper) => hash;

        public int HashComponents(int hash, IWrapper wrapper) => hash;

        public int HashProfiles(int hash, IWrapper wrapper) => hash;
    }

    /// <summary>
    /// A refresh runs while the hashing tasks are active even when each
    /// one finishes at once, so the reload test always tests what it is
    /// for, whatever the machine.
    /// </summary>
    [TestMethod]
    public void Reload_HashingTakesNoTime_StillRefreshes()
    {
        var engine = new Mock<IFiftyOneAspectEngine>();
        var wrapper = new Mock<IWrapper>();
        wrapper.Setup(w => w.GetEngine()).Returns(engine.Object);

        new MetaDataTests().Reload(
            wrapper.Object,
            new InstantHasher(),
            PerformanceProfiles.MaxPerformance);

        engine.Verify(e => e.RefreshData(null), Times.AtLeastOnce());
    }
}
