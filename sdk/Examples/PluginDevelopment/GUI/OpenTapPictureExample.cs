using System;
using System.ComponentModel;

namespace OpenTap.Plugins.PluginDevelopment.GUI
{
    [Display("OpenTAP Picture Example", Groups: new[] { "Examples", "Plugin Development", "GUI" },
        Description: "This example shows how to use OpenTap Pictures.")]
    public class OpenTapPictureExample : TestStep
    {
        enum ResponseEnum
        {
            [Display("I pressed the button, proceed!")]
            Yes,
            [Display("Something is wrong, abort!")]
            No
        }

        [Display("Confirm instrument configuration")]
        class PictureDialogRequest
        {
            public PictureDialogRequest(string question)
            {
                Question = question;
            }

            /// <summary>
            /// The layout of a picture can be controlled using the Layout attribute, just like other UserInput members
            /// </summary>
            [Layout(LayoutMode.FullRow)]
            [Display("Picture", Order: 1)]
            public Picture Picture { get; set; }

            [Layout(LayoutMode.FullRow, rowHeight: 2)]
            [Browsable(true)]
            [Display("Message", Order: 2)]
            public string Question { get; }

            [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
            [Submit]
            public ResponseEnum response { get; set; }

        }
        public override void Run()
        {
            // Open a dialog with the picture settings defined in the test step
            var request = new PictureDialogRequest(Question)
            {
                Picture = Picture,
            };

            UserInput.Request(request);

            if (request.response == ResponseEnum.No)
            {
                UpgradeVerdict(Verdict.Error);
            }
        }

        public string Question { get; set; } = "Press the button labeled 'A' in the figure.";

        /// <summary>
        /// Instantiate an OpenTAP picture with some default picture
        /// These can be controlled by other test step properties if they should be configurable, or they can be hardcoded values
        /// </summary>
        public Picture Picture { get; } = new Picture()
        {
            Source = "GUI\\SomeInstrument.png",
            Description = "The instrument we are controlling."
        };

        /// <summary>
        /// Control the source of the picture with a regular test step property
        /// </summary>
        [Display("Source", "The source of the picture. This can be a URL or a file path.", "Picture", Order: 2,
            Collapsed: true)]
        [FilePath(FilePathAttribute.BehaviorChoice.Open)]
        public string PictureSource
        {
            get => Picture.Source;
            set => Picture.Source = value;
        }

        /// <summary>
        /// Control the description of the picture with a regular test step property
        /// </summary>
        [Display("Description", "A description of the picture. " +
                                "This can be helpful to set if the picture cannot be loaded for some reason, " +
                                "or if the test plan is not running in a GUI environment.",
            "Picture", Order: 3, Collapsed: true)]

        public string PictureDescription
        {
            get => Picture.Description;
            set => Picture.Description = value;
        }
    }
}