Release Notes - OpenTAP 9.30.0
============= 

Highlights
---------

### API Enhancements

* **Custom Name Formatting:**
  Test steps can now implement custom name formatting evaluated *before* macro expansion by implementing the following interface:

  ```csharp
  public interface IFormatName : ITestStep
  {
      string GetFormattedName();
  }
  ```

* **Neutral DisplayAttribute Variant:**
  It is now possible to retrieve the neutral language variant of `DisplayAttribute` through the following Prpoerty on the DisplayAttribute class:

  ```csharp
  public DisplayAttribute NeutralDisplayAttribute { get; }
  ```

---

### Bug Fixes

* Fixed deadlock issues that occurred when waiting for output from test steps stopped due to break conditions.
