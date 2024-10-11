using System;

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Serilog.Core;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.InMemory.Assertions;

namespace Serilog.Enrichers.WithCaller.Tests;

[TestClass()]
public class CallerEnricherTests
{
    public static string OutputTemplate { get; } = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message} (at {Caller}){NewLine}{Exception}";
    public static LoggingLevelSwitch LoggingLevelSwitch { get; } = new LoggingLevelSwitch(Events.LogEventLevel.Verbose);

    public static InMemorySink InMemoryInstance => InMemorySink.Instance;

    public static ILogger CreateLogger(bool includeFileInfo = false, int maxDepth = 1)
    {
        return new LoggerConfiguration()
            .Enrich.WithCaller(includeFileInfo, maxDepth)
            .WriteTo.InMemory(outputTemplate: OutputTemplate)
            .CreateLogger();
    }

    [TestCleanup]
    public void Cleanup()
    {
        InMemoryInstance.Dispose();
    }

    //https://gist.github.com/nblumhardt/0e1e22f50fe79de60ad257f77653c813
    //https://github.com/serilog-contrib/SerilogSinksInMemory
    [TestMethod()]
    public void EnrichTest()
    {
        var logger = new LoggerConfiguration()
                    .Enrich.WithCaller()
                    .WriteTo.InMemory(outputTemplate: OutputTemplate)
                    .CreateLogger();

        logger.Error(new Exception(), "hello");
        InMemoryInstance.Should()
            .HaveMessage("hello")
            .Appearing().Once()
            .WithProperty("Caller")
            .WithValue("Serilog.Enrichers.WithCaller.Tests.CallerEnricherTests.EnrichTest()");
    }

    [TestMethod()]
    public void EnrichTestWithFileInfo()
    {
        var fileName = new StackFrame(needFileInfo: true).GetFileName();

        var logger = new LoggerConfiguration()
                    .Enrich.WithCaller(true, 1)
                    .WriteTo.InMemory(outputTemplate: OutputTemplate)
                    .CreateLogger();

        logger.Error(new Exception(), "hello"); // line value "nn" is the suffix in WithValue check below
        
        InMemoryInstance.Should()
            .HaveMessage("hello")
            .Appearing().Once()
            .WithProperty("Caller")
            .WithValue($"Serilog.Enrichers.WithCaller.Tests.CallerEnricherTests.EnrichTestWithFileInfo() " + 
                       $"{fileName}:62");
    }

    [TestMethod()]
    public void MaxDepthTest()
    {
        Logger logger = new LoggerConfiguration()
                            .Enrich.WithCaller(includeFileInfo: false, maxDepth: 2)
                            .WriteTo.InMemory(outputTemplate: OutputTemplate)
                            .CreateLogger();

        logger.Error(new Exception(), "hello");

        InMemoryInstance.Should()
            .HaveMessage("hello")
            .Appearing().Once()
            .WithProperty("Caller")
            .WithValue("Serilog.Enrichers.WithCaller.Tests.CallerEnricherTests.MaxDepthTest() " +
                       "at RuntimeMethodHandle.InvokeMethod(object, Void**, Signature, bool)");
    }

    [TestMethod()]
    public void MaximumDetailTest()
    {
        string fileName = new StackFrame(needFileInfo: true).GetFileName();

        Logger logger = new LoggerConfiguration()
                            .Enrich.With(new CallerEnricher(true, true, true, true, true, 1))   // mit alles und scharf
                            .WriteTo.InMemory(outputTemplate: OutputTemplate)
                            .CreateLogger();

        void someLocalFunc(Lazy<string> someArg)
        {
            logger.Error(new Exception(), someArg.Value);
        }

        someLocalFunc(new Lazy<string>("hello"));   // just to have a generic argument

        InMemoryInstance.Should()
            .HaveMessage("hello")
            .Appearing().Once()
            .WithProperty("Caller")
            .WithValue($"void Serilog.Enrichers.WithCaller.Tests.CallerEnricherTests.MaximumDetailTest()+someLocalFunc(Lazy<string> someArg) " +
                       $"{fileName}:102");
    }

    [TestMethod()]
    public void MinimumDetailTest()
    {
        var logger = new LoggerConfiguration()
                    .Enrich.With(new CallerEnricher(false, false, false, false, false, 1))
                    .WriteTo.InMemory(outputTemplate: OutputTemplate)
                    .CreateLogger();

        void someLocalFunc(Lazy<string> someArg)
        {
            logger.Error(new Exception(), someArg.Value);
        }

        someLocalFunc(new Lazy<string>("hello"));   // just to have a generic argument

        InMemoryInstance.Should()
            .HaveMessage("hello")
            .Appearing().Once()
            .WithProperty("Caller")
            .WithValue($"CallerEnricherTests.MinimumDetailTest()+someLocalFunc()");
    }
}