Attributes
=========

Attributes are standard parts of C# and are used extensively throughout .NET. They have constructors (just like classes) with different signatures, each with required and optional parameters. For more information on attributes, refer to the MSDN C# documentation. 

For OpenTAP, *type* information is not enough to fully describe what is needed from a property or class. For this reason, attributes are a convenient way to specify additional information. OpenTAP, the GUI Editor and CLI use reflection (which allows interrogation of attributes) extensively. Some attributes have already been shown in code samples in this document. 

## Attributes Used by OpenTAP

OpenTAP uses the following attributes: 

| **Attribute name** | **Description** |
| ---- | -------- |
| **AllowAnyChild**   | Used on *step class* to allow children of any type to be added.  |
| **AllowAsChildIn**   | Used on *step* to allow step to be inserted into a specific step type.  |
| **AllowChildrenOfType**   | Used on *step* to allow any children of a specific type to be added.  |
| **AvailableValues**   | Allows the user to select from items in a list. The list can be dynamically changed at run-time.   |
| **ColumnDisplayName**   |Indicates a property could be displayed as a column in the test plan grid.   |
| **CommandLineArgument** | Used on a property in a class that implements the *ICliAction* interface, to add a command line argument/switch to the action (e.g. "--verbose"). |
| **DeserializeOrder** | Can be used to control the order in which properties are deserialized. |
| **DirectoryPath**  | Indicates a string property is a folder path.   |
| **Display**   | Expresses how a property is shown and sorted. Can also be used to group properties.   |
| **EnabledIf**   | Disables some controls under certain conditions.  |
| **ExternalParameter** | Indicates that a property on a TestStep (a step setting) should be a External Parameter by default when added to a test plan.   |
| **FilePath**   | Indicates a string property is a file path.   |
| **Flags**   | Indicates the values of an enumeration represents a bitmask.   |
| **HandlesType**   | Indicates a IPropGridControlProvider can handle a certain type. Used by advanced programmers who are modifying the GUI editor internals.   |
| **HelpLink**   | Defines the help link for a class or property.   |
| **Layout** | Used to specify the desired layout of the element in the user interface. |
| **MacroPath**   | Indicates a setting should use MacroPath values, such as &lt;Name&gt; and %Temp%.   |
| **MetaData**   | A *property* marked by this attribute becomes metadata and will be provided to all result listeners. If a resource or a component setting is used with this attribute (and *Allow Metadata Dialog* is enabled), a dialog prompts the user. This works for both the GUI Editor and the OpenTAP CLI.  |
| **Output**   | Indicates a test step property is an output variable.  |
| **ResourceOpen** | Used to control how and if referenced resources are opened. This attribute is attached to a resource property. Three modes are available, Before, InParallel and Ignore. Refer to the API documentation for more information. |
| **ResultListenerIgnore**   | Indicates a property that should not be published to ResultListeners.   |
| **Scpi**   | Identifies a method or enumeration value that can be handled by the SCPI class.    |
| **SettingsGroup**  | Indicates that component settings belong to a settings group (e.g. "Bench" for bench settings).   |
| **SuggestedValues** | Marks the property value can be selected from a list in the OpenTAP Editor. Points to another property that contains the list of suggested values. | 
| **TimeSpanFormat** | Attribute applicable to a property of type 'TimeSpan' to display the property value in a human readable format in the user interface. |
| **UnnamedCommandLineArgument** | When used on a property in a class that implements the *ICliAction* interface, the property becomes an unnamed parameter to the command line argument. |
| **Unit**   | Indicates a unit displayed with the setting values. Multiple options exist.   |
| **VisaAddress**   | Indicates a property that represents a VISA address. The editor will be populated with addresses from all available instruments.   |
| **XmlIgnore**   | Indicates that a property should not be serialized.  |

For attribute usage examples, see the files in:

-	`TAP_PATH\Packages\SDK\Examples\PluginDevelopment\TestSteps\Attributes`

Some of the commonly used attributes are described in the following sections. For more details on the attributes see OpenTapApiReference.chm.

## Attribute Details

### Display
The **Display** attribute is the most commonly used OpenTAP attribute. This attribute:

-	Can be applied to class names (impacting the appearance in dialogs, such as the Add New Step dialog), or to properties (impacting appearance in the Step Settings Panel).
-	Has the following signature in its constructor:

```csharp
(string Name, string Description = "", string Group = null, double Order = 0D, bool Collapsed = false, string[] Groups = null)
```

-	Requires the **Name** parameter. All the other parameters are optional.
-	Supports a **Group** or **Groups** of parameters to enable you to organize the presentation of the items in the Test Automation Editor.

The parameters are ordered starting with the most frequently used parameters first. The following examples show example code and the resulting Editor appearance:

```csharp
// Defining the name and description.
[Display("MyName", "MyDescription")]
public string NameAndDescription { get; set; }
```

![](./appendix_img1.png)

See the examples in **`TAP_PATH\Packages\SDK\Examples\PluginDevelopment\TestSteps\Attributes`** for different uses of the Display attribute.

Display has the following parameters:

| **Attribute**   | **Required** |**Description** |
| --      | --     |--------  |
| **Name**        | Required |The name displayed in the Editor. If the Display attribute is not used, the **property name** is used in the Editor. |
|**Description**  | Optional |Text displayed in tools tips, dialogs and editors in the Editor.|
|**Group/Groups** | Optional |Specifies an item’s group. Use **Group** if the item is in a one-level hierarchy or **Groups** if the item is in a hierarchy with two or more levels. The hierarchy is specified by the left-to-right order of the string array. Use either Group or Groups; do not use both. Groups is preferred. Groups are ordered according to the average order value of their child items. For test steps, the top-level group is always ordered alphabetically. Syntax: `Groups: new[] { "Group" , "Subgroup" }`|
|**Order**        | Optional |Specifies the display order for an item. Note that **Order** is supported for settings and properties, such as test step settings, DUT settings, and instrument settings. It does not support types: test steps, DUTs, instruments. These items are ordered alphabetically, with groups appearing before ungrouped items. Order is of type double, and can be negative. Order’s behavior matches the Microsoft behavior of the *Display.Order* attribute. If order is not specified, a default value of -10,000 is assumed. Items (ungrouped or within a group) are ranked so that items with lower order values precede those with higher values; alphabetically if order values are equal or not specified. To avoid confusion, we recommend that you set the order value for ungrouped items to negative values so that they appear at the top and Grouped items to a small range of values to avoid conflicts with other items (potentially specified in base classes). For example, if *Item A* has order = 100, and *Item B* has order = 50, *Item B* is ranked first.|

### Embedded Attribute
The EmbeddedAttribute can be used to embed the members of one object into the owner object. This hides the embedded object from reflection, but shows the embedded objects members instead. This can be used to let objects share common settings and code without using inheritance.

### EnabledIf Attribute
The **EnabledIf** attribute disables or enables settings properties based on other settings (or other properties) of the same object. The decorated settings reference another property of an object by name, and its value is compared to the value specified in an argument. Properties that are not settings can also be specified, which allows the implementation of more complex behaviors.

For test steps, if instrument, DUTs or other resource properties are disabled, the resources will not be opened when the test plan starts, However, if another step needs them they will still be opened.

The **HideIfDisabled** optional parameter of EnabledIf makes it possible to hide settings when they are disabled. This is useful to hide irrelevant information from the user.

Multiple EnabledIf statements can be used at the same time. In this case all of them must be enabled (following the logical *AND* behavior) to make the setting enabled. If another behavior is wanted, an extra property (hidden to the user) can be created and referenced to implement another logic. In interaction with HideIfDisabled, the enabling property of that specific EnabledIf attribute must return false for the property to be hidden.

In the following code, BandwidthOverride is enabled when **Radio Standard** = GSM.

```csharp
public class EnabledIfExample : TestStep
{
    #region Settings

    // Radio Standard to set DUT to transmit.
    [Display("Radio Standard", Group: "DUT Setup", Order: 1)]
    public RadioStandard Standard { get; set; }

    // This setting is only used when Standard == LTE || Standard == HCDHA.
    [Display("Measurement Bandwidth", Group: "DUT Setup", Order: 2.1)]
    [EnabledIf("Standard", RadioStandard.Lte, RadioStandard.Wcdma)]
    public double Bandwidth { get; set; }

    // Only enabled when the Standard is set to GSM.
    [Display("Override Bandwidth", Group: "Advanced DUT Setup", Order: 3.1)]
    [EnabledIf("Standard", RadioStandard.Gsm, HideIfDisabled = true)]
    public bool BandwidthOverride { get; set; }

    // Only enabled when both Standard = GSM, and BandwidthOverride property is enabled.
    [Display("Override Bandwidth", Group: "Advanced DUT Setup", Order: 3.1)]
    [EnabledIf("Standard", RadioStandard.Gsm, HideIfDisabled = true)]
    [EnabledIf("BandwidthOverride", true, HideIfDisabled = true)]
    public double ActualBandwidth { get; set; }

    #endregion Settings
}
```

When **Radio Standard** is set to GSM in the step settings, both **Override Bandwidth** options are then displayed:

![](./appendix_img2.png)

For an example, see `TAP_PATH\Packages\SDK\Examples\PluginDevelopment\TestSteps\Attributes\EnabledIfAttributeExample.cs`.

### Flags Attribute
The **Flags** attribute is a C# attribute used with enumerations. This attribute indicates that an enumeration can be treated as a *bit field* (meaning, elements can be combined by bitwise OR operation). The enumeration constants must be defined in powers of two (for example 1, 2, 4, …).

Using the Flags attribute results in a multiple select in the Editor, as shown below:

![](./appendix_img3.png)

### FilePath and DirectoryPath Attributes 
The FilePath and DirectoryPath attributes can be used on a string-type property to indicate the string is a file or a folder system path. When this attribute is present, the Editor displays a browse button allowing the user to choose a file or folder. These attributes can be used as follows:

```csharp
[FilePath]
public string MyFilePath { get; set; }
```

This results in the following user control in the Editor:

![](./appendix_img4.png)

The DirectoryPath attribute works the same as the FilePath attribute, but in the place of a file browse dialog, a directory browse dialog opens when the browse ('...') button is clicked.

The FilePath attribute supports specifying file type as well.

It can be done by writing the file extension as such:

```csharp
[FilePath(FilePathAttribute.BehaviorChoice.Open, "csv");
```

Or it can be done by specifying a more advanced filter expression as shown below.

```csharp
[FilePath(FilePathAttribute.BehaviorChoice.Open, "Comma Separated Files (*.csv)|*.csv| Tab Separated Files (*.tsv) | *.tsv| All Files | *.*")]
```

The syntax works as follows:
```[Name_1] | [file extensions 1] | [Name_2] | [file extensions 2] ...```

Each filter comes in pairs of two, a name and a list of extensions. The name of a filter can be anything, excluding the '|' character. It normally contains the name of all the included file extensions, for example "Image Files (\*.png, \*.jpg)". The file extensions is normally not seen by the user, but should contain all the supported file extensions as a semi-colon separated list. Lastly, it is common practice to include the 'AllFiles | \*.\*' part, which makes it possible for the user to override the known filters and manually select any kind of file.

### Submit Attribute
This attribute is used only for objects used together with UserInput.Request. It is used to mark the property that finalizes the input. For example this could be used with an enum to add an OK/Cancel button, that closes the dialog when clicked. See the example in UserInputExample.cs for an example of how to use it.

### Layout Attribute
LayoutAttribute is used to control how settings are arranged in graphical user interfaces. It can be used to control the height, width and positioning of settings elements. Use this with the Submit attribute to create a dialog with options like OK/Cancel on the bottom. See UserInputExample.cs for an example.

### MetaData Attribute
Metadata is a set of data that describes and gives information about other data. The Metadata attribute marks a property as metadata. 

OpenTAP can prompt the user for metadata. Two requirements must be met:

-	The MetaData attribute is used and the promptUser parameter is set to *true*
-	The *Allow Metadata Dialog* property in **Settings > Engine**, is set to *true*

If both requirements are met, a dialog (in the Editor) or prompt(in OpenTAP CLI) will appear on each test plan run to ask the user for the appropriate values. This works for both the Editor and the OpenTAP CLI. An example of where metadata might be useful is when testing multiple DUTs in a row and the serial number must be typed in manually.

Values captured as metadata are provided to all the result listeners, and can be used in the macro system. See SimpleDut.cs for an example of the use of the MetaData attribute.

### Unit Attribute
The Unit attribute specifies the units for a setting. The Editor displays the units after the value (with a space separator). Compound units (watt-hours) should be hyphenated. Optionally, displayed units can insert engineering prefixes.

See the `TAP_PATH\Packages\SDK\Examples\PluginDevelopment\TestSteps\Attributes\UnitAttributeExample.cs` file for an extensive example.

### XmlIgnore Attribute
The XmlIgnore attribute indicates that a setting should not be serialized. If XmlIgnore is set for a property, the property will not show up in the Editor. If you want to NOT serialize the setting AND show it in the Editor, then use the Browsable(true) attribute, as shown below:

```csharp
// Editable property not serialized to XML 
[Browsable(true)]
[XmlIgnore]
public double NotSerializedVisible { get; set; } 
```

Properties that represent instrument settings (like the one below) should not be serialized as they will result in run-time errors:

```csharp
[XmlIgnore]
public double Power
{ 
    set; { ScpiCommand(":SOURce:POWer:LEVel:IMMediate:AMPLitude {0}", value) }
    get; { return ScpiQuery<double>(":SOURce:POWer:LEVel:IMMediate:AMPLitude?"); }
}
```
