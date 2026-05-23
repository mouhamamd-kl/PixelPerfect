namespace PixelPerfect.UI.Atoms
{
    /// <summary>
    /// Implemented by every custom control that reacts to theme changes.
    /// MainLayout.ApplyTheme() cascades down through all IThemeable children.
    /// </summary>
    public interface IThemeable
    {
        void ApplyTheme();
    }
}
