namespace QueryDuck.Core.Capture;

public static class QueryCaptureSampling
{
    public static bool ShouldCapture(QueryCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.EnableSampling)
        {
            return true;
        }

        if (options.SamplingRate <= 0)
        {
            return false;
        }

        if (options.SamplingRate >= 1)
        {
            return true;
        }

        return Random.Shared.NextDouble() <= options.SamplingRate;
    }
}
