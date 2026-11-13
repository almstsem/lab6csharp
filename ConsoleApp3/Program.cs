using System;
using System.IO;
using System.Windows.Forms;
using Hospital.Services;

namespace Hospital
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var service = new HospitalService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"));
            Application.Run(new MainForm(service));
        }
    }
}
