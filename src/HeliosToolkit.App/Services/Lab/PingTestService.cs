using System.Net.NetworkInformation;
using HeliosToolkit.Core.Lab;
using Serilog;

namespace HeliosToolkit.App.Services.Lab;

/// <summary>Small ping burst to measure latency, jitter and loss to a host.</summary>
public sealed class PingTestService
{
    public async Task<PingResultStats> RunAsync(
        string host, int count = 20, CancellationToken ct = default)
    {
        var rtts = new List<double>(count);
        using var ping = new Ping();

        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                PingReply reply = await ping.SendPingAsync(host, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    rtts.Add(reply.RoundtripTime);
                }
            }
            catch (PingException e)
            {
                Log.Debug(e, "Ping {Host} failed", host);
            }

            if (i < count - 1)
            {
                await Task.Delay(150, ct);
            }
        }

        return PingStats.FromRtts(rtts, count);
    }
}
