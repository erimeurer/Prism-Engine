using System;

namespace MonoGameEditor.Runtime
{
    public static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

        [STAThread]
        static void Main()
        {
            try
            {
                using (var game = new StandalonePlayer())
                    game.Run();
            }
            catch (Exception ex)
            {
                File.WriteAllText("StartupCrash.txt", ex.ToString());
                MessageBox(IntPtr.Zero, ex.ToString(), "Startup Error", 0x10); // 0x10 is MB_ICONERROR
            }
        }
    }
}
