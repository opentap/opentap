using System.Collections.Generic;
using System.Linq;
namespace OpenTap.Plugins.BasicSteps;

class SweepRowMemberData : IMemberData, IParameterMemberData
{
    private readonly SweepRowTypeData _declaringType;
    private readonly IMemberData _innerMember;
    private TapSerializer _tapSerializer;
        
    public SweepRowMemberData(SweepRowTypeData declaringType, IMemberData innerMember)
    {
        _declaringType = declaringType;
        _innerMember = innerMember;
        ParameterizedMembers = [(declaringType.SweepParameterLoop, innerMember)];
    }

    public IEnumerable<object> Attributes => _innerMember.Attributes.Where(attr => !(attr is FactoryAttribute || attr is ElementFactoryAttribute));
    public string Name => _innerMember.Name;
    public ITypeData DeclaringType => _declaringType;
    public ITypeData TypeDescriptor => _innerMember.TypeDescriptor;
    public bool Writable => _innerMember.Writable;
    public bool Readable => _innerMember.Readable;
    public void SetValue(object owner, object value)
    {
        var own = (SweepRow)owner;
        own.Values[Name] = CloneIfPossible(value, own.Loop);
    }

    public object GetValue(object owner)
    {
        var row = (SweepRow)owner;
        if(row.Values.TryGetValue(Name, out var value))
            return value;
        var clone = CloneIfPossible(_innerMember.GetValue(owner), row.Loop);
        row.Values[Name] = clone;
        return clone;
    }
        
        
    object CloneIfPossible(object value, object context)
    {
        if (value == null) return null;
        // SweepRow and SweepRowCollection are not that easy to clone. 
        // so to support use cases like sweeps of sweeps, they get special treatment here.
        if (value is SweepRow sr)
        {
            var sr2 = new SweepRow(sr.Loop)
            {
                Enabled = sr.Enabled
            };
            foreach (var kv in sr.Values)
                sr2.Values.Add(kv.Key, CloneIfPossible(kv.Value, context));
            return sr2;
        }

        if (value is SweepRowCollection src)
        {
            var src2 = new SweepRowCollection(src.Loop);
            foreach(var element in src)
                src2.Add((SweepRow)CloneIfPossible(element, context));
            return src2;
        }

        if (Utils.IsTriviallyCloneable(value))
            return value;
            
        var valType = TypeData.GetTypeData(value);
        var td = valType.AsTypeData();
        if (td.IsValueType)
            return value;
            
        if (StringConvertProvider.TryGetString(value, out string result))
        {
            if (StringConvertProvider.TryFromString(result, valType, context, out object result2))
                return result2;
        }
            
        _tapSerializer ??= new TapSerializer();
        try
        {
            return _tapSerializer.DeserializeFromString(_tapSerializer.SerializeToString(value), valType) ?? value;
        }
        catch
        {
            return value;
        }
    }

    public IEnumerable<(object Source, IMemberData Member)> ParameterizedMembers { get; }
}