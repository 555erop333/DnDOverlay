using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

class IconConverter
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    static int Main(string[] args)
    {
        try
        {
            var input = args.Length > 0 ? args[0] : "MainIco.png";
            var output = args.Length > 1 ? args[1] : Path.ChangeExtension(input, ".ico");

            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"Input file '{input}' not found.");
                return 1;
            }

            using var bitmap = (Bitmap)Image.FromFile(input);
            var hIcon = bitmap.GetHicon();
            try
            {
                using var icon = Icon.FromHandle(hIcon);
                using var fileStream = File.Create(output);
                icon.Save(fileStream);
            }
            finally
            {
                DestroyIcon(hIcon);
            }

            Console.WriteLine($"Saved icon to {output}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
