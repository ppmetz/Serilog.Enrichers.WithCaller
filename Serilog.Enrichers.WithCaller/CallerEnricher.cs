using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Collections.Generic.Enumerable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Serilog.Enrichers.WithCaller;

public class CallerEnricher : ILogEventEnricher
{
    private readonly bool _includeReturnParameter;
    private readonly bool _includeParameters;
    private readonly bool _includeParameterNames;
    private readonly bool _includeFileInfo;
    private readonly bool _useFullTypeName;
    private readonly int _maxDepth;
    private readonly Predicate<MethodBase> _filter;

    private static Predicate<MethodBase> _defaultFilter = method => method?.DeclaringType?.Assembly == typeof(Log).Assembly;

    public CallerEnricher()
        : this(null, null, null, null, null, 1, _defaultFilter)
    {
        // added default constructor again so one can use the generic Enrich.With<CallerEnricher>() method
    }

    // for backward compatibility with 1.2
    public CallerEnricher(bool? includeFileInfo, int maxDepth)
        : this(null, null, null, null, includeFileInfo, maxDepth, _defaultFilter)
    {
    }

    public CallerEnricher(bool? includeReturnParameter, bool? useFullTypeName, bool? includeParameters, bool? includeParameterNames, bool? includeFileInfo, int maxDepth)
        : this(includeReturnParameter, useFullTypeName, includeParameters, includeParameterNames, includeFileInfo, maxDepth, _defaultFilter)
    {
    }

    public CallerEnricher(bool? includeReturnParameter, bool? useFullTypeName, bool? includeParameters, bool? includeParameterNames, bool? includeFileInfo, int maxDepth, Predicate<MethodBase> filter)
    {
        _includeReturnParameter = includeReturnParameter ?? false;
        _includeFileInfo = includeFileInfo ?? false;
        _includeParameters = includeParameters ?? true;
        _includeParameterNames = _includeParameters && (includeParameterNames ?? false);
        _useFullTypeName = useFullTypeName ?? true;
        _maxDepth = Math.Max(1, maxDepth);
        _filter = filter;
    }

    public static int SkipFramesCount { get; set; } = 3;
    public static int MaxFrameCount { get; set; } = 128;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        int foundFrames = 0;

        StringBuilder caller = new();
        EnhancedStackTrace stackTrace = EnhancedStackTrace.Current();

        IEnumerable<EnhancedStackFrame> framesToProcess = stackTrace.Take(MaxFrameCount).Skip(SkipFramesCount);

        foreach (EnhancedStackFrame frame in framesToProcess)
        {
            if (!frame.HasMethod())
            {
                break;
            }

            ResolvedMethod method = frame.MethodInfo;

            if (_filter(method.MethodBase ?? method.SubMethodBase))
            {
                continue;
            }

            if (foundFrames > 0)
            {
                caller.Append(" at ");
            }

            if (!_includeReturnParameter)
            {
                method.ReturnParameter = null;
                method.IsAsync = false; // if we don't want return parameter, then we don't want this either
            }

            if (!_includeParameters)
            {
                method.Parameters = EnumerableIList<ResolvedParameter>.Empty;
                method.SubMethodParameters = EnumerableIList<ResolvedParameter>.Empty;
            }
            else
            {
                if (!_includeParameterNames)
                {
                    foreach (ResolvedParameter param in method.Parameters)
                    {
                        param.Name = "";
                    }
                    foreach (ResolvedParameter param in method.SubMethodParameters)
                    {
                        param.Name = "";
                    }
                }
            }

            method.Append(caller, _useFullTypeName);

            if (_includeFileInfo && frame.GetFileName() is string callerFileName)
            {
                caller.Append($" {callerFileName}:{frame.GetFileLineNumber()}");
            }

            foundFrames++;

            if (foundFrames >= _maxDepth)
            {
                break;
            }
        }

        if (foundFrames > 0)
        {
            logEvent.AddPropertyIfAbsent(new LogEventProperty("Caller", new ScalarValue(caller.ToString())));
            return;
        }

        logEvent.AddPropertyIfAbsent(new LogEventProperty("Caller", new ScalarValue("<unknown method>")));
        return;
    }
}
