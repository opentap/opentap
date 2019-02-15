Plugin Development
==================

## TAP Plugin Object Hierarchy

At the very root of the TAP plugin class hierarchy is the **ValidatingObject** class. Items in blue are TAP base classes. The green, dotted rectangles are user extensions to those base classes.

 ![](ObjectHierarchy.PNG)
 
 Extending **ValidatingObject** are the **ComponentSettings**, **Resource**, and **TestStep** base classes. These are abstract classes and hence cannot be instantiated. They are used as base classes for other classes.
 
-	**ComponentSettings** is used for storing settings throughout the system. It contains properties to be set or lists of instruments, DUTs, and so on. ComponentSettings are visible via the various Settings menus in the GUIs.

-	**Resource** is a base class for the DUT, Instrument and ResultListener classes. Resources are often specified in the settings for a test step. Referencing a 'Resource' from a test step will cause it to automatically be opened upon test plan execution. 

-	**TestStep** does not inherit from Resource but inherits directly from ValidatingObject.

## Developing Using Interfaces

Developers may choose to NOT extend the TAP base classes, but instead to implement the required interfaces. You can inherit from as many interfaces as you want, but from only one class. The TestStepInterface.cs file provides an example of implementing ITestStep.

Developers implementing IResource (required by virtually all other TAP plugin classes), must ensure that the implementation of:

-	IResource.Open sets IsConnected to true, and 
-	IResource.Close sets IsConnected to false.

## Using Attributes in Plugins

Attributes are standard parts of C# and are used extensively throughout .NET. They have constructors (just like classes) with different signatures, each with required and optional parameters. For more information on attributes, refer to the MSDN C# documentation. 

For TAP, *type* information is not enough to fully describe what is needed from a property or class. For this reason, attributes are a convenient way to specify additional information. The TAP Engine, the GUI and CLI use reflection (which allows interrogation of attributes) extensively. Some attributes have already been shown in code samples in this document. 

### Attributes Used by TAP
TAP uses the following attributes: 

| **Attribute name** | **Description** |
| ---- | -------- |
| **AllowAnyChild**   | Used on *step class* to allow children of any type to be added.  |
| **AllowAsChildIn**   | Used on *step* to allow step to be inserted into a specific step type.  |
| **AllowChildrenOfType**   | Used on *step* to allow any children of a specific type to be added.  |
| **AvailableValues**   | Allows the user to select from items in a list. The list can be dynamically changed at run-time.   |
| **ColumnDisplayName**   |Indicates a property could be displayed as a column in the test plan grid.   |
| **DirectoryPath**  | Indicates a string property is a folder path.   |
| **Display**   | Expresses how a property is shown and sorted. Can also be used to group properties.   |
| **EnabledIf**   | Disables some controls under certain conditions.  |
| **FilePath**   | Indicates a string property is a file path.   |
| **Flags**   | Indicates the values of an enumeration represents a bitmask.   |
| **HandlesType**   | Indicates a IPropGridControlProvider can handle a certain type. Used by advanced programmers who are modifying GUI internals.   |
| **HelpLink**   | Defines the help link for a class or property.   |
| **IgnoreSerializer**  | Used on classes to ignore serialization. Useful for cases where a plugin implementation contains non-serializable members or types.   |
| **MacroPath**   | Indicates a setting should use MacroPath values, such as &lt;Name&gt; and %Temp%.   |
| **MetaData**   | A *property* marked by this attribute becomes metadata and will be provided to all result listeners. If a resource is used with this attribute (and *Allow Metadata Dialog* is enabled), a dialog prompts the user. This works for both TAP GUI and TAP CLI.  |
| **Output**   | Indicates a test step property is an output variable.  |
| **ResultListenerIgnore**   | Indicates a property that should not be published to ResultListeners.   |
| **Scpi**   | Identifies a method or enumeration value that can be handled by the SCPI class.    |
| **SettingsGroup**  | Indicates that component settings belong to a settings group (e.g. "Bench" for bench settings).   |
| **Unit**   | Indicates a unit displayed with the setting values. Multiple options exist.   |
| **VisaAddress**   | Indicates a property that represents a VISA address. The editor will be populated with addresses from all available instruments.   |
| **XmlIgnore**   | Indicates that a property should not be serialized.  |

For attribute usage examples, see the files in:

-	`TAP_PATH\SDK Examples\PluginDevelopment\TestSteps\Attributes`

Some of the commonly used attributes are described in the following sections.

## Best Practices for Plugin Development

The following recommendations will help you get your project off to a good start and help ensure a smooth development process.

-	You can develop one or many plugins in one Visual Studio project. The organization is up to the developer. Keysight recommends the following:
    -	Encapsulate your logic. Keeping all instrument logic inside the instrument class makes it possible to swap out instruments without changing TestSteps. For example, a TestStep plugin knows to call **MeasureVoltage**, and the instrument plugin knows how to get that measurement from its specific instrument.  
    -	You can put Instruments, DUTs, and TestSteps all in separate packages and create a "plug-and-play" type of interaction for test developers. For example, you can create test steps that make a measurement and plot a result. If done properly, the steps don't necessarily care which instrument gets the data or what type of device is being tested.
-	The namespace of a plugin should follow the folder structure. For example, a folder structure of **`MyCompany\Tap\Plugins\Category`** should use a namespace **MyCompany.Tap.Plugins.Category**.

-	Don't introduce general settings unless absolutely necessary. Instead try to move general settings to test steps (such as a parent step holding settings for a group of child steps) or to DUT or Instrument settings.

-	For DUTs, Instruments, and Result Listeners, set the Name property in the constructor, so that this Name appears in the Resource Bar.

-	Use the **Display** attribute (with a minimum of name and description) on properties and classes. This ensures good naming and tooltips in the GUI.

-	Use **Rules** for input validation to ensure valid data.

