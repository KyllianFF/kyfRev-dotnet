namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Une page prévenue quand elle s'ouvre, pour relire ce qu'elle montre.</summary>
public interface INavigationAware
{
    /// <summary>La page vient de s'ouvrir, un coffre étant ouvert.</summary>
    void OnNavigatedTo();
}
