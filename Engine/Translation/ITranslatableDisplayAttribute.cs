namespace OpenTap.Translation;

/// <summary>
/// special internal specialization of DisplayAttribute needed for translating dynamic members
/// </summary>
internal interface ITranslatableDisplayAttribute
{
    DisplayAttribute Translate();
}

