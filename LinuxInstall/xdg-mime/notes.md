# Status
Spent a few hours wondering why mimes were not working for TapPackage. Turns out that xdg-mime will happily install and add a type to the database,
but xdg-open will not read from the database unless `mimetype` is installed (some perl script). If `mimetype` is not installed, it falls back to `file` 
which only performs a magic test that (correctly) determines .TapPackage to be a zip file.

It is a bit unclear what the "correct" cross-desktop way of doing this is. I don't think we can depend on `mimetype`.

## TODO
* Investigate how major DEs do this. (KDE, Gnome). Worst case they have bespoke, non-portable handling.
