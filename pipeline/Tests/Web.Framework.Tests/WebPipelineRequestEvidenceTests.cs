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

using FiftyOne.Pipeline.Core.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Hosting;

namespace FiftyOne.Pipeline.Web.Framework.Tests
{
    /// <summary>
    /// How the .NET Framework integration turns a System.Web request into
    /// evidence. Each request is read by System.Web from a worker request,
    /// which is how IIS hands one over, so the integration reads the
    /// address, the query string, the form and the headers as it reads a
    /// real request's.
    /// </summary>
    [TestClass]
    public class WebPipelineRequestEvidenceTests
    {
        /// <summary>
        /// A request as IIS hands it to System.Web.
        /// </summary>
        private sealed class WorkerRequest : SimpleWorkerRequest
        {
            private readonly string _verb;
            private readonly Dictionary<int, string> _known;
            private readonly string[][] _unknown;
            private readonly string _remoteAddress;
            private readonly bool _secure;
            private readonly byte[] _body;

            public WorkerRequest(
                string verb = "GET",
                string query = "",
                Dictionary<int, string> known = null,
                string[][] unknown = null,
                string remoteAddress = "1.2.3.4",
                bool secure = false,
                byte[] body = null)
                : base(
                    "/",
                    AppDomain.CurrentDomain.BaseDirectory,
                    "page",
                    query,
                    TextWriter.Null)
            {
                _verb = verb;
                _known = known ?? new Dictionary<int, string>();
                _unknown = unknown ?? new string[0][];
                _remoteAddress = remoteAddress;
                _secure = secure;
                _body = body ?? new byte[0];
                if (_body.Length > 0)
                {
                    _known[HeaderContentLength] =
                        _body.Length.ToString(
                            System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            public override string GetHttpVerbName() => _verb;

            public override string GetKnownRequestHeader(int index) =>
                _known.TryGetValue(index, out var value) ? value : null;

            public override string[][] GetUnknownRequestHeaders() =>
                _unknown;

            public override string GetRemoteAddress() => _remoteAddress;

            public override bool IsSecure() => _secure;

            public override byte[] GetPreloadedEntityBody() => _body;

            public override int GetPreloadedEntityBodyLength() =>
                _body.Length;

            public override int GetTotalEntityBodyLength() => _body.Length;

            public override bool IsEntireEntityBodyIsPreloaded() => true;
        }

        /// <summary>
        /// A flow data whose pipeline accepts every key, so every value the
        /// integration finds is offered to it.
        /// </summary>
        private static IFlowData AcceptingEverything()
        {
            var filter = Substitute.For<IEvidenceKeyFilter>();
            filter.Include(Arg.Any<string>()).Returns(true);
            var data = Substitute.For<IFlowData>();
            data.EvidenceKeyFilter.Returns(filter);
            return data;
        }

        /// <summary>
        /// A request with no client address adds no client IP, rather than
        /// adding the key with no value.
        /// </summary>
        [TestMethod]
        public void ClientIp_NoAddress_NotAdded()
        {
            var data = AcceptingEverything();
            var request = new HttpContext(
                new WorkerRequest(remoteAddress: null)).Request;

            WebPipeline.AddEvidenceFromRequest(data, request);

            data.DidNotReceive().AddEvidence(
                Core.Constants.EVIDENCE_CLIENTIP_KEY, Arg.Any<object>());
        }

        /// <summary>
        /// A request's client address is added as the client IP.
        /// </summary>
        [TestMethod]
        public void ClientIp_Address_Added()
        {
            var data = AcceptingEverything();
            var request = new HttpContext(
                new WorkerRequest(remoteAddress: "1.2.3.4")).Request;

            WebPipeline.AddEvidenceFromRequest(data, request);

            data.Received(1).AddEvidence(
                Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4");
        }

        /// <summary>
        /// A query string parameter written with no '=' is a name with an
        /// empty value, as the URL standard parses it and as ASP.NET Core
        /// gives it, and not a key with an empty name.
        /// </summary>
        [TestMethod]
        public void Query_NameWithNoValue_AddedWithAnEmptyValue()
        {
            var data = AcceptingEverything();
            var request = new HttpContext(
                new WorkerRequest(query: "flag&other&name=value")).Request;

            WebPipeline.AddEvidenceFromRequest(data, request);

            data.Received(1).AddEvidence("query.flag", string.Empty);
            data.Received(1).AddEvidence("query.other", string.Empty);
            data.Received(1).AddEvidence("query.name", "value");
            data.DidNotReceive().AddEvidence("query.", Arg.Any<object>());
        }

        /// <summary>
        /// A form whose content type carries a parameter, such as a
        /// character set, is still read, including a field written with no
        /// '='.
        /// </summary>
        [TestMethod]
        public void Form_ContentTypeWithParameter_Read()
        {
            var data = AcceptingEverything();
            var request = new HttpContext(new WorkerRequest(
                verb: "POST",
                known: new Dictionary<int, string>
                {
                    {
                        HttpWorkerRequest.HeaderContentType,
                        "application/x-www-form-urlencoded; charset=utf-8"
                    },
                },
                body: Encoding.UTF8.GetBytes("field=value&bare"))).Request;

            WebPipeline.AddEvidenceFromRequest(data, request);

            data.Received(1).AddEvidence("query.field", "value");
            data.Received(1).AddEvidence("query.bare", string.Empty);
        }

        /// <summary>
        /// The protocol comes from the request when it is secure, then
        /// from X-Origin-Proto, then from X-Forwarded-Proto, and is http
        /// otherwise. Each header holds a different value, so the value
        /// added shows which one was read.
        /// </summary>
        [DataTestMethod]
        [DataRow(true, "origin-value", "forwarded-value", "https")]
        [DataRow(false, "origin-value", "forwarded-value", "origin-value")]
        [DataRow(false, null, "forwarded-value", "forwarded-value")]
        [DataRow(false, null, null, "http")]
        public void Protocol_Source(
            bool secure,
            string originProto,
            string forwardedProto,
            string expected)
        {
            var unknown = new List<string[]>();
            if (originProto != null)
            {
                unknown.Add(new[] { "X-Origin-Proto", originProto });
            }
            if (forwardedProto != null)
            {
                unknown.Add(new[] { "X-Forwarded-Proto", forwardedProto });
            }
            var data = AcceptingEverything();
            var request = new HttpContext(new WorkerRequest(
                secure: secure,
                unknown: unknown.ToArray())).Request;

            var filler = new WebPipeline.EvidenceFiller(data);
            filler.AddRequestProtocolToEvidence(request);

            data.Received(1).AddEvidence(
                Core.Constants.EVIDENCE_PROTOCOL, expected);
        }

        /// <summary>
        /// Where no element asks for the protocol, it is not added.
        /// </summary>
        [TestMethod]
        public void Protocol_NotRequired_NotAdded()
        {
            var filter = Substitute.For<IEvidenceKeyFilter>();
            filter.Include(Arg.Any<string>()).Returns(false);
            var data = Substitute.For<IFlowData>();
            data.EvidenceKeyFilter.Returns(filter);
            var request = new HttpContext(new WorkerRequest(
                unknown: new[] { new[] { "X-Forwarded-Proto", "https" } }))
                .Request;

            var filler = new WebPipeline.EvidenceFiller(data);
            filler.AddRequestProtocolToEvidence(request);

            data.DidNotReceive().AddEvidence(
                Core.Constants.EVIDENCE_PROTOCOL, Arg.Any<object>());
        }
    }
}
