using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Keysight.OpenTap.Wpf;
using OpenTap;
namespace PluginDevelopment.Gui.OperatorPanel
{
    public class UserInputRequestData
    {
        readonly Action<UserInputRequestData> submitCallback;
        public ManualResetEventSlim WaitHandle { get; } = new ManualResetEventSlim(false);
        public UserInputRequestData(object userInputObject, Action<UserInputRequestData> submitCallback)
        {
            this.submitCallback = submitCallback;
            UserInputObject = userInputObject;
        }
        

        void GenerateElements()
        {
            var upd = new UpdateMonitor();
            upd.OnCommit += InvokeSubmit;
            var annotation = AnnotationCollection.Annotate(UserInputObject, upd);
            var rootGroup = GenericGui.CreateGroupUI(annotation.Get<IMembersAnnotation>().Members, "", 0);
            Grid grd = new Grid();
            grd.ColumnDefinitions.Add(new ColumnDefinition{Width = GridLength.Auto});
            grd.ColumnDefinitions.Add(new ColumnDefinition{Width = new GridLength(1.0, GridUnitType.Star)});
            int gridRow = 0;
            void genRec(GroupUi group)
            {
                var items = group.Items.OfType<ItemUi>().ToArray();
                if (items.Length > 0)
                {
                    var label = new Label
                    {
                        Content = group.Name
                    };
                    grd.Children.Add(label);
                    Grid.SetRow(label, gridRow++);
                    Grid.SetColumnSpan(label, 2);
                    foreach (var item in items)
                    {
                        var name = new TextBlock
                        {
                            Text = item.GetName(),
                            MinWidth = 30
                        };
                        var control = item.Control;
                        Grid.SetRow(name, gridRow);
                        Grid.SetRow(control, gridRow++);
                        Grid.SetColumn(control, 1);
                        
                        grd.Children.Add(name);
                        grd.Children.Add(control);
                    }
                }
                var subGroups = group.Items.OfType<GroupUi>().ToArray();
                foreach (var subGroup in subGroups)
                {
                    genRec(subGroup);
                }
            }
            

            genRec(rootGroup);
            
            bool hasSubmitItem = rootGroup.Sequential.OfType<ItemUi>()
                .Any(x => x.IsVisible && x.Item.Member.HasAttribute<SubmitAttribute>());
            if (hasSubmitItem == false)
            {
                var submitButton = new Button
                {
                    Content = "OK"
                };
                submitButton.Click += (s, e) => InvokeSubmit();
                Grid.SetRow(submitButton, gridRow++);
                grd.Children.Add(submitButton);

            }
            
            for(int i = 0; i < gridRow; i++)
                grd.RowDefinitions.Add(new RowDefinition());
            
            
            this.elements = new FrameworkElement[]{grd};
            
        }

        void InvokeSubmit()
        {
            submitCallback?.Invoke(this);
            WaitHandle.Set();
        }

        FrameworkElement[] elements;

        public IReadOnlyCollection<FrameworkElement> Elements
        {
            get
            {
                if (elements == null)
                {
                    GenerateElements();
                }
                return elements;
            }
        }
        public object UserInputObject { get; }
        
        
    }
}
