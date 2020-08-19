using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>
    /// This annotation represents a context menu for a given setting. Multiple of these might be present in a single
    /// AnnotationCollection. 
    /// </summary>
    public sealed class MenuAnnotation : IOwnedAnnotation
    {
        /// <summary> Creates a new instance. </summary>
        /// <param name="member"> Which member of an object should the menu be generated for</param>
        internal MenuAnnotation(IMemberData member) => this.member = member; 

        /// <summary> The model used to construct menu items. </summary>
        readonly IMemberData member; 
        List<AnnotationCollection> annotations;
        List<IMenuModel> models;
        object[] source;

        List<IMenuModel> getModels()
        {
            if (models == null)
            {
                var factoryTypes = TypeData.GetDerivedTypes<IMenuModelFactory>();
                models = new List<IMenuModel>();
                foreach (var type in factoryTypes)
                {
                    if (type.CanCreateInstance == false) continue;
                    try
                    {
                        var factory = (IMenuModelFactory)type.CreateInstance();
                        var model = factory.CreateModel(member);
                        if (model == null) continue;
                        model.Source = source;
                        models.Add(model);
                    }
                    catch
                    {
                    
                    }
                }
            }

            return models;
        }

        private static bool FilterDefault2(IReflectionData m)
        {
            var browsable = m.GetAttribute<BrowsableAttribute>();

            // Browsable overrides everything
            if (browsable != null) return browsable.Browsable;

            var xmlIgnore = m.GetAttribute<XmlIgnoreAttribute>();
            if (xmlIgnore != null)
                return false;

            if (m is IMemberData mem)
            {
                if (m.HasAttribute<OutputAttribute>())
                    return true;
                if (!mem.Writable || !mem.Readable)
                    return false;
                return true;
            }
            return false;
        }
        
        void generateAnnotations()
        {
            List<IMenuModel> models = getModels();
            List<AnnotationCollection> items = new List<AnnotationCollection>();
            foreach (var model in models)
            {
                var a = AnnotationCollection.Annotate(model);

                var members = a.Get<IMembersAnnotation>()?.Members
                    .Where(x =>
                    {
                        var member = x.Get<IMemberAnnotation>()?.Member;
                        if (member == null) return false;
                        return FilterDefault2(member);
                    });
                if(members == null) continue;
                items.AddRange(members);
            }
            annotations = items;
        }

        /// <summary> Gets the menu items associated with this Menu. </summary>
        public IEnumerable<AnnotationCollection> MenuItems
        {
            get
            {
                if (annotations == null)
                    generateAnnotations();
                return annotations;
            }
        }

        void IOwnedAnnotation.Read(object source)
        {
            this.source = source as object[] ?? new []{source};
            if (annotations == null) return;

            foreach (var model in models)
                model.Source = this.source;
            foreach(var annotation in annotations)
                annotation.Read();
        }

        void IOwnedAnnotation.Write(object source)
        {
            this.source = source as object[] ?? new []{source};
            if (annotations == null) return;
            foreach (var model in models)
                model.Source = this.source;
            foreach(var annotation in annotations)
                annotation.Write();
        }
    }

    /// <summary> Factory class for build menus. This can be used to extend member with additional menu annotations.</summary>
    public interface IMenuModelFactory : ITapPlugin
    {
        /// <summary> Create model should create exactly one IMenuItemModel whose members will be used in the MenuAnnotation. </summary>
        /// <param name="member">The member to show the menu for.</param>
        /// <returns>Shall return null if the model does not support the member.</returns>
        IMenuModel CreateModel(IMemberData member);
    }

    /// <summary>
    /// Base class for things in a menu item. 
    /// </summary>
    public interface IMenuModel
    {
        /// <summary> The source for the menu item. This may be more than one element.
        /// It is strongly recommended to explicitly implement this property. </summary>
        object[] Source { get; set; }
    }

    internal interface ITestStepMenuModel
    {
        ITestStepParent[] Source { get; }
        IMemberData Member { get; }
    }

    /// <summary>
    /// Icon names defined here others might be defined by plugins. These names are used by IconAnnotationAttribute. 
    /// </summary>
    public static class IconNames
    {
        const string Common = nameof(OpenTap)+ "." + nameof(IconNames) + ".";
        /// <summary> Parameterize command </summary>
        public const string Parameterize = Common + nameof(Parameterize);
        /// <summary> Parameterize command </summary>
        public const string ParameterizeOnTestPlan = Common + nameof(ParameterizeOnTestPlan);
        /// <summary> Parameterize command </summary>
        public const string ParameterizeOnParent = Common + nameof(ParameterizeOnParent);
        /// <summary> Unparameterize command </summary>
        public const string Unparameterize = Common + nameof(Unparameterize);
        /// <summary> Parameterize command </summary>
        public const string EditParameter = Common + nameof(EditParameter);
    }
}
