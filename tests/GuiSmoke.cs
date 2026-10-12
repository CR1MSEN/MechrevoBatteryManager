using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using MechrevoBatteryManager.Gui;
class GuiSmoke
{
    [STAThread] static void Main(string[] args)
    {
        args = Array.FindAll(args, delegate(string value) { return value.EndsWith(".png", StringComparison.OrdinalIgnoreCase); });
        if (args.Length != 2) throw new Exception("Expected two PNG output paths");
        Application.EnableVisualStyles();
        using (var form = new MainForm(false))
        {
            form.Show(); Application.DoEvents();
            foreach (string field in new [] {"lightOff", "lightWhite"})
            {
                var box = (CheckBox)typeof(MainForm).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                if (box.Enabled) throw new Exception("Unverified light control enabled");
            }
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0,0,form.Width,form.Height)); using (var output = System.IO.File.Create(args[0])) bitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png); }
            var language = (ComboBox)typeof(MainForm).GetField("language", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            language.SelectedIndex = 1; Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0,0,form.Width,form.Height)); using (var output = System.IO.File.Create(args[1])) bitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png); }
            form.Close();
        }
        Console.WriteLine("PASS GUI construction, Chinese/English switching, and disabled lighting controls; hardware initialization bypassed.");
    }
}

