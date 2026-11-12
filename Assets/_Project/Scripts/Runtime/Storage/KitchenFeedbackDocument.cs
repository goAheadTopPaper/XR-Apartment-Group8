using System;
using System.IO;
using System.Text;

namespace XRApartment.Runtime.Storage
{
    internal static class KitchenFeedbackDocument
    {
        static readonly object WriteLock = new object();

        internal static void Append(string path, string objectId, string objectName, string layout, string fridge, string feedback)
        {
            lock (WriteLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    var exists = File.Exists(path);
                    using (var output = new StreamWriter(temporary, false, new UTF8Encoding(false)))
                    {
                        if (exists) output.Write(File.ReadAllText(path, Encoding.UTF8));
                        else output.WriteLine("Kitchen feedback");
                        output.WriteLine();
                        output.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}]");
                        output.WriteLine($"Object: {objectName} ({objectId})");
                        output.WriteLine($"Layout: {layout} | Fridge: {fridge}");
                        output.WriteLine("Feedback:");
                        output.WriteLine(feedback);
                    }
                    if (exists) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
        }
    }
}
