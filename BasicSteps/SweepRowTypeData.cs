using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Plugins.BasicSteps;

class SweepRowTypeData : ITypeData
{
    public IEnumerable<object> Attributes => BaseType.Attributes;
        
    // Sweep row type data cannot be deserialized in a normal sense anyway. Needs sweep step reference
    public string Name => BaseType.Name; 
    public ITypeData BaseType { get; } = TypeData.FromType(typeof(SweepRow));
    public IEnumerable<IMemberData> GetMembers() => BaseType.GetMembers().Concat(GetSweepMembers());

    IEnumerable<IMemberData> GetSweepMembers()
    {
        var selected = SweepParameterLoop.SelectedParameters;
        var loopMembers = TypeData.GetTypeData(SweepParameterLoop).GetMembers()
            .Where(x => selected.Contains(x))
            .OfType<IParameterMemberData>();
        return loopMembers.Select(x => new SweepRowMemberData(this, x));
    } 

    public IMemberData GetMember(string name)
    {
        return BaseType.GetMember(name) ?? GetSweepMembers().FirstOrDefault(x => x.Name == name);
    }

    public SweepParameterStep SweepParameterLoop;

    public SweepRowTypeData(SweepParameterStep sweepParameterLoop)
    {
        this.SweepParameterLoop = sweepParameterLoop;
    }
        
    public object CreateInstance(object[] arguments)
    {
        throw new Exception("Cannot create instance");
    }

    public bool CanCreateInstance => false;

    public override bool Equals(object obj)
    {
        if (obj is SweepRowTypeData otherSweepRow && otherSweepRow.SweepParameterLoop == SweepParameterLoop)
            return true;
        return false;
    }

    public override int GetHashCode()
    {
        return SweepParameterLoop.GetHashCode() * 37012721 + 1649210;
    }
};