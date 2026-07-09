namespace QueryDuck.Core.Capture;

public sealed record QueryEventDiffRequest(string LeftEventId, string RightEventId);
