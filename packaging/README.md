# Publishing

## GitHub (free version)

1. Create a public repository and push this folder (the `.gitignore` keeps builds and personal settings out).
2. Fill in `RepoUrl` and `DonateUrl` in `FocusGlance/AppInfo.cs`, and the matching links at the top of the main `README.md`.
3. Tag a release: `git tag v2.1.0` then `git push --tags`. The **Release** workflow builds
   `Focus-with-a-glance.exe` and attaches it to a GitHub release, with a SHA-256 checksum.
4. Turn on a donation page: [GitHub Sponsors](https://github.com/sponsors), [Ko-fi](https://ko-fi.com) or
   [Buy Me a Coffee](https://buymeacoffee.com). Add a `.github/FUNDING.yml` so GitHub shows a *Sponsor* button:
   ```yaml
   ko_fi: your-name
   ```

The donation button appears in Settings → About and in the tray menu, **only in the GitHub build**. The Store
build hides it automatically.

## Microsoft Store

1. Register at [Partner Center](https://partner.microsoft.com/dashboard) as an individual developer (free).
2. *Apps and games → New product → MSIX or PWA app*, and reserve the name **Focus with a glance**.
3. Open *Product management → Product identity* and copy **Package/Identity/Name**, **Publisher** and
   **PublisherDisplayName**.
4. Build the package:
   ```powershell
   .\packaging\build-store.ps1 -GetTools `
     -IdentityName "<Package/Identity/Name>" `
     -Publisher "<Package/Identity/Publisher>" `
     -PublisherDisplayName "<PublisherDisplayName>"
   ```
   This writes `packaging\out\Focus-with-a-glance_<version>_x64.msix` (unsigned; the Store signs it).
5. Start a submission:
   - **Pricing:** Free, or a small price. Desktop apps may also use their own payment system, but keep
     donation links out of the Store build (already done).
   - **Properties:** category *Productivity*; privacy policy URL = the `PRIVACY.md` link on GitHub.
   - **Store listings:** English and Arabic (the package declares both languages). Use 1366×768 or larger
     screenshots.
   - **Submission options → restricted capabilities:** explain `runFullTrust`:
     *"Desktop app (.NET/WinForms + WebView2) converted to MSIX; needs full trust to show an always-on-top
     overlay window and read media and volume state."*
   - **Packages:** upload the `.msix`.
6. Before each update, raise `<Version>` in `FocusGlance/FocusGlance.csproj`; the Store rejects a package
   whose version isn't higher.

### What changes in the Store build

- *Start with Windows* uses the package's startup task instead of the registry. Users can also turn it off in
  Windows Settings → Apps → Startup.
- Settings live in the package's private copy of `%LocalAppData%\FocusGlance`, so the GitHub and Store
  versions don't share settings.
- No donation link.

### Testing the package locally (optional)

Installing an unsigned `.msix` needs either Developer Mode (`Add-AppxPackage -Register
packaging\out\layout\AppxManifest.xml`) or a self-signed certificate. Both change Windows security settings,
so do this only on a machine you're comfortable changing.
