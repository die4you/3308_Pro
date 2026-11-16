using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace MebKoordinatorluk.Keygen;

static class Program
{
    [STAThread]
    static void Main()
    {
        var trCulture = new CultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = trCulture;
        CultureInfo.DefaultThreadCurrentUICulture = trCulture;
        Thread.CurrentThread.CurrentCulture = trCulture;
        Thread.CurrentThread.CurrentUICulture = trCulture;

        ApplicationConfiguration.Initialize();
        Application.Run(new KeygenForm());
    }
}
