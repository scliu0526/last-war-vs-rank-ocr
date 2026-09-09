using Microsoft.ML.OnnxRuntime;
using System.IO;

namespace RankLens.App;

public static class OcrRecognitionDecoder
{
    public static string Decode(OcrTensorOutput output, IReadOnlyList<string> dictionary, int blankIndex = 0)
    {
        var dimensions = output.Dimensions;
        if (dimensions.Length != 3 || dimensions[0] != 1)
            throw new InvalidDataException("Recognition 模型輸出 shape 不符合 [1,time,classes]。");
        var timesteps = new List<IReadOnlyList<float>>(dimensions[1]);
        for (var time = 0; time < dimensions[1]; time++)
        {
            var values = new float[dimensions[2]];
            Array.Copy(output.Values, time * dimensions[2], values, 0, dimensions[2]);
            timesteps.Add(values);
        }
        return CtcDecoder.Decode(timesteps, dictionary, blankIndex);
    }

    public static string DecodeFirstTensor(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,
        IReadOnlyList<string> dictionary,
        int blankIndex = 0)
    {
        var tensor = outputs.FirstOrDefault(output => output.ValueType == OnnxValueType.ONNX_TYPE_TENSOR)?.AsTensor<float>()
            ?? throw new InvalidDataException("Recognition 模型沒有 float tensor 輸出。");
        var dimensions = tensor.Dimensions.ToArray();
        return Decode(new OcrTensorOutput("recognition", dimensions, tensor.ToArray()), dictionary, blankIndex);
    }
}
