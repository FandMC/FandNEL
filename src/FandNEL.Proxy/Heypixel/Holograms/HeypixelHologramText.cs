using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Heypixel;

/// <summary>只转换组件的可见文字，颜色、字体、点击事件和其他 NBT 字段原样保留。</summary>
internal static class HeypixelHologramText
{
    internal static NbtCompound AsCompound(NbtTag component)
    {
        if (component is NbtCompound compound) return (NbtCompound)compound.DeepClone();
        if (component is NbtString literal) return new NbtCompound().Set("text", literal.DeepClone());
        if (component is not NbtList { Count: > 0 } list)
            throw new InvalidDataException("无效的文字组件。");
        // 数组组件的首元素是父组件，后续元素继承它的样式。
        var parent = AsCompound(list[0]);
        var children = parent.TryGet("extra", out var extra) && extra is NbtList existing
            ? existing.Values.Select(value => (NbtTag)AsCompound(value)).ToList() : [];
        children.AddRange(list.Values.Skip(1).Select(value => (NbtTag)AsCompound(value)));
        if (children.Count > 0) parent.Set("extra", new NbtList(NbtTagType.Compound, children));
        return parent;
    }

    internal static NbtTag Normalize(NbtTag source, out bool changed, out bool multiline)
    {
        var copy = source.DeepClone();
        var hasChanges = false;
        var hasLines = false;
        Visit(copy);
        changed = hasChanges;
        multiline = hasLines;
        return copy;

        void Text(NbtString text)
        {
            var value = text.Value.Replace("\\r\\n", "\n", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            hasChanges |= value != text.Value;
            hasLines |= value.Contains('\n');
            text.Value = value;
        }

        void Visit(NbtTag component)
        {
            switch (component)
            {
                case NbtString text:
                    Text(text);
                    break;
                case NbtList list:
                    foreach (var child in list.Values) Visit(child);
                    break;
                case NbtCompound compound:
                    if (compound.TryGet("text", out var content) && content is NbtString literal) Text(literal);
                    if (compound.TryGet("fallback", out var fallback) && fallback is NbtString fallbackText) Text(fallbackText);
                    if (compound.TryGet("extra", out var extra)) Visit(extra);
                    if (compound.TryGet("with", out var arguments)) Visit(arguments);
                    if (compound.TryGet("separator", out var separator)) Visit(separator);
                    break;
            }
        }
    }
}
