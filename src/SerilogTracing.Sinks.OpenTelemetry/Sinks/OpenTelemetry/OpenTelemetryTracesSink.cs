﻿// Copyright 2022 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Trace.V1;
using Serilog.Core;
using Serilog.Events;
using SerilogTracing.Sinks.OpenTelemetry.ProtocolHelpers;
using Serilog.Sinks.PeriodicBatching;

namespace SerilogTracing.Sinks.OpenTelemetry;

class OpenTelemetryTracesSink : IBatchedLogEventSink, ILogEventSink
{
    readonly IFormatProvider? _formatProvider;
    readonly ResourceSpans _resourceSpansTemplate;
    readonly IExporter _exporter;
    readonly IncludedData _includedData;

    public OpenTelemetryTracesSink(
        IExporter exporter,
        IFormatProvider? formatProvider,
        IReadOnlyDictionary<string, object> resourceAttributes,
        IncludedData includedData)
    {
        _exporter = exporter;
        _formatProvider = formatProvider;
        _includedData = includedData;

        if ((includedData & IncludedData.SpecRequiredResourceAttributes) == IncludedData.SpecRequiredResourceAttributes)
        {
            resourceAttributes = RequiredResourceAttributes.AddDefaults(resourceAttributes);
        }

        _resourceSpansTemplate = RequestTemplateFactory.CreateResourceSpans(resourceAttributes);
    }

    /// <summary>
    /// Transforms and sends the given batch of LogEvent objects
    /// to an OTLP endpoint.
    /// </summary>
    public Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        var resourceSpans = _resourceSpansTemplate.Clone();
        var traceAnonymousScope = (ScopeSpans?)null;
        var spansNamedScopes = (Dictionary<string, ScopeSpans>?)null;

        foreach (var logEvent in batch)
        {
            var (span, scopeName) = OtlpEventBuilder.ToSpan(logEvent, _formatProvider, _includedData);
            if (scopeName == null)
            {
                if (traceAnonymousScope == null)
                {
                    traceAnonymousScope = RequestTemplateFactory.CreateScopeSpans(null);
                    resourceSpans.ScopeSpans.Add(traceAnonymousScope);
                }
            
                traceAnonymousScope.Spans.Add(span);
            }
            else
            {
                spansNamedScopes ??= new Dictionary<string, ScopeSpans>();
                if (!spansNamedScopes.TryGetValue(scopeName, out var namedScope))
                {
                    namedScope = RequestTemplateFactory.CreateScopeSpans(scopeName);
                    spansNamedScopes.Add(scopeName, namedScope);
                    resourceSpans.ScopeSpans.Add(namedScope);
                }
            
                namedScope.Spans.Add(span);
            }
        }

        var spansRequest = new ExportTraceServiceRequest();
        spansRequest.ResourceSpans.Add(resourceSpans);
        return _exporter.ExportAsync(spansRequest);
    }

    /// <summary>
    /// Transforms and sends the given LogEvent
    /// to an OTLP endpoint.
    /// </summary>
    public void Emit(LogEvent logEvent)
    {
        var (span, scopeName) = OtlpEventBuilder.ToSpan(logEvent, _formatProvider, _includedData);
        var scopeSpans = RequestTemplateFactory.CreateScopeSpans(scopeName);
        scopeSpans.Spans.Add(span);
        var resourceSpans = _resourceSpansTemplate.Clone();
        resourceSpans.ScopeSpans.Add(scopeSpans);
        var request = new ExportTraceServiceRequest();
        request.ResourceSpans.Add(resourceSpans);
        _exporter.Export(request);
    }

    /// <summary>
    /// A no-op for an empty batch.
    /// </summary>
    public Task OnEmptyBatchAsync()
    {
        return Task.CompletedTask;
    }
}
