using System;

namespace OpenTap;

/// <summary>
/// Describes what hardware an Instrument or Dut driver supports.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class SupportedModelsAttribute : Attribute
{
    /// <summary>
    /// The manufacturer of the hardware.
    /// </summary>
    public string Manufacturer { get; set; }
    /// <summary>
    /// The hardware model numbers supported by the driver.
    /// </summary>
    public string[] Models { get; set; }
    /// <summary>
    /// Instantiates a new <see cref="SupportedModelsAttribute"/>
    /// </summary>
    /// <param name="manufacturer">The manufacturer of the hardware.</param>
    /// <param name="models">The hardware model numbers supported by the driver.</param>
    public SupportedModelsAttribute(string manufacturer, params string[] models)
    {
        Manufacturer = manufacturer;
        Models = models;
    } 
    
    /// <summary>
    /// Instantiates a new <see cref="SupportedModelsAttribute"/>
    /// </summary>
    public SupportedModelsAttribute()
    {
        
    }
}