# Translating Packages

Starting with OpenTAP version 9.29, it is possible to provide translation for
OpenTAP packages. OpenTAP can automatically generate a neutral translation file
in the .resx format. A neutral translation file is a template pre-filled with
language-neutral strings, based on the strings in the source code. The neutral
file will usually be in English, as this was the recommended plugin language
before localization support was introduced.

To generate a translation template, use the `tap sdk translate` CLI action. For
example, to generate a translation template for OpenTAP, run the CLI action
`tap sdk translate OpenTAP`. This creates the file `translations/OpenTAP.resx`
in the installation folder.

The currently selected language can be viewed and changed in the `Engine`
settings. OpenTAP detects the available languages by scanning the
`translations` folder in the installation directory for files with the file
extension `.resx`. `.resx` is XML markup, so it is very possible to edit by
hand, but we recommend using tools such as
[ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager).
ResXManager is convenient to use because it highlights potential errors such as
missing translations, inconsistent casing, inconsistent punctuation, missing
argumnents to format strings, and so on.

The language of a translation file is determined by parsing the [ISO 639
language code](https://en.wikipedia.org/wiki/List_of_ISO_639_language_codes) in
the filename before the `.resx` file extension. For example, the file
`OpenTAP.de.resx` would be treated as a German translation file, and
`OpenTAP.resx` would be treated as a neutral language file, due to the missing
extension.

OpenTAP generates the neutral translation file based on compiled DLL files by
using reflection. This is convenient because it makes it possible to create 3rd
party translations for existing plugins. But because we are working within the
limits of the C# reflection system, it is not possible for OpenTAP to
automatically discover everything. Here is a rough outline of the scanning
algorithm:

1. Scan all DLLs belonging to a package, and gather all plugin types. (Types
   which inherit from `ITapPlugin`. This is the case for all OpenTAP plugin
types, such as TestStep, ICliAction, Custom Settings, and many others.)
2. Scan all properties of each type. If the property is visible to the user,
   add it to the translation file. 2a. If the property has a DisplayAttribute
specified, add this display information to the translation file. 2b. If the
property does *not* have a DisplayAttribute, derive a display attribute and add
this information to the translation file.
3. If a property is visible and references a non-plugin type (an Enum, or a
   class embedded using `[EmbedProperties]` for example), add the referenced
type to the list of types to scan.

Here is a non-exhaustive list of things which are *not* automatically detected:
* Non-static strings (strings which are not Enum names, or strings from a
`[Display(...)]` attribute)
* Classes embedded using `[EmbedProperties]`, if the class does *not* belong to
the package being translated.
* Referenced enums, if the enum does *not* belong to the package being
translated.
* Enums and Classes which are not directly referenced by a plugin. Notably,
this includes classes used in `UserInput.Request()`

Because the scan is automated, it is also likely to pick up strings which will
never be presented to the user. If you are sure a string is not user-facing,
then there is no need to translate it. This will not cause anything to stop
working in OpenTAP. If the string is required, and no translation is available,
then OpenTAP will fall back to the neutral variant (based on source code).

## Handling non-static strings

By non-static string, we mean all strings which are not hard-coded in a
`[Display]` attribute. These strings normally fall in to one of two categories:

1. Information strings, which are logged through the Logging system
2. Dynamic content displayed in dialogs through the UserInput system

Because these strings can be dynamically generated at runtime, it is not
possible to automatically generate translation stubs for them. Instead, we have
added the `StringLocalizer` class. The `StringLocalizer` system is designed to
be similar to the existing `ComponentSettings` system.

Because `StringLocalizer` inherits from `ITapPlugin`, the translation generator
can create entries for all the strings defined in the `StringLocalizer` class.
Accessing the `Current` property on a `StringLocalizer` instance goes through
the OpenTAP translation system and returns an instance with translation strings
appropriate for the currently selected language.

See the example below of a test step using language-aware logging:


```csharp
public class MyTestStep : TestStep
{
    public class Strings : StringLocalizer<Strings>
    {
        public readonly string StringFormatTemplateExample = "Hello, {0}"; 
        public readonly string RunningTestStep = "Running test step!"; 
    }
    public override void Run()
    {
        Log.Info(Strings.Current.RunningTestStep);
        Log.Info(string.Format(Strings.Current.StringFormatTemplateExample, "World"));
    }
}
```

We recommend using the same strategy when locale-sensitive UserInput is required.

## Manually adding types

As noted above there are a couple of reasons why a type was not added to the translation file:

1. The type does not belong to the package being translated.
2. The type is not an `ITapPlugin`, and it is not referenced by another `ITapPlugin`.
3. The type is not public.

OpenTAP does not add types from other packages because we assume a translation
file provides translations for a single package. Looking inside the `.resx`
file reveals that translations are just key-value pairs of fully qualified
names and a corresponding value. As such, there is no rule against providing
translations for other packages, but the behavior when multiple translations
are available for the same string is not defined. If you are sure you want to
provide translations for another package, then you can freely add the required
translation key to your own translation file. 

The most common scenario when this is required would be if you embed a class
object from another package using `[EmbedProperties]`, or if you are using an
Enum from another package in a test step property.

Getting the lookup key may require a bit of sleuthing on your part. To get the
fully qualified type name of a given type to use in the translation file, you
can write a bit of C# code. For example,
`Console.WriteLine(typeof(ForeignType).FullName)`. You can then try to follow
the pattern from the other entries in your translation file.

## Translating UserInput

UserInput is crucial to translate because it is *always* user-facing.
Unfortunately, the signature of user inputs is `void UserInput.Request(object obj)`, so
there is no way for OpenTAP to detect what might get passed into this function
via reflection. Unfortunately, this makes it difficult to make translations of
UserInput from an old plugin package. But if you can create a new version of
the plugin, it is simple to add translation support by using a mix of
`StringLocalizer` and turning the request object into an `ITapPlugin` type by
manually inheriting the interface.

Here is an example of a translatable request object:

```csharp
// Inheriting from ITapPlugin causes the automatic translation template generator to create entries for this object.
// Note that the type must be public.
[Display("This is the title of the dialog")]
public class MyTranslatableRequestObject : ITapPlugin
{
    // This enum is automatically added to the translation table because it is referenced by `MyTranslatableRequestObject`, which is a plugin type
    public enum UserResponse
    {
        // Display attributes are optional here, but are convenient for providing descriptions
        [Display("Ok", Description: "Confirm the dialog")]
        Ok,
        [Display("Cancel", Description: "Cancel the dialog.")]
        Cancel
    }
    public class RequestStrings : StringLocalizer<RequestStrings>
    {
        public readonly string Message = "Here is my dialog text";
    }

    // Strings used in this dialog can be defined in a string localizer. This is a convenient way to
    // provide translations for visible, readonly strings, such as this message.
    [Layout(LayoutMode.FullRow)]
    [Browsable(true)] public string Message => RequestStrings.Current.Message;

    // Because the response object is an Enum, it is automatically added to the translation file.
    [Submit]
    [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
    public UserResponse Response { get; set; } = UserResponse.Cancel;
}

public class DialogStep : TestStep
{
    public override void Run()
    {
        // Because the translation work is done in the object class,
        // nothing extra is needed in the test step.
        var req = new MyTranslatableRequestObject();
        UserInput.Request(req);
        if (req.Response == MyTranslatableRequestObject.UserResponse.Ok)
        {
            // do something
        }
    }
}

```

## Translation tips

This section is dedicated to some general tips about working with translations in OpenTAP.

### Check in to source control

Because the translation format is textual XML files, we recommend checking
translation files into source control in the same repository where the rest of
your package lives. Since the neutral translation can be generated quickly with
a CLI action, you can easily automate this step by using a post-build action
(.csproj solution), or by creating some automation in your CI build. 

When used in combination with source control such as `git`, this makes it easy
to see if you have changed a type name (which would break existing translations
since the fully qualified type name is used as a lookup key), or if you forgot
to add translations for a language.

### Use tools for editing

We briefly mentioned
[ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager)
already, which has many convenient features.


### Do live translation with KS8400

OpenTAP detects changes to translation files while running, so it is convenient
to have KS8400 open while translating. This works nicely in combination with
ResXManager, since it automatically saves changes. Changes are detected
just-in-time when the translated string is requested by the UI, so e.g.
translations of a test step are normally detected the next time the step is
selected, so clicking back and forth between two steps in the UI will normally
update the step.
