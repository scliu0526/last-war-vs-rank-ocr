using System.Management;

namespace RankLens.App;

public sealed record GpuAdapterInfo(int DeviceId, string Name, bool IsLikelyDirectMLCompatible);

public sealed class GpuAdapterCatalog
{
    public IReadOnlyList<GpuAdapterInfo> Enumerate()
    {
        var result = new List<GpuAdapterInfo>();
        using var query = new ManagementObjectSearcher("SELECT Name, Status FROM Win32_VideoController");
        var deviceId = 0;
        foreach (ManagementObject item in query.Get())
        {
            var name = item["Name"]?.ToString();
            var status = item["Status"]?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(new GpuAdapterInfo(deviceId++, name, string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)));
            }
        }

        return result;
    }
}
