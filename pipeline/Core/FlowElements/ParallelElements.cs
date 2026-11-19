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
using FiftyOne.Pipeline.Core.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// ParallelElements executes it's child <see cref="IFlowElement"/> 
    /// objects in parallel.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/advanced-features/parallel-processing.md">Specification</see>
    /// </summary>
    internal class ParallelElements : FlowElementBase<IElementData, IElementPropertyMetaData>
    {
        /// <summary>
        /// The element of a group that is running in this asynchronous
        /// flow, and the Flow Data it is processing, so that the Flow Data
        /// can refuse a value the element adds to its Evidence while the
        /// other elements of the group read it.
        /// </summary>
        internal static readonly AsyncLocal<Running> Current =
            new AsyncLocal<Running>();

        /// <summary>
        /// Refuses a value for the Evidence of the Flow Data that the
        /// running element of a group is processing, when that element adds
        /// it, and records a configuration error against the element. Both
        /// <see cref="FlowData.AddEvidence(string, object)"/> and a direct
        /// write to <see cref="Evidence"/> call this.
        /// </summary>
        /// <param name="evidence">The Evidence being written.</param>
        /// <param name="key">The key of the value being added.</param>
        /// <returns>True when the value is refused.</returns>
        internal static bool RefuseEvidence(IEvidence evidence, string key)
        {
            var running = Current.Value;
            if (running == null ||
                ReferenceEquals(running.Data.GetEvidence(), evidence) == false)
            {
                return false;
            }
            var suppress = running.Data.Pipeline?.SuppressProcessExceptions
                ?? false;
            running.Data.AddError(
                new PipelineConfigurationException(string.Format(
                    CultureInfo.InvariantCulture,
                    Messages.ExceptionParallelGroupAddedEvidence,
                    key)),
                running.Element,
                true,
                suppress == false);
            return true;
        }

        /// <summary>
        /// An element of a group and the Flow Data it is processing.
        /// </summary>
        internal sealed class Running
        {
            internal Running(IFlowData data, IFlowElement element)
            {
                Data = data;
                Element = element;
            }

            internal IFlowData Data { get; }

            internal IFlowElement Element { get; }
        }

        protected List<IFlowElement> _flowElements;

        /// <summary>
        /// Get a read only list of the child <see cref="IFlowElement"/> 
        /// instances.
        /// </summary>
        internal IReadOnlyList<IFlowElement> FlowElements
        {
            get
            {
                return new ReadOnlyCollection<IFlowElement>(_flowElements);
            }
        }

        /// <summary>
        /// A filter that will only include the evidence keys that can 
        /// be used by at least one <see cref="IFlowElement"/> within 
        /// this pipeline.
        /// (Will only be populated after the <see cref="EvidenceKeyFilter"/>
        /// property is used.)
        /// </summary>
        private EvidenceKeyFilterAggregator _evidenceKeyFilter;


        public override string ElementDataKey
        {
            get
            {
                // This is because ParallelElements instances cannot translate 
                // IFlowData to a single IElementData (because they contain 
                // multiple elements and each element could have it's own data)
                throw new NotImplementedException(
                    Messages.ExceptionParallelElementsNoDataKey);
            }
        }

        public override bool IsConcurrent => true;

        /// <summary>
        /// Get a filter that will only include the evidence keys that can 
        /// be used by at least one <see cref="IFlowElement"/> within 
        /// this pipeline.
        /// </summary>
        public override IEvidenceKeyFilter EvidenceKeyFilter
        {
            get
            {
                if (_evidenceKeyFilter == null)
                {
                    _evidenceKeyFilter = new EvidenceKeyFilterAggregator();
                    foreach (var filter in _flowElements.Select(e => e.EvidenceKeyFilter))
                    {
                        _evidenceKeyFilter.AddFilter(filter);
                    }
                }
                return _evidenceKeyFilter;
            }
        }

        public override IList<IElementPropertyMetaData> Properties => throw new NotImplementedException();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger to be used by this instance.
        /// </param>
        /// <param name="flowElements">
        /// The list of <see cref="IFlowElement"/> instances to execute
        /// when Process is called.
        /// </param>
        public ParallelElements(
            ILogger<ParallelElements> logger,
            params IFlowElement[] flowElements) : base(logger)
        {
            _flowElements = new List<IFlowElement>(flowElements);
        }

        /// <summary>
        /// Called by the Process method on the 
        /// <see cref="FlowElementBase{T, TMeta}"/> base class.
        /// Executes all child elements in parallel.
        /// </summary>
        /// <param name="data">
        /// The data to use when executing the flow elements.
        /// </param>
        protected override void ProcessInternal(IFlowData data)
        {
            List<Task> allTasks = new List<Task>();
            foreach (var element in _flowElements)
            {
                allTasks.Add(
                    // Run each element on a new thread.
                    Task.Run(() =>
                    {
                        var activity = ElementTracing.StartElement(element);
                        // Set inside the task, so it reaches only this
                        // element and what it calls.
                        Current.Value = new Running(data, element);
                        try
                        {
                            element.Process(data);
                        }
                        catch (Exception ex)
                        {
                            // Mark the span before the exception carries
                            // on into the error handling below.
                            activity?.SetStatus(
                                ActivityStatusCode.Error, ex.Message);
                            throw;
                        }
                        finally
                        {
                            Current.Value = null;
                            activity?.Dispose();
                        }
                    }).ContinueWith(t =>
                    {
                        // If any exceptions occurred then add them to the 
                        // flow data.
                        if (t.Exception != null)
                        {
                            // As in Pipeline.Process, error level logging is
                            // skipped when the pipeline suppresses process
                            // exceptions. The error remains available through
                            // IFlowData.Errors and is logged at debug level.
                            // Pipeline is never null on a flow data created by
                            // a pipeline, but stub implementations can leave it
                            // unset, so treat that as 'do not suppress'.
                            var suppress =
                                data.Pipeline?.SuppressProcessExceptions ?? false;
                            foreach (var innerException in t.Exception.InnerExceptions)
                            {
                                data.AddError(innerException, element, true, !suppress);
                                if (suppress && Logger.IsEnabled(LogLevel.Debug))
                                {
                                    Logger.LogDebug(innerException,
                                        "Suppressed error during processing of " +
                                        $"'{element?.GetType().Name}'.");
                                }
                            }
                        }
                    }, TaskScheduler.Default));
            }

            // Wait until all tasks have completed.
            Task.WhenAll(allTasks).Wait();
        }

        protected override void ManagedResourcesCleanup()
        {
            foreach (var element in _flowElements)
            {
                element.Dispose();
            }
            _flowElements = null;
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }
}
