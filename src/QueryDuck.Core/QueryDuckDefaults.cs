namespace QueryDuck.Core;

public static class QueryDuckDefaults
{
    public const int EventServerPort = 17654;

    public const string EventServerHost = "127.0.0.1";

    public const string ServerPrefix = "http://127.0.0.1:17654/";

    public const string EventsUrl = "http://127.0.0.1:17654/queryduck/events";

    public const int EventSchemaVersion = 8;

    public const int MaxRequestBodyBytes = 5 * 1024 * 1024;
}
