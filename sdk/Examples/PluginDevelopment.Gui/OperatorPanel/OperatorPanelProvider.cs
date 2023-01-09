using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Keysight.OpenTap.Wpf;
using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    [Display("Operator UI", Group:"Examples")]
    public class OperatorPanelProvider : ITapDockPanel
    {
        internal readonly OperatorUiSetting Setting;
        public OperatorPanelProvider()
        {
            
        }
        
        internal OperatorPanelProvider(OperatorUiSetting setting) => Setting = setting;
        
        public FrameworkElement CreateElement(ITapDockContext context)
        {
            return new OperatorUIPanel(context, Setting);
        }

        public double? DesiredWidth => 200;
        public double? DesiredHeight => 200;
    }

    public class UiParameter
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }
    
    public class OperatorUiSetting
    {
        public string Name { get; set; }
        public List<UiParameter> Parameters { get; set; } = new List<UiParameter>();
    }

    class OperatorUiTypeData : ITypeData
    {
        public readonly OperatorUiSetting Setting;

        public OperatorUiTypeData(OperatorUiSetting setting)
        {
            this.Setting = setting;
            BaseType = TypeData.FromType(typeof(OperatorPanelProvider));
            Name = "OperatorPanel:" + setting.Name;
            Attributes = new object[] { new DisplayAttribute(setting.Name, "Operator UI", Group: "Operator UI") };
        }
        public IEnumerable<object> Attributes { get; }
        public string Name { get; }
        public IEnumerable<IMemberData> GetMembers() => BaseType.GetMembers();

        public IMemberData GetMember(string name) => BaseType.GetMember(name);

        public object CreateInstance(object[] arguments) =>  new OperatorPanelProvider(Setting);

        public ITypeData BaseType { get; }
        public bool CanCreateInstance => true;
    }
    
    public class OperatorUiSettings : ComponentSettingsList<OperatorUiSettings, OperatorUiSetting>
    {
        
    }

    public class OperatorUiTypeDataProvider : ITypeDataProvider, ITypeDataSearcherCacheInvalidated
    {
        public ITypeData GetTypeData(string identifier)
        {
            if (identifier.StartsWith("OperatorPanel:") == false)
                return null;
            return Types.FirstOrDefault(x => x.Name == identifier);
        }

        public ITypeData GetTypeData(object obj)
        {
            if (obj is OperatorPanelProvider p)
                return Types.OfType<OperatorUiTypeData>().FirstOrDefault(x => x.Setting == p.Setting);
            
            return null;
        }

        public double Priority { get; }
        
        public void Search()
        {
            
        }

        ITypeData[] types;
        public IEnumerable<ITypeData> Types
        {
            get
            {
                if(types == null || OperatorUiSettings.Current.Count != types.Length)
                    types = OperatorUiSettings.Current.Select(x => new OperatorUiTypeData(x)).ToArray();
                return types;
            }
            
        } 
        public event EventHandler<TypeDataCacheInvalidatedEventArgs> CacheInvalidated;
    }
}