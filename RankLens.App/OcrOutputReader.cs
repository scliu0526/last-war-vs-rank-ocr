using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace RankLens.App;

public sealed record OcrTensorOutput(string Name, int[] Dimensions, float[] Values);

public static class OcrOutputReader
{
    public static IReadOnlyList<OcrTensorOutput> Read(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs)
    {
        var result = new List<OcrTensorOutput>();
        foreach (var output in outputs)
        {
            if (output.ValueType != OnnxValueType.ONNX_TYPE_TENSOR) continue;
            Tensor<float> tensor;
            try { tensor = output.AsTensor<float>(); }
            catch (InvalidCastException) { continue; }
            result.Add(new OcrTensorOutput(output.Name, tensor.Dimensions.ToArray(), tensor.ToArray()));
        }
        return result;
    }
}
