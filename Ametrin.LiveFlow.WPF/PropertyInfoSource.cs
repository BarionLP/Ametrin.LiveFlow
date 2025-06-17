namespace Ametrin.LiveFlow.WPF;

public enum PropertyInfoSource
{
    /// <summary>
    /// Do not auto generate collumns
    /// </summary>
    None,

    /// <summary>
    /// Uses static type reflection to generate collumns
    /// </summary>
    Type,

    /// <summary>
    /// Derives properties from the first loaded element (supports ExpandoObjects)
    /// </summary>
    FirstElement
}
