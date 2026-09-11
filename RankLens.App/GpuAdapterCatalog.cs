using Vortice.DXGI;
using static Vortice.DXGI.DXGI;

namespace RankLens.App;

public sealed record GpuAdapterInfo(int DeviceId, string Name, bool IsLikelyDirectMLCompatible);
public sealed record GpuAdapterOption(string Name, bool IsEnabled, string Reason);

public sealed class GpuAdapterCatalog
{
    public IReadOnlyList<GpuAdapterInfo> Enumerate()
    {
        var result = new List<GpuAdapterInfo>();
        using var factory = CreateDXGIFactory1<IDXGIFactory1>();
        for (uint deviceId = 0; ; deviceId++)
        {
            var adapterResult = factory.EnumAdapters1(deviceId, out var adapter);
            if (adapterResult.Failure || adapter is null) break;
            using (adapter)
            {
                var description = adapter.Description1;
                var isHardware = !description.Flags.HasFlag(AdapterFlags.Software);
                result.Add(new GpuAdapterInfo((int)deviceId, description.Description.TrimEnd('\0'), isHardware));
            }
        }

        return result;
    }
}
