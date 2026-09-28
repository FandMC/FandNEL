using System.Collections.ObjectModel;

namespace FandNEL.Proxy.Packet.Minecraft.Nbt;

public enum NbtTagType : byte { End = 0, Byte = 1, Short = 2, Int = 3, Long = 4, Float = 5, Double = 6, ByteArray = 7, String = 8, List = 9, Compound = 10, IntArray = 11, LongArray = 12 }

public abstract class NbtTag(NbtTagType type)
{
    public NbtTagType Type { get; } = type;
    public abstract NbtTag DeepClone();
}

public sealed class NbtEnd : NbtTag
{
    public static NbtEnd Instance { get; } = new();
    private NbtEnd() : base(NbtTagType.End) { }
    public override NbtTag DeepClone() => this;
}

public sealed class NbtByte(sbyte value) : NbtTag(NbtTagType.Byte) { public sbyte Value { get; set; } = value; public override NbtTag DeepClone() => new NbtByte(Value); }
public sealed class NbtShort(short value) : NbtTag(NbtTagType.Short) { public short Value { get; set; } = value; public override NbtTag DeepClone() => new NbtShort(Value); }
public sealed class NbtInt(int value) : NbtTag(NbtTagType.Int) { public int Value { get; set; } = value; public override NbtTag DeepClone() => new NbtInt(Value); }
public sealed class NbtLong(long value) : NbtTag(NbtTagType.Long) { public long Value { get; set; } = value; public override NbtTag DeepClone() => new NbtLong(Value); }
public sealed class NbtFloat(float value) : NbtTag(NbtTagType.Float) { public float Value { get; set; } = value; public override NbtTag DeepClone() => new NbtFloat(Value); }
public sealed class NbtDouble(double value) : NbtTag(NbtTagType.Double) { public double Value { get; set; } = value; public override NbtTag DeepClone() => new NbtDouble(Value); }

public sealed class NbtByteArray(IEnumerable<byte> value) : NbtTag(NbtTagType.ByteArray)
{
    private byte[] _value = value?.ToArray() ?? throw new ArgumentNullException(nameof(value));
    public byte[] Value { get => (byte[])_value.Clone(); set => _value = value?.ToArray() ?? throw new ArgumentNullException(nameof(value)); }
    public override NbtTag DeepClone() => new NbtByteArray(_value);
}
public sealed class NbtString(string value) : NbtTag(NbtTagType.String) { public string Value { get; set; } = value ?? throw new ArgumentNullException(nameof(value)); public override NbtTag DeepClone() => new NbtString(Value); }

public sealed class NbtList(NbtTagType elementType, IEnumerable<NbtTag>? values = null) : NbtTag(NbtTagType.List)
{
    private readonly List<NbtTag> _values = values?.ToList() ?? [];
    public NbtTagType ElementType { get; } = elementType;
    public IReadOnlyList<NbtTag> Values => new ReadOnlyCollection<NbtTag>(_values);
    public int Count => _values.Count;
    public NbtTag this[int index] { get => _values[index]; set { Validate(value); _values[index] = value; } }
    public NbtList Add(NbtTag value) { Validate(value); _values.Add(value); return this; }
    private void Validate(NbtTag value) { ArgumentNullException.ThrowIfNull(value); if (value.Type != ElementType) throw new ArgumentException("列表元素类型不匹配。", nameof(value)); }
    public override NbtTag DeepClone() => new NbtList(ElementType, _values.Select(x => x.DeepClone()));
}

public sealed class NbtCompound(string name = "") : NbtTag(NbtTagType.Compound)
{
    private readonly Dictionary<string, NbtTag> _values = new(StringComparer.Ordinal);
    public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));
    public IReadOnlyDictionary<string, NbtTag> Values => new ReadOnlyDictionary<string, NbtTag>(_values);
    public NbtTag this[string key] { get => _values[key]; set => Set(key, value); }
    public bool TryGet(string key, out NbtTag value) => _values.TryGetValue(key, out value!);
    public bool Remove(string key) => _values.Remove(key);
    public NbtCompound Set(string key, NbtTag value) { ArgumentNullException.ThrowIfNull(key); ArgumentNullException.ThrowIfNull(value); if (value.Type == NbtTagType.End) throw new ArgumentException("Compound 不能包含 End 标签。", nameof(value)); _values[key] = value; return this; }
    public override NbtTag DeepClone() { var copy = new NbtCompound(Name); foreach (var item in _values) copy.Set(item.Key, item.Value.DeepClone()); return copy; }
}
public sealed class NbtIntArray(IEnumerable<int> value) : NbtTag(NbtTagType.IntArray) { private int[] _value = value?.ToArray() ?? throw new ArgumentNullException(nameof(value)); public int[] Value { get => (int[])_value.Clone(); set => _value = value?.ToArray() ?? throw new ArgumentNullException(nameof(value)); } public override NbtTag DeepClone() => new NbtIntArray(_value); }
public sealed class NbtLongArray(IEnumerable<long> value) : NbtTag(NbtTagType.LongArray) { private long[] _value = value?.ToArray() ?? throw new ArgumentNullException(nameof(value)); public long[] Value { get => (long[])_value.Clone(); set => _value = value?.ToArray() ?? throw new ArgumentNullException(nameof(value)); } public override NbtTag DeepClone() => new NbtLongArray(_value); }
