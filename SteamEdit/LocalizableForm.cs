using System;
using System.Windows.Forms;

namespace SteamEdit;

public class LocalizableForm : Form
{
    protected virtual void ApplyLanguage() { }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        I18n.LanguageChanged += OnLanguageChanged;
        ApplyLanguage();
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        if (InvokeRequired) { BeginInvoke(new Action(ApplyLanguage)); return; }
        ApplyLanguage();
    }

    protected override void Dispose(bool disposing)
    {
        I18n.LanguageChanged -= OnLanguageChanged;
        base.Dispose(disposing);
    }
}
