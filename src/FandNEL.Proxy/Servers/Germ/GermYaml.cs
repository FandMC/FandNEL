using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace FandNEL.Proxy.Servers.Germ;

/// <summary>带边界校验的 YAML 解析，限制体积、深度、节点数并拒绝循环别名。</summary>
internal static class GermYaml
{
    private const int MaximumCharacters = 1_048_576;
    private const int MaximumDepth = 64;
    private const int MaximumNodes = 20_000;

    private static readonly IDeserializer Deserializer = new DeserializerBuilder().WithDuplicateKeyChecking().Build();

    /// <summary>把 YAML 文本解析为字典；输入为空或非字典时返回空字典。</summary>
    internal static Dictionary<object, object> Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return [];
        if (yaml.Length > MaximumCharacters)
            throw new InvalidDataException($"YAML 输入超过 {MaximumCharacters} 字符上限");

        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count != 1)
            throw new InvalidDataException("YAML 必须且只能包含一个文档");

        var remainingNodes = MaximumNodes;
        ValidateNode(stream.Documents[0].RootNode, 0, ref remainingNodes, new HashSet<YamlNode>(ReferenceEqualityComparer.Instance));
        return Deserializer.Deserialize<Dictionary<object, object>>(new StringReader(yaml)) ?? [];
    }

    private static void ValidateNode(YamlNode node, int depth, ref int remainingNodes, HashSet<YamlNode> activeNodes)
    {
        if (depth > MaximumDepth)
            throw new InvalidDataException($"YAML 嵌套深度超过 {MaximumDepth}");
        if (--remainingNodes < 0)
            throw new InvalidDataException($"YAML 节点数超过 {MaximumNodes}");
        if (!activeNodes.Add(node))
            throw new InvalidDataException("YAML 包含循环别名");
        try
        {
            switch (node)
            {
                case YamlMappingNode mapping:
                    foreach (var child in mapping.Children)
                    {
                        ValidateNode(child.Key, depth + 1, ref remainingNodes, activeNodes);
                        ValidateNode(child.Value, depth + 1, ref remainingNodes, activeNodes);
                    }
                    break;
                case YamlSequenceNode sequence:
                    foreach (var child in sequence.Children)
                        ValidateNode(child, depth + 1, ref remainingNodes, activeNodes);
                    break;
            }
        }
        finally
        {
            activeNodes.Remove(node);
        }
    }
}
