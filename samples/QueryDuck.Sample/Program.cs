using Microsoft.EntityFrameworkCore;
using QueryDuck.Core;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Debugging;
using QueryDuck.Sample;

var optionsBuilder = new DbContextOptionsBuilder<SampleDbContext>()
    .UseOracle(Environment.GetEnvironmentVariable("QUERYDUCK_ORACLE_CONNECTION")
        ?? "User Id=sample;Password=sample;Data Source=localhost:1521/FREEPDB1")
    .UseQueryDuckDebugging();

await using var context = new SampleDbContext(optionsBuilder.Options);

Console.WriteLine($"QueryDuck sample - {QueryDuckAssembly.Version}");
Console.WriteLine("Auto-capture is ON - all queries include expression trees without WithQueryDuckScope().");
Console.WriteLine("Event server: http://127.0.0.1:17654/queryduck/events");
Console.WriteLine("Open the QueryDuck tool window in Rider (View -> Tool Windows -> QueryDuck).");
Console.WriteLine();

var emptyCodeQuery = SampleQueries.FindByEmptyCode(context);
var debugView = emptyCodeQuery.Debug(context);

Console.WriteLine(debugView.Summary);
Console.WriteLine(debugView.Sql);
Console.WriteLine();
Console.WriteLine("Warnings:");
foreach (var warning in debugView.Warnings)
{
    Console.WriteLine($"  [{warning.RuleId}] {warning.Message}");
}

// Auto-capture: just execute the query - no WithQueryDuckScope() needed.
QueryDuckCapture.RecordFromQuery(emptyCodeQuery, context);
QueryDuckCapture.RecordFromQuery(SampleQueries.CustomerRegionNames(context), context);

Console.WriteLine();
Console.WriteLine($"Captured events: {QueryDuckCapture.LastCommands.Count}");
Console.WriteLine("Press Enter to exit (leave running while using the Rider plugin)...");
Console.ReadLine();
