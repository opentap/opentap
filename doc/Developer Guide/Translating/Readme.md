# Translating Packages

Starting with OpenTAP version 9.29, it is possible to provide translation for
OpenTAP packages. OpenTAP can automatically generate translation templates for
all plugin types in a package. Translations are handled through the Annotation system.

The following things **cannot** be translated:
    
* String options from an `[AvailableValues]` target. (Enum options can be translated)
* UserInput dialogs with dynamic content (Static content can be translated, with some user intervention)


## What is

> Internationalization (i18n).
> 
> Localization (l10n).
> 
> Globalization (g11n).
> 
> Localizability (l12y).


## What exists

react-i18n - Explicit DOM element: <Trans i18nkey="key">...</Trans>
Translation function: t(word) -> translated_word

Both of these approaches are unsuitable for OpenTAP 


TODO: 
* Enums broken
* Embedded types broken
* Figure out what classes to include?
* Improve CLI action? (interactive mode?)
** Specify additional types to translate (classes the developer knows will be embedded, etc)
** Custom readline wizard?
** Resource Manager - IReader, IWriter
* Improve tooling
** 
