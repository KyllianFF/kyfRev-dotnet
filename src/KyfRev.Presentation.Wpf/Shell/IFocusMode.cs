using System.ComponentModel;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Le mode concentration, tel qu'une vue peut le suivre et le commander.</summary>
public interface IFocusMode : INotifyPropertyChanged
{
    /// <summary>Le mode est actif : le menu est masqué.</summary>
    bool IsFocusMode { get; set; }
}
