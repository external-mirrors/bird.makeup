using System.Diagnostics;
using OpenTelemetry;

namespace BirdsiteLive.Middleware;

/// <summary>
/// A processor that filters out ASP.NET Core server traces while keeping internal traces.
/// </summary>
public class FilterProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        // Only export internal traces, filter out server/client/etc traces
        if (data.Kind != ActivityKind.Internal)
        {
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }

        base.OnEnd(data);
    }
}

