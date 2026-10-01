namespace FandNEL.Proxy.Packet.Minecraft.Components;

/// <summary>物品组件补丁；保留原始组件 bytes，便于尚未需要业务理解的组件安全透传。</summary>
public sealed class DataComponentPatch
{
    private readonly Dictionary<int, RawDataComponent> _added = [];
    private readonly HashSet<int> _removed = [];

    public IReadOnlyDictionary<int, RawDataComponent> Added => _added;
    public IReadOnlySet<int> Removed => _removed;
    public bool IsEmpty => _added.Count == 0 && _removed.Count == 0;

    public void Set(RawDataComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _added[component.TypeId] = component;
        _removed.Remove(component.TypeId);
    }

    public void Remove(int typeId)
    {
        _added.Remove(typeId);
        _removed.Add(typeId);
    }

    public bool TryGet(int typeId, out RawDataComponent? component) => _added.TryGetValue(typeId, out component);
}
