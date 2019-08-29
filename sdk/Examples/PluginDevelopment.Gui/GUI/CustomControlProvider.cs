//Copyright 2012-2019 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
using Keysight.OpenTap.Wpf;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace OpenTap.Plugins.PluginDevelopment
{
    // This example shows how to create a custom IControlProvider implementation.
    // This is a WPF specific extension to the Editor application that allows
    // adding custom controls to property grids. 
    //
    // In this example it will be shown how to display a picture in the settings of a test step in a generic way.
    //
    // In practice it should rarely be necessesary to add custom controls, but for some use cases it can be convenient.
    // For more flexible and platform independent extensions, consider adding custom annotations instead.
    //

    /// <summary>
    /// This control provider displays a string as an image when the PictureAttribute is used.
    /// </summary>
    [Display("Picture Control Provider", Group: "Example")]
    public class PictureControlProvider : IControlProvider
    {
        /// <summary> Select a fairly high order value. </summary>
        public double Order => 21;

        /// <summary>
        /// If the member has a PictureAttribute and it support being a string value, then it can probably be loaded as an image. 
        /// </summary>
        /// <param name="annotation"></param>
        /// <returns></returns>
        public FrameworkElement CreateControl(AnnotationCollection annotation)
        {
            // [Picture] is used.
            bool ispic = annotation.Get<IMemberAnnotation>()?.Member.HasAttribute<PictureAttribute>() ?? false;
            if (ispic == false) return null;

            // It is something that acts like a string.
            var stringValueProvider = annotation.Get<IStringValueAnnotation>();
            if (stringValueProvider == null) return null;

            // create a WPF image control.
            var img = new Image();
            BindingOperations.SetBinding(img, Image.SourceProperty, 
                new Binding(nameof(IStringValueAnnotation.Value))
                {
                    Source = stringValueProvider,
                    Converter = new FileToImageConverter() // this converter converts a file path (local or web) to an image source.
                });
            
            // ** Performance Notice ** 
            // This way of showing an image is quite slow. If it is a large image from a slow network drive it could cause the GUI to lag whenever the step is selected.
            // consider adding in memory caching of that is the case. For brevity this is not shown here.
            //

            // Get updates when changes happen to the annotation. For example when the value has changed.
            var update = annotation.Get<UpdateMonitor>(true);
            if(update != null)
            {
                // When the value changes explicitly reload the SourceProperty of the image.
                update.RegisterSourceUpdated(img, () => img.GetBindingExpression(Image.SourceProperty).UpdateTarget());
            }

            return img;
        }
        /// <summary> Slow but simple URI to image converter. </summary>
        class FileToImageConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                string path = (string)value;
                if (string.IsNullOrWhiteSpace(path)) return null;
                try
                {
                    // this could also be a url.
                    if (File.Exists(Path.GetFullPath(path)))
                        path = Path.GetFullPath(path);
                }
                catch
                {

                }
                try
                {
                    return new BitmapImage(new Uri(path), new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.CacheIfAvailable));
                }
                catch
                {
                    return null;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException(); // this is not needed.
            }
        }

    }

    /// <summary> The PictureAttribute is used to mark that a string should be shown as a picture. </summary>
    public class PictureAttribute : Attribute
    {

    }

    [Display("Step With Picture", "This test step shows a picture", Groups: new[] { "Examples", "Plugin Development", "GUI" })]
    public class StepWithPicture : TestStep
    {
        [Picture] // 'Picture' should be shown as a picture.
        [Layout(LayoutMode.FullRow)] // Hide the name of the setting.
        public string Picture { get; set; } 

        /// <summary> The path to the picture, this can be a local file path or a HTTP address. </summary>
        [FilePath]
        [Display("Path")]
        public string Path
        {
            get { return Picture; }
            set { Picture = value; }
        }

        [Browsable(true)]
        public void ListAllControlProviders()
        {
            // it can sometimes be useful to list all plugins of a kind to see the order in which they are used.
            Log.Info("Listing Control Providers:");
            foreach (var controlProvider in PluginManager.GetPlugins<IControlProvider>().Select(x => (IControlProvider)Activator.CreateInstance(x)).ToArray().OrderBy(x => x.Order))
            {
                Log.Info("{0} : {1}", controlProvider.Order, controlProvider.ToString());
            }
        }

        public override void Run()
        {

        }
    }
}
