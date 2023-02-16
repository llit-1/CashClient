using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace cash_client_updater
{
    internal class Program
    {
        // скрываем окно консоли
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;


        static private string updaterVersion = "2.0.3";
        static private string processName = "RKNet_CashClient";
        static private bool islogEnable = true;

        static void Main(string[] args)
        {
            try
            {
                // скрываем окно консоли
                var handle = GetConsoleWindow();
                ShowWindow(handle, SW_HIDE);

                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine("Утилита обновления кассового клиента RKNet v" + updaterVersion);
                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine();

                LocalLog($"утилита обновления кассового клиента RKNet v{updaterVersion} запущена");

                // закрытие клиента
                var n = 1;
                var proc = Process.GetProcessesByName(processName);

                try
                {
                    while (proc.Length > 0)
                    {
                        Console.WriteLine("попытка закрытия клиента #" + n + "...");
                        LocalLog("попытка закрытия клиента #" + n + "...");
                        proc[0].Kill();
                        proc[0].WaitForExit();
                        n++;
                        proc = Process.GetProcessesByName(processName);
                    }

                    Thread.Sleep(5 * 1000); // Пауза 5 секунд
                    Console.WriteLine("клиент закрыт");
                    LocalLog("клиент закрыт");
                }
                catch(Exception ex)
                {
                    LocalLog($"неудачно: {ex.Message}");
                    LocalLog("обновление будет продолжено, не смотря на запущенный экземпляр клиента...");
                }
               

                // замена файлов
                var tempDirectory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                var appDirectory = tempDirectory.Parent;
                var cashClient = Path.Combine(appDirectory.FullName, processName + ".exe");

                if (Directory.Exists(tempDirectory.FullName))
                {
                    Console.WriteLine("удаление старых файлов...");
                    LocalLog("удаление старых файлов...");
                    foreach (var file in Directory.GetFiles(appDirectory.FullName))
                    {
                        if (!file.Contains("RKNet_CashClient.exe.config"))
                        {
                            File.Delete(file);
                        }
                    }

                    Console.WriteLine("копирование новых файлов...");
                    LocalLog("копирование новых файлов...");
                    foreach (var tempFile in Directory.GetFiles(tempDirectory.FullName))
                    {
                        if (!tempFile.Contains("cash_client_updater") && !tempFile.Contains("RKNet_CashClient.exe.config") && !tempFile.Contains("update.zip"))
                        {
                            var appFile = new FileInfo(tempFile).Name;
                            appFile = Path.Combine(appDirectory.FullName, appFile);
                            File.Copy(tempFile, appFile);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("не обнаружен каталог с обновлением");
                    LocalLog("не обнаружен каталог с обновлением");
                }

                if (File.Exists(cashClient))
                {
                    Console.WriteLine("запуск клиента...");
                    LocalLog("запуск клиента...");
                    Process.Start(cashClient);
                }
                else
                {
                    Console.WriteLine($"не обнаружен файл для запуска {cashClient}");
                    LocalLog($"не обнаружен файл для запуска {cashClient}");
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                LocalLog($"сбой во время обновления: {e.Message}");
                //Console.WriteLine(e.ToString());
            }

            Console.WriteLine("завершено");
            LocalLog($"завершено");
            //Console.ReadKey();
        }

        static void LocalLog(string msg)
        {
            if (islogEnable)
            {
                try
                {
                    var curDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);                    
                    var appDirectory = curDir.Parent.FullName;
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
