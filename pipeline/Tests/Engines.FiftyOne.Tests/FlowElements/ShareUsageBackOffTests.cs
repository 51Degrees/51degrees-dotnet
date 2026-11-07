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

using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements
{
    /// <summary>
    /// The rules that decide when usage sharing may send after failed
    /// sends, with each test setting the time.
    /// </summary>
    [TestClass]
    public class ShareUsageBackOffTests
    {
        private static readonly DateTime Start =
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static DateTime At(double seconds) => Start.AddSeconds(seconds);

        private static TimeSpan Seconds(double seconds) =>
            TimeSpan.FromSeconds(seconds);

        /// <summary>
        /// Three failures within a ten second window start a two second
        /// pause, rising by two each time up to five.
        /// </summary>
        private static ShareUsageBackOff Create(
            int failuresToEnterRecovery = 3,
            double initialDelaySeconds = 2,
            double maxDelaySeconds = 5) =>
            new ShareUsageBackOff(
                failuresToEnterRecovery,
                10,
                initialDelaySeconds,
                maxDelaySeconds,
                2);

        [TestMethod]
        public void ShareUsageBackOff_PausesAfterFailuresWithinWindow()
        {
            var backOff = Create();

            Assert.AreEqual(TimeSpan.Zero, backOff.Failed(At(0)));
            Assert.AreEqual(TimeSpan.Zero, backOff.Failed(At(1)));
            Assert.AreEqual(TimeSpan.Zero, backOff.Remaining(At(1)),
                "Sending paused before enough sends failed.");
            Assert.IsFalse(backOff.BackingOff);

            Assert.AreEqual(Seconds(2), backOff.Failed(At(2)));
            Assert.IsTrue(backOff.BackingOff);
            Assert.AreEqual(Seconds(1), backOff.Remaining(At(3)));
            Assert.AreEqual(TimeSpan.Zero, backOff.Remaining(At(4)));
        }

        [TestMethod]
        public void ShareUsageBackOff_FailuresOutsideWindowDoNotCount()
        {
            var backOff = Create();

            backOff.Failed(At(0));
            backOff.Failed(At(1));

            Assert.AreEqual(TimeSpan.Zero, backOff.Failed(At(11.5)),
                "Failures older than the window were counted.");
            Assert.IsFalse(backOff.BackingOff);
        }

        /// <summary>
        /// The window counts failures, not failures in a row, so a high
        /// failure rate pauses sending even when some sends succeed.
        /// </summary>
        [TestMethod]
        public void ShareUsageBackOff_SuccessesBetweenFailuresDoNotReset()
        {
            var backOff = Create();

            backOff.Failed(At(0));
            Assert.IsFalse(backOff.Succeeded());
            backOff.Failed(At(1));
            Assert.IsFalse(backOff.Succeeded());

            Assert.AreEqual(Seconds(2), backOff.Failed(At(2)));
        }

        [TestMethod]
        public void ShareUsageBackOff_FailedSendAfterPauseMultipliesDelay()
        {
            var backOff = Create(failuresToEnterRecovery: 1);

            Assert.AreEqual(Seconds(2), backOff.Failed(At(0)));
            Assert.AreEqual(Seconds(4), backOff.Failed(At(2)));
            Assert.AreEqual(Seconds(5), backOff.Failed(At(6)),
                "The delay grew past the maximum.");
            Assert.AreEqual(Seconds(5), backOff.Failed(At(11)));
            Assert.AreEqual(Seconds(5), backOff.Remaining(At(11)));
        }

        [TestMethod]
        public void ShareUsageBackOff_SuccessEndsBackOff()
        {
            var backOff = Create(failuresToEnterRecovery: 2);
            backOff.Failed(At(0));
            backOff.Failed(At(1));
            backOff.Failed(At(3));

            Assert.IsTrue(backOff.Succeeded(),
                "A success did not end the back-off.");
            Assert.IsFalse(backOff.BackingOff);
            Assert.AreEqual(TimeSpan.Zero, backOff.Remaining(At(4)));

            // The next pause needs the full number of failures again, and
            // starts from the initial delay.
            Assert.AreEqual(TimeSpan.Zero, backOff.Failed(At(8)));
            Assert.AreEqual(Seconds(2), backOff.Failed(At(9)));
        }

        [TestMethod]
        public void ShareUsageBackOff_MaximumBelowInitialLimitsFirstPause()
        {
            var backOff = Create(1, 10, 3);

            Assert.AreEqual(Seconds(3), backOff.Failed(At(0)));
        }

        [TestMethod]
        [DataRow(0, 10, 2.0, 5.0, 2.0)]
        [DataRow(1, 0, 2.0, 5.0, 2.0)]
        [DataRow(1, 10, 0.0, 5.0, 2.0)]
        [DataRow(1, 10, double.NaN, 5.0, 2.0)]
        [DataRow(1, 10, 2.0, 0.0, 2.0)]
        [DataRow(1, 10, 2.0, 5.0, 0.5)]
        public void ShareUsageBackOff_InvalidSettingsAreRejected(
            int failuresToEnterRecovery,
            int failuresWindowSeconds,
            double initialDelaySeconds,
            double maxDelaySeconds,
            double multiplier)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            {
                _ = new ShareUsageBackOff(
                    failuresToEnterRecovery,
                    failuresWindowSeconds,
                    initialDelaySeconds,
                    maxDelaySeconds,
                    multiplier);
            });
        }
    }
}
