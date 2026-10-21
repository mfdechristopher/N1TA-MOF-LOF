using System;
using System.Windows.Forms;

namespace N1TA.MofLof
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--selftest") { Environment.Exit(SelfTest.Run()); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm main = new MainForm();
            if (args.Length > 0 && !args[0].StartsWith("--")) main.StartLog = args[0];     // a log file: command line, drag onto the exe, or "Open with"
            Application.Run(main);
        }
    }
}
