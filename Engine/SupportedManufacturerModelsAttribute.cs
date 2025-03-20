using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace OpenTap;

/// <summary>
/// Marks that a driver adds support for a specific set of instruments.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class SupportedManufacturerModelsAttribute : Attribute
{
    /// <summary>
    /// The manufacturer of this range of models.
    /// </summary>
    public string Manufacturer { get; set; }
    /// <summary>
    /// The range of models supported by this driver.
    /// </summary>
    public string[] Models { get; set; }
    /// <summary>
    /// Instantiates a new <see cref="SupportedManufacturerModelsAttribute"/>
    /// </summary>
    /// <param name="manufacturer">The manufacturer supported by this driver.</param>
    /// <param name="model">The model supported by this driver.</param>
    /// <param name="models">Additional models supported by this driver.</param>
    public SupportedManufacturerModelsAttribute(string manufacturer, string model, params string[] models)
    {
        Manufacturer = manufacturer;
        Models = new [] { model }.Concat(models).ToArray();
    } 
    /// <summary>
    /// Instantiates a new <see cref="SupportedManufacturerModelsAttribute"/>
    /// </summary>
    public SupportedManufacturerModelsAttribute()
    {
        
    }
}