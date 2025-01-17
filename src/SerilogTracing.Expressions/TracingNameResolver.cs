﻿using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Serilog.Expressions;

#if NETSTANDARD2_0
using SerilogTracing.Pollyfill;
#nullable disable warnings
#endif

namespace SerilogTracing.Expressions;

/// <summary>
/// Adds expression support for <c>TimeSpan Elapsed()</c>, <c>bool IsSpan()</c>, <c>bool IsRootSpan()</c>,
/// <c>TimeSpan FromUnixEpoch(DateTime)</c>, <c>long Milliseconds(TimeSpan)</c>, <c>long Microseconds(TimeSpan)</c>,
/// <c>ulong or long Nanoseconds(TimeSpan)</c>. Note that the <c>Nanoseconds</c> function is undefined on overflow or
/// underflow.
/// </summary>
public class TracingNameResolver: NameResolver
{
    readonly NameResolver _tracingFunctions = new StaticMemberNameResolver(typeof(TracingFunctions));

    /// <inheritdoc/>
    public override bool TryResolveFunctionName(string name, [NotNullWhen(true)] out MethodInfo? implementation)
    {
        return _tracingFunctions.TryResolveFunctionName(name, out implementation);
    }
}

#if NETSTANDARD2_0
#nullable enable warnings
#endif
