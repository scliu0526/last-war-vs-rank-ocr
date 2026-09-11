using System.Runtime.InteropServices;
using Vortice.DXGI;
using static Vortice.DXGI.DXGI;

namespace RankLens.App;

public sealed record GpuAdapterInfo(
    int DeviceId,
    string Name,
    bool IsLikelyDirectMLCompatible,
    string CompatibilityReason);
public sealed record GpuAdapterOption(string Name, bool IsEnabled, string Reason);

public sealed class GpuAdapterCatalog
{
    private const int Direct3DFeatureLevel11 = 0xB000;
    private static readonly Guid Direct3D12DeviceInterface = new("189819F1-1DB6-4B57-BE54-1821339B85F7");

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
                var isSoftware = description.Flags.HasFlag(AdapterFlags.Software);
                var isCompatible = !isSoftware && SupportsDirectX12(adapter.NativePointer);
                var reason = isSoftware
                    ? "軟體顯示卡不可用於 DirectML OCR，請使用 CPU 模式。"
                    : isCompatible
                        ? "支援 DirectX 12，可供 DirectML 使用。"
                        : "顯示卡不支援 DirectX 12，無法使用 DirectML OCR。";
                result.Add(new GpuAdapterInfo(
                    (int)deviceId,
                    description.Description.TrimEnd('\0'),
                    isCompatible,
                    reason));
            }
        }

        return result;
    }

    private static bool SupportsDirectX12(nint adapter)
    {
        nint device = 0;
        try
        {
            var interfaceId = Direct3D12DeviceInterface;
            return D3D12CreateDevice(adapter, Direct3DFeatureLevel11, ref interfaceId, out device) >= 0
                && device != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            if (device != 0) Marshal.Release(device);
        }
    }

    [DllImport("d3d12.dll", ExactSpelling = true)]
    private static extern int D3D12CreateDevice(
        nint adapter,
        int minimumFeatureLevel,
        ref Guid interfaceId,
        out nint device);
}
