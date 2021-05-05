# Known Issues

In this section, known issues is listed along with their potential workaround or fixes.

## WinForms Plugins Causes Editor to Freeze or be Unresponsive
If you're creating or using WinForms plugins with the Editor and experience issues with freezing or the Editor being unresponsive.

A possible solution is to disable WinForms from installing its synchronization context: 

```
WindowsFormsSynchronizationContext.AutoInstall = false
```

This value is `[ThreadStatic]` and should be disabled in all threads.

For more information, see OpenTAP issue [#489](https://gitlab.com/OpenTAP/opentap/-/issues/489).
