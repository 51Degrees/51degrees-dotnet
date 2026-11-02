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

using FiftyOne.Common.Services;
using FiftyOne.Common.TestHelpers;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Common.Tests
{
    [TestClass]
    public class LimitedHttpClientTests
    {
        private TestLoggerFactory _loggerFactory;

        /// <summary>
        /// Test handler class to simulate an HTTP service.
        /// </summary>
        private class TestHttpHandler : HttpMessageHandler
        {
            private readonly Func<string, Task<string>> _getResponse;

            public HttpClient Client => new HttpClient(this);

            public int Requests { get; private set; }

            public TestHttpHandler(Func<string, Task<string>> getResponse)
            {
                _getResponse = getResponse;
                Requests = 0;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Requests = Requests + 1;
                var response = await _getResponse(request.RequestUri?.ToString());
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                result.Content = new StringContent(response);
                return result;
            }
        }

        [TestInitialize]
        public void Init()
        {
            _loggerFactory = new TestLoggerFactory();
        }

        /// <summary>
        /// Check that a single request is passed to the underlying service
        /// correctly.
        /// </summary>
        /// <param name="method"></param>
        [DataRow("GET")]
        [DataRow("POST")]
        [TestMethod]
        public void SingleRequest(string method)
        {
            // Arrange
            var expectedResult = "some string...";
            var handler = new TestHttpHandler((s) => Task.FromResult(expectedResult));
            var limiter = new LimitedHttpClientWrapper(
                _loggerFactory.CreateLogger<LimitedHttpClientWrapper>(),
                handler.Client,
                1);

            // Act
            Task<HttpResponseMessage> result = null;
            switch (method)
            {
                case "GET":
                    result = limiter.GetAsync("http://51degrees.com", CancellationToken.None);
                    break;
                case "POST":
                    result = limiter.PostAsync("https://51degrees.com", null, CancellationToken.None);
                    break;
            }

            // Assert
            Assert.IsTrue(result.IsCompletedSuccessfully);
            Assert.IsNotNull(result.Result.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(expectedResult, result.Result.Content.ReadAsStringAsync().Result);
            AssertNoRemainingRequests(limiter);
        }

        /// <summary>
        /// Check that when an exception is thrown by the underlying client,
        /// that the count is still decremented. Otherwise the wrapper will
        /// eventually stop returning responses completely.
        /// </summary>
        /// <param name="method"></param>
        [DataRow("GET")]
        [DataRow("POST")]
        [TestMethod]
        public void FailedRequest(string method)
        {
            // Arrange
            var handler = new TestHttpHandler((s) =>
            {
                throw new Exception();
            });
            var limiter = new LimitedHttpClientWrapper(
                _loggerFactory.CreateLogger<LimitedHttpClientWrapper>(),
                handler.Client,
                1);

            // Act
            Task<HttpResponseMessage> result = null;
            switch (method)
            {
                case "GET":
                    result = limiter.GetAsync("http://51degrees.com", CancellationToken.None);
                    break;
                case "POST":
                    result = limiter.PostAsync("https://51degrees.com", null, CancellationToken.None);
                    break;
            }

            // Assert
            Assert.IsTrue(result.IsFaulted);
            AssertNoRemainingRequests(limiter);
        }

        /// <summary>
        /// Check that multiple requests are passed to the underlying service
        /// correctly when there are less than the specified number of max
        /// concurrent requests.
        /// </summary>
        /// <param name="method"></param>
        [DataRow("GET")]
        [DataRow("POST")]
        [TestMethod]
        public void MultipleRequests(string method)
        {
            // Arrange
            var expectedResult = "some string...";
            var handler = new TestHttpHandler((s) => Task.FromResult(expectedResult));
            var limiter = new LimitedHttpClientWrapper(
                _loggerFactory.CreateLogger<LimitedHttpClientWrapper>(),
                handler.Client,
                2);

            // Act
            Task<HttpResponseMessage> result1 = null;
            Task<HttpResponseMessage> result2 = null;
            switch (method)
            {
                case "GET":
                    result1 = limiter.GetAsync("http://51degrees.com", CancellationToken.None);
                    result2 = limiter.GetAsync("http://51degrees.com", CancellationToken.None);
                    break;
                case "POST":
                    result1 = limiter.PostAsync("https://51degrees.com", null, CancellationToken.None);
                    result2 = limiter.PostAsync("https://51degrees.com", null, CancellationToken.None);
                    break;
            }

            // Assert
            Assert.IsTrue(result1.IsCompletedSuccessfully);
            Assert.IsNotNull(result1.Result.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(expectedResult, result1.Result.Content.ReadAsStringAsync().Result);
            Assert.IsTrue(result2.IsCompletedSuccessfully);
            Assert.IsNotNull(result2.Result.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(expectedResult, result2.Result.Content.ReadAsStringAsync().Result);
            AssertNoRemainingRequests(limiter);
        }

        /// <summary>
        /// Check that requests over the limit of concurrent requests return
        /// null, and do not call the underlying service.
        /// This is done by setting the response function used by the mock
        /// service to loop until a token is set, keeping a request active
        /// while a second request is made.
        /// </summary>
        /// <param name="method"></param>
        [DataRow("GET")]
        [DataRow("POST")]
        [TestMethod]
        public void MultipleRequests_Limited(string method)
        {
            // Arrange
            var expectedResult = "some string...";
            var token = new CancellationTokenSource();
            var handler = new TestHttpHandler((s) => Task.Run(() =>
            {
                while (token.Token.IsCancellationRequested == false)
                {
                    Task.Delay(1);
                }
                return expectedResult;
            }));
            var limiter = new LimitedHttpClientWrapper(
                _loggerFactory.CreateLogger<LimitedHttpClientWrapper>(),
                handler.Client,
                1);

            // Act
            Task<HttpResponseMessage> result1 = null;
            Task<HttpResponseMessage> result2 = null;
            switch (method)
            {
                case "GET":
                    result1 = limiter.GetAsync("http://51degrees.com", CancellationToken.None);
                    // Make the second request while the first is still in
                    // progress, then complete the first.
                    result2 = limiter.GetAsync("http://51degrees.com", CancellationToken.None);
                    break;
                case "POST":
                    result1 = limiter.PostAsync("http://51degrees.com", null, CancellationToken.None);
                    // Make the second request while the first is still in
                    // progress, then complete the first.
                    result2 = limiter.PostAsync("http://51degrees.com", null, CancellationToken.None);
                    break;
            }

            token.Cancel();

            // Assert
            result1.Wait();
            Assert.IsTrue(result1.IsCompletedSuccessfully);
            Assert.IsNotNull(result1.Result.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(expectedResult, result1.Result.Content.ReadAsStringAsync().Result);
            Assert.IsTrue(result2.IsCompletedSuccessfully);
            Assert.IsNull(result2.Result);
            AssertNoRemainingRequests(limiter);
            Assert.AreEqual(1, handler.Requests);
        }

        /// <summary>
        /// Check that the correct responses are returned for each request.
        /// This ensures that tasks are not mixed up somehow.
        /// </summary>
        /// <param name="method"></param>
        /// <exception cref="Exception"></exception>
        [DataRow("GET")]
        [DataRow("POST")]
        [TestMethod]
        public void MultipleRequests_DifferentResults(string method)
        {
            // Arrange
            var expectedResult1 = "some string...";
            var expectedResult2 = "some other string...";
            var handler = new TestHttpHandler((s) =>
            {
                if (s.EndsWith("1"))
                {
                    return Task.FromResult(expectedResult1);
                }
                else if (s.EndsWith("2"))
                {
                    return Task.FromResult(expectedResult2);
                }
                else { throw new Exception(); }
            });
            var limiter = new LimitedHttpClientWrapper(
                _loggerFactory.CreateLogger<LimitedHttpClientWrapper>(),
                handler.Client,
                2);

            // Act
            Task<HttpResponseMessage> result1 = null;
            Task<HttpResponseMessage> result2 = null;
            switch (method)
            {
                case "GET":
                    result1 = limiter.GetAsync("http://51degrees.com/1", CancellationToken.None);
                    result2 = limiter.GetAsync("http://51degrees.com/2", CancellationToken.None);
                    break;
                case "POST":
                    result1 = limiter.PostAsync("https://51degrees.com/1", null, CancellationToken.None);
                    result2 = limiter.PostAsync("https://51degrees.com/2", null, CancellationToken.None);
                    break;
            }

            // Assert
            result1.Wait();
            Assert.IsTrue(result1.IsCompletedSuccessfully);
            Assert.IsNotNull(result1.Result.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(expectedResult1, result1.Result.Content.ReadAsStringAsync().Result);
            Assert.IsTrue(result2.IsCompletedSuccessfully);
            Assert.IsNotNull(result2.Result.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(expectedResult2, result2.Result.Content.ReadAsStringAsync().Result);
            AssertNoRemainingRequests(limiter);
            Assert.AreEqual(2, handler.Requests);
        }

        /// <summary>
        /// Check that requests made after the limit has been exceeded, then
        /// cleared down below the limit, call the underlying service
        /// correctly.
        /// This is done by setting the response function used by the mock
        /// service to loop until a token is set, keeping a request active
        /// while a second request is made. The token is then set so that the
        /// first request can complete, then a third request is made.
        /// Outcome should be:
        /// request 1 - succeed
        /// request 2 - fail
        /// request 3 - succeed
        /// </summary>
        /// <param name="method"></param>
        [DataRow("GET")]
        [DataRow("POST")]
        [TestMethod]
        public void MultipleRequests_PreviouslyLimited(string method)
        {
            // Arrange
            var expectedResult = "some string...";
            var token = new CancellationTokenSource();
            var handler = new TestHttpHandler((s) => Task.Run(() =>
            {
                while (token.Token.IsCancellationRequested == false)
                {
                    Task.Delay(1);
                }
                return expectedResult;
            }));
            var limiter = new LimitedHttpClientWrapper(
                _loggerFactory.CreateLogger<LimitedHttpClientWrapper>(),
                handler.Client,
                1);

            // Act
            Task<HttpResponseMessage> result1 = null;
            Task<HttpResponseMessage> result2 = null;
            Task<HttpResponseMessage> result3 = null;
            switch (method)
            {
                case "GET":
                    result1 = limiter.GetAsync("http://51degrees.com", CancellationToken.None);
                    // Make the second request while the first is still in
                    // progress, then complete the first.
                    result2 = limiter.GetAsync("http://51degrees.com", CancellationToken.None);
                    token.Cancel();
                    Thread.Sleep(10);
                    result3 = limiter.GetAsync("http://51degrees.com", CancellationToken.None);
                    break;
                case "POST":
                    result1 = limiter.PostAsync("http://51degrees.com", null, CancellationToken.None);
                    // Make the second request while the first is still in
                    // progress, then complete the first.
                    result2 = limiter.PostAsync("http://51degrees.com", null, CancellationToken.None);
                    token.Cancel();
                    Thread.Sleep(10);
                    result3 = limiter.PostAsync("http://51degrees.com", null, CancellationToken.None);
                    break;
            }

            // Assert
            result3.Wait();
            Assert.IsTrue(result1.IsCompletedSuccessfully);
            Assert.IsNotNull(result1.Result.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(expectedResult, result1.Result.Content.ReadAsStringAsync().Result);
            Assert.IsTrue(result2.IsCompletedSuccessfully);
            Assert.IsNull(result2.Result);
            Assert.IsTrue(result3.IsCompletedSuccessfully);
            Assert.IsNotNull(result3.Result.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(expectedResult, result3.Result.Content.ReadAsStringAsync().Result);
            AssertNoRemainingRequests(limiter);
            Assert.AreEqual(2, handler.Requests);
        }


        /// <summary>
        /// Asserts that there are no current requests in the serivce.
        /// Retries 10 times over 10ms to ensure the "Continue" tasks
        /// have also completed.
        /// </summary>
        /// <param name="limiter"></param>
        private void AssertNoRemainingRequests(LimitedHttpClientWrapper limiter)
        {
            int count = 0;
            while (count < 10 && limiter.CurrentRequests > 0)
            {
                count++;
                Thread.Sleep(1);
            }
            Assert.AreEqual(0, limiter.CurrentRequests);
        }
    }
}