 Getting Started in Visual Studio
================================

TAP plugin development begins with the **Keysight TAP Plugin** template, a Visual Studio extension installed with the TAP SDK. This template:

-	Contains TAP plugin classes and gives you a head start to plugin development by setting up the project and providing code skeletons.
-	Copies the project's output to the location specified by the TAP_PATH environment variable, which enables TAP to load the plugins during development.

## View the Visual Studio Example Projects
Before you create your own project, look at the projects and files in **`TAP_PATH\SDK Examples`**. This folder provides code for example DUT, instrument and test step plugins. First-time TAP developers should browse and build the projects, then use the TAP GUI to view the example DUTs, instruments and test steps. 

SDK Examples contains the following:

| **Folder**  | **Description** |
| -------- | --------  |
| **`ExamplePlugin\ExamplePlugin.csproj`**                           | Creates a plugin package that contains one DUT resource, one Instrument resource, and one test step.   |
|**`PluginDevelopment\PluginDevelopment.csproj`**                    | Creates a plugin package that contains several test steps, two DUT resources, four Instrument resources, and two result listeners.                                              |
|**`TestPlanExecution\BuildTestPlan.Api\BuildTestPlan.Api.csproj`**  | Shows how to build, save and execute a test plan using TAP API.  |
|**`TestPlanExecution\RunTestPlan.Api\RunTestPlan.Api.csproj`**      | Shows how to load and run a test plan using TAP API.   |

See the following sections:

-	[Build and View the Example Plugin Project](#build-and-view-the-example-plugin-project) provides a quick overview on how to build this plugin and see its contents.
-	[Contents of the Plugin Development Project](#contents-of-the-plugin-development-project)  shows the many resources and test steps in this project. Follow the process in the previous section to build and view the project.
-	[Create a Project with the TAP Plugin Template](#create-a-project-with-the-tap-plugin-template) describes how to get started on your own project.

### Build and View the Example Plugin Project
The Example Plugin project creates a basic plugin package that contains a test step, a DUT resource, and an instrument resource. Follow these steps to build the project and view the results in TAP: 

1. In Visual Studio:

    a.   Open **ExamplePlugin.csproj**. In the Solution Explorer, notice the three .cs files. These will create an instrument resource, a DUT resource, and a test step:
    
    ![](ExamplePlugin_img1.PNG)

    
    b.	Select Debug > Start Debugging to build the solution and open it in TAP.
    
2. 	In TAP:

    a.	Click the **+** icon, and add the **Measure Peak Amplitude** step:
    
    ![](ExamplePlugin_img2.PNG)
    
    b.	View the step settings. Notice, the step requires a Generator instrument:
    
     ![](ExamplePlugin_img3.PNG)
     
    c. In the **Resource bar**, at the bottom of the GUI, click **DUTs Add New** or if you already have DUTs configured click on one of those. This launches the **Bench Settings** window. Click the **+** button and in the **Add New Dut** window add the **Low Pass Filter** DUT, then close the window:
    
    ![](ExamplePlugin_img4.PNG)
    
    The **Bench Settings** window can also be launched from Settings > Bench > DUT.
    
    d.	In the **Bench Settings** window, notice the **Filter** DUT lets users to specify an ID and a Comment: 
    
    ![](ExamplePlugin_img5.PNG)
    
    e. 	In **Bench Settings**, click the **Instruments** tab, then click the + button. Add the **Generator** instrument, then close the window:
    
    ![](ExamplePlugin_img6.PNG)
    
    f.	Notice the **Generator** instrument allows users to specify the Visa Address:
    
    ![](ExamplePlugin_img7.PNG)
    
3. 	Close TAP. Do not save any files.
4. 	If you want to remove the examples, go to your TAP folder and delete:
    - Example.Tap.Plugins.ExamplePlugin.dll
    - Example.Tap.Plugins.ExamplePlugin.pdb

### Contents of the Plugin Development Project

Follow the same process to build and view the **Plugin Development** project (`TAP_PATH\SDK Examples\PluginDevelopment\PluginDevelopment.csproj`), which contains many examples:

-	Several **test step** categories that contain a number of steps:

![](PluginDev_img1.PNG)

- 	Two **DUT** resources:
 	
![](PluginDev_img2.PNG)

-	Six Instrument resources:
	
![](PluginDev_img3.PNG)

-	Two Results Listeners:
	
![](PluginDev_img4.PNG)

To remove these examples, go to your TAP folder and delete the **Example.Tap.Plugins.PluginDevelopment.dll** and **Example.Tap.Plugins.PluginDevelopment.pdb** files.

## Create a Project with the TAP Plugin Template

The **TAP Plugin Template** is a Visual Studio extension which is installed with the SDK. To start a new Visual studio project that will contain TAP plugin classes:

1. 	Select **File > New > Project**. Expand the **Visual C#** templates and select the **Keysight TAP Plugin** template when you create your project:

![](CreateProject_img1.PNG)

This template:

- 	Copies the project's output to the location specified by the TAP_PATH environment variable. This enables TAP to load the plugins during development. Note that TAP_PATH is typically either:
    -	`C:\Program Files\Keysight\TAP8` (64-bit systems)
    -	`C:\Program Files (x86)\Keysight\TAP8` (32-bit systems)
-   Includes a "packaging" step (see [Plugin Packaging and Versioning](../Plugin Packaging and Versioning/Readme.md) for details).
- 	Includes a TestStep class (Step.cs). If you are not creating a test step, delete this class.

2. To create a particular plugin or add another plugin to your project:

    a.	Select **Project > Add New Item**.
    
    b.	Enter **Tap** in the search field.
    
    c.	Select the appropriate plugin.
    
![](CreateProject_img2.PNG)
