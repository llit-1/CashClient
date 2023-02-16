using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace RKNet_CashClient.Models
{
    public static class Logging
    {
        public static bool islogEnable = true;

        // Локальный лог
        public static void LocalLog(string msg)
        {
            if (islogEnable)
            {                
                try
                {
                    var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    var logDirectory = Path.Combine(appDirectory, "logs");
                    var logFilename = Path.Combine(logDirectory, "log.txt");

                    var log_file = new FileInfo(logFilename);

                    if (!Directory.Exists(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }

                    if (log_file.Exists)
                    {
                        var size = log_file.Length / 1024;
                        if (size >= 1024) log_file.Delete();
                    }

                    var fs = new FileStream(log_file.FullName, FileMode.OpenOrCreate);
                    var sw = new StreamWriter(fs);
                    fs.Seek(0, SeekOrigin.End);
                    var time = DateTime.Now.ToString();

                    sw.WriteLine(time + "  " + msg);

                    sw.Close();
                }
                catch (Exception ex)
                {
                    
                }
            }
        }
    }
}
