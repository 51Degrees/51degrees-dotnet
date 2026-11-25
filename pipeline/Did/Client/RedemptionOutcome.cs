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

namespace FiftyOne.Did.Client
{
    /// <summary>
    /// What happened to a sealed result at redemption, mapped from the
    /// <c>status</c> string in the redeem endpoint's answer. Only
    /// <see cref="Success"/> carries an unsealed result. The other four
    /// say why there is none this time.
    /// <para>
    /// <see cref="Unreadable"/> is the default value, so an outcome that
    /// was never set never reads as a success.
    /// </para>
    /// </summary>
    public enum RedemptionOutcome
    {
        /// <summary>
        /// The sealed result could not be read. Every cryptographic failure
        /// (tampered, made for another identifier or transaction, sealed
        /// under a key the service does not hold, or presented without a
        /// licence key the account requires) gives this one answer by
        /// design, so nothing finer is available. A <c>status</c> string
        /// this client does not recognise also maps here, with the raw
        /// string kept in <see cref="RedeemResult.StatusValue"/>.
        /// </summary>
        Unreadable = 0,

        /// <summary>
        /// The sealed result was redeemed. <see cref="RedeemResult.Context"/>
        /// holds the creator context verdict, and
        /// <see cref="RedeemResult.Signature"/> the signature outcome.
        /// </summary>
        Success = 1,

        /// <summary>
        /// The sealed result was presented after the service's freshness
        /// window closed. <see cref="RedeemResult.VerifiedAt"/> says when
        /// the verification happened.
        /// </summary>
        Expired = 2,

        /// <summary>
        /// The sealed result had already been redeemed on this service
        /// instance.
        /// </summary>
        Replayed = 3,

        /// <summary>
        /// The service could not confirm first use of the sealed result and
        /// answered 503. The caller may retry.
        /// </summary>
        Unconfirmed = 4,
    }
}
