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
        /// <summary> Creates a 'member' menu annotation. </summary>
        /// <param name="member"> Which member of an object should the menu be generated for.</param>
        /// <param name="type"> Set this if menu items for the type should be generated. </param>
        internal MenuAnnotation(IMemberData member, ITypeData type)
        {
            this.member = member;
            this.type = type;
        }

        /// <summary> Create a 'type' menu annotation. </summary>
        /// <param name="type"> The type for which the menu should be generated. </param>
        internal MenuAnnotation(ITypeData type) => this.type = type;

        /// <summary> The member used to construct menu items. This maybe be null if 'type' has a value.</summary>
        readonly IMemberData member;
        
        /// <summary> The type used to construct menu items. This maybe be null if 'model' has a value.</summary>
        readonly ITypeData type;
        
        List<AnnotationCollection> annotations;
        List<IMenuModel> models;
        object[] source;

        List<IMenuModel> getModels()
        {
            if (models == null)
            {
                models = new List<IMenuModel>();
                
                // get models for type menu annotations
                if (type != null)
                {
                    var factoryTypes = TypeData.GetDerivedTypes<ITypeMenuModelFactory>();
                    
                    foreach (var type in factoryTypes)
                    {
                        if (type.CanCreateInstance == false) continue;
                        try
                        {
                            var factory = (ITypeMenuModelFactory)type.CreateInstance();
                            IMenuModel model = factory.CreateModel(this.type);
                            if (model == null) continue;
                            model.Source = source;
                            if (model is IMenuModelState active && active.Enabled == false) continue;
                            models.Add(model);
                        }
                        catch
                        {

                        }
                    }
                }
                
                if(member != null)
                {
                    // get models for member menu annotations
                    var factoryTypes = TypeData.GetDerivedTypes<IMenuModelFactory>();
                    
                    foreach (var type in factoryTypes)
                    {
                        if (type.CanCreateInstance == false) continue;
                        try
                        {
                            var factory = (IMenuModelFactory)type.CreateInstance();
                            IMenuModel model = factory.CreateModel(member);
                            if (model == null) continue;
                            model.Source = source;
                            if (model is IMenuModelState active && active.Enabled == false) continue;
                            models.Add(model);
                        }
                        catch
                        {

                        }
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

    /// <summary> Factory class for build menus. This can be used to extend a type with additional menu annotations.</summary>
    public interface ITypeMenuModelFactory : ITapPlugin
    {
        /// <summary> Create model should create exactly one IMenuItemModel whose members will be used in the MenuAnnotation. </summary>
        /// <param name="type">The member to show the menu for.</param>
        /// <returns>Shall return null if the model does not support the member.</returns>
        ITypeMenuModel CreateModel(ITypeData type);
    }

    /// <summary> Base class for things in a menu item. </summary>
    public interface IMenuModel
    {
        /// <summary> The source for the menu item. This may be more than one element.
        /// It is strongly recommended to explicitly implement this property. </summary>
        object[] Source { get; set; }
    }

    /// <summary> menu model for members </summary>
    public interface IMemberMenuModel : IMenuModel
    {
        /// <summary> The member this menu model represents. </summary>
        IMemberData Member { get; }
    }
    
    /// <summary> menu model for types </summary>
    public interface ITypeMenuModel : IMenuModel
    {
        
    }
    

    /// <summary>
    /// A MenuModel that can signal about it's current state (Enabled/Disabled). This is useful to dynamically disable the menu model.
    /// </summary>
    public interface IMenuModelState : IMenuModel
    {
        /// <summary> Gets if the menu model is enabled or disabled. </summary>
        bool Enabled { get; }
    }

    /// <summary> Menu models for test step properties. </summary>
    public interface ITestStepMenuModel
    {
        /// <summary> The selected test steps. </summary>
        ITestStepParent[] Source { get; }
        /// <summary> Selected Member </summary>
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
        
        /// <summary> Parameterized icon name.</summary>
        public const string Parameterized = Common + nameof(Parameterized);
        /// <summary> Remove Parameter Command.</summary>
        public const string RemoveParameter = Common + nameof(RemoveParameter);
        /// <summary> Command to assign an output. </summary>
        public const string AssignOutput = Common + nameof(AssignOutput);
        /// <summary> Command to unassign an output. </summary>
        public const string UnassignOutput = Common + nameof(UnassignOutput);
        /// <summary> This is an output.</summary>
        public const string Output = Common + nameof(Output);
        /// <summary> This is an output.</summary>
        public const string OutputAssigned = Common + nameof(OutputAssigned);
        /// <summary> This is an input</summary>
        public const string Input = Common + nameof(Input);
        /// <summary> Add a mixin. </summary>
        public const string AddMixin = Common + nameof(AddMixin);
        /// <summary> Modifies a mixin. </summary>
        public const string ModifyMixin = Common + nameof(ModifyMixin);
        /// <summary> Remove a mixin. </summary>
        public const string RemoveMixin = Common + nameof(RemoveMixin);
        /// <summary> Assign an expression. </summary>
        public const string AssignExpression = Common + nameof(AssignExpression);
        /// <summary> Modify an expression. </summary>
        public const string ModifyExpression = Common + nameof(ModifyExpression);
        /// <summary> Remove expression. </summary>
        public const string RemoveExpression = Common + nameof(RemoveExpression);
        /// <summary> Assigned if a thing has an expression assigned. </summary>
        public const string HasExpression = Common + nameof(HasExpression);
    }
}
