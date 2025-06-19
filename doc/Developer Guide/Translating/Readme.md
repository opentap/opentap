# Translating Packages

Starting with OpenTAP version 9.29, it is possible to provide translation for
OpenTAP packages. OpenTAP can automatically generate a neutral translation file
in the .resx format. A neutral translation file is a template pre-filled with
language-neutral strings, based on the strings in the source code. The neutral
file will usually be in English, as this was the recommended plugin language
before localization support was introduced.

To generate a translation template, use the `tap sdk translate` CLI action from
the SDK package. For example, to generate a translation template for OpenTAP,
run the CLI action `tap sdk translate OpenTAP`. This creates the file
`Resources/OpenTAP.resx` in the installation folder.

The currently selected language can be viewed and changed in the `Engine`
settings. OpenTAP detects the available languages by scanning the
`translations` folder in the installation directory for files with the file
extension `.resx`. `.resx` is XML markup, so it is very possible to edit by
hand, but we recommend using tools such as
[ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager).
ResXManager is convenient to use because it highlights potential errors such as
missing translations, inconsistent casing, inconsistent punctuation, missing
arguments to format strings, and so on.

> NOTE: The language selector will be hidden if no language files are available.

The language of a translation file is determined by parsing the [ISO 639
language code](https://en.wikipedia.org/wiki/List_of_ISO_639_language_codes) in
the filename before the `.resx` file extension. For example, the file
`OpenTAP.de.resx` would be treated as a German translation file, and
`OpenTAP.resx` would be treated as a neutral language file, due to the missing
extension.

> NOTE: Even if the Neutral language is selected, OpenTAP will not perform
> string lookups in neutral .resx files. The content of neutral resx files is
> derived from the source code, but it is possible for resx files and binaries
> to go out of sync, so the binary files are assumed to be authoritative.

OpenTAP generates the neutral translation file based on compiled DLL files by
using reflection. This is convenient because it makes it possible to create 3rd
party translations for existing plugins. But because we are working within the
limits of the C# reflection system, it is not possible for OpenTAP to
automatically discover everything. Here is a rough outline of the scanning
algorithm:

1. Scan all DLLs belonging to a package, and gather all plugin types. (Types
   which inherit from `ITapPlugin`. This is the case for all OpenTAP plugin
types, such as TestStep, ICliAction, Custom Settings,, IStringLocalizer, and many others.)
2. Scan all properties of each type. If the property is visible to the user,
   add it to the translation file. If the property has a DisplayAttribute
specified, add this display information to the translation file. If the
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
added the `IStringLocalizer` interface.

Because `IStringLocalizer` inherits from `ITapPlugin`, the translation
generator can create entries for strings defined in classes inheriting
`IStringLocalizer`. Strings provided by an `IStringLocalizer` must either call
the `this.Translate()`, or `this.TranslateFormat()` methods from a property
getter.

See the example below of a test step using language-aware logging:


```csharp
public class MyTestStep : TestStep, IStringLocalizer
{
    // Some configurable step option
    public int StepSetting { get; set; } = 123;
    // Translate a fixed string.
    public string InfoMessageExample => this.Translate("Hello Translations");
    // Translate a string with format parameters. This is recommended over manually calling string.Format() because
    // TranslateFormat validates the template parameters in the translated string.
    public string StringFormatExample => this.TranslateFormat("StepSetting is '{0}'", arguments: [StepSetting]);

    public override void Run()
    {
        Log.Info(InfoMessageExample);
        Log.Info(StringFormatExample);
    }
}
```

We recommend using the same strategy for UserInput implementations.

> NOTE: The resx template generator must be able to instantiate
> `IStringLocalizer` implementations in order to detect the required
> translation strings. This requires the implementation to have an empty
> constructor. This is also a requirement for `TestStep` plugins, and is
> generally the case for all OpenTAP plugin types.

### How does it work?

The syntax is a bit magical, and is designed to make common translation
scenarios as simple as possible. OpenTAP defines an extension method on the
interface `IStringLocalizer`, and makes use of the `[CallerMemberName]`
attribute. The real signature of the `Translate()` method is this:

```csharp 
public static string Translate(this IStringLocalizer stringLocalizer, string neutral, [CallerMemberName] string key = null, CultureInfo language = null)
```

So the line `this.Translate("Hello Translations")` is really a shorthand for
`OpenTap.Translations.Translations.Translate(this, "Hello Translations",
nameof(InfoMessageExample))`. The template generator works by creating an
instance of the `IStringLocalizer`, and looping through all of its public
properties and calling the getter function. This causes the `Translate()`
function to be called, which the template generator can use to derive the full
lookup key (`stringLocalizer.GetType().FullName + "." + key`).

This implementation detail is not crucial to understand to use the system, but
explains why the `Translate` call must be used from a property getter.


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
Unfortunately, the signature of user inputs is `void UserInput.Request(object
obj)`, so there is no way for OpenTAP to detect what might get passed into this
function via reflection. Unfortunately, this makes it difficult to make
translations of UserInput from an old plugin package. But if you can create a
new version of the plugin, it is simple to add translation support by making
the request object inherit the `IStringLocalizer` interface.

Here is an example of a translatable request object:

```csharp
// Inheriting from ITapPlugin causes the automatic translation template generator to create entries for this object.
// Note that the type must be public.
[Display("This is the title of the dialog")]
public class MyTranslatableRequestObject : IStringLocalizer
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

    // Strings used in this dialog can be defined in a string localizer. This is a convenient way to
    // provide translations for visible, readonly strings, such as this message.
    [Layout(LayoutMode.FullRow)]
    [Browsable(true)] public string Message => this.Translate("Here is my dialog text");

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

## Distributing translations

If you own the package, we recommend bundling your translation files directly
in the package. This solves the problem of language files potentially getting
out of sync with the source files by ensuring the translation files always
match the installed version of the package, and ensures that the translations
are always available when your package is installed. See example:

```xml
<Package Name="MyPackage">
  <Files>
    <!-- You can include the neutral file if you want, but it is not required, and OpenTAP will ignore it -->
    <!-- <File Path="Languages/MyPackage.resx"/> -->
    <File Path="Languages/MyPackage.de.resx"/>
    <File Path="Languages/MyPackage.fr.resx"/>
    <File Path="Packages/MyPackage/MyPackage.dll"/>
  </Files>
</Package>
```

If you do not own the package you are translating, things get a bit more
tricky. If possible, you can reach out to the package maintainers and add a
pull request with your translations, but be aware that they may not accept your
translation.

Alternatively, you can add additional translations for other packages in your
own plugin, or even create a new package which only contains translations:

```xml
<Package Name="OpenTAP German Language Pack">
  <Description>This package adds german translation for OpenTAP types.</Description>
  <Dependencies>
    <!-- Manually add a dependency on the package you are translating. This
    communicates that your translations were created for this version of
    OpenTAP, and that strings added after 9.29.0 may not be translated. -->
    <PackageDependency Package="OpenTAP" Version="^9.29.0" />
  </Dependencies>
  <Files>
    <!-- You could call this file "OpenTAP.de.resx", but this introduces the
    risks of name collisions if OpenTAP added native german support in a future
    update. By using a more specific name, collisions are avoided. -->
    <File Path="Languages/OpenTAP German Language Pack.de.resx"/>
  </Files>
```

If there are multiple translations of the same string, the choice is not
currently defined, so you should not make assumptions about which string will
be chosen as this could easily change in a new OpenTAP version.

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
