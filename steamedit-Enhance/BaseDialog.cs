#nullable enable
using System.ComponentModel;
using System.Windows.Forms;

namespace steamedit_Enhance;

internal class BaseDialog : LocalizableForm
{
    private IContainer components = null!;

    public BaseDialog()
    {
    }

    protected void InitBaseComponent()
    {
        components = new Container();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }
}
