using Xunit;

// Many tests share the global QueryDuckCapture ring buffer; running test
// classes in parallel makes them pollute each other's captured events.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
