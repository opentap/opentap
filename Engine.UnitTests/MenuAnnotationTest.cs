using System.ComponentModel;
using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
namespace OpenTap.UnitTests
{
    public class MenuAnnotationTest
    {
        public class CustomTestPlanMenuModel: ITypeMenuModel
        {

            object[] source;
            object[] IMenuModel.Source
            {
                get => source;
                set => source = value;
            }

            [Browsable(true)]
            public void PresetPlan()
            {
                foreach (var plan in source.Cast<TestPlan>())
                {
                    plan.ChildTestSteps.Clear();
                    plan.ChildTestSteps.Add(new DelayStep());
                }
            }
            
        }

        public class CustomTypeMenuFactory :  ITypeMenuModelFactory
        {
          
            ITypeMenuModel ITypeMenuModelFactory.CreateModel(ITypeData type)
            {
                if (type.DescendsTo(typeof(TestPlan)))
                    return new CustomTestPlanMenuModel();
                return null;
            }
        }
        
        [Test]
        public void CustomMenuModelTest()
        {
            var plan = new TestPlan();
            var a = AnnotationCollection.Annotate(plan);
            var x = a.Get<MenuAnnotation>();
            var items = x.MenuItems.ToArray();
            var callAnnotation = items.First(x => x.Name == nameof(CustomTestPlanMenuModel.PresetPlan));
            var invoke = callAnnotation.Get<IMethodAnnotation>();
            invoke.Invoke();

            Assert.IsTrue(plan.ChildTestSteps[0] is DelayStep);
            
            var memberMenu = a.Get<IMembersAnnotation>().Members.First().Get<MenuAnnotation>().MenuItems.First(x => x.Name == nameof(CustomTestPlanMenuModel.PresetPlan));
            Assert.IsNotNull(memberMenu);
            //Assert.IsTrue(memberMenu.MenuItems.Any(x => x.GetType() == ));
        }
    }
}
