using System.Runtime.InteropServices;

namespace FocusGlance;

/// <summary>Public links and build flavour. Fill the links in once the GitHub repo and donation page exist.</summary>
static class AppInfo
{
    /// <summary>GitHub repository, e.g. "https://github.com/you/focus-with-a-glance". Empty hides the link.</summary>
    public const string RepoUrl = "https://github.com/torrentfreak11-a11y/focus-with-a-glance";

    /// <summary>Donation page (Ko-fi, Buy Me a Coffee, GitHub Sponsors…). Shown only in the free GitHub build.</summary>
    public const string DonateUrl = "";

    /// <summary>Hosted privacy policy (the Store listing needs a URL). Defaults to PRIVACY.md in the repo.</summary>
    public static string PrivacyUrl => RepoUrl.Length > 0 ? RepoUrl + "/blob/main/PRIVACY.md" : "";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFullName(ref int length, char[]? name);

    /// <summary>True when running from the MSIX (Microsoft Store) package.</summary>
    public static readonly bool IsPackaged = DetectPackaged();

    static bool DetectPackaged()
    {
        try
        {
            int len = 0;
            return GetCurrentPackageFullName(ref len, null) != 15700; // APPMODEL_ERROR_NO_PACKAGE
        }
        catch { return false; }
    }
}
