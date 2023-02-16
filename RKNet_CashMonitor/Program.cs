using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// приложение, следящее за состоянием запущенных приложений
namespace RKNet_CashMonitor
{
    internal class Program
    {
        // Параметры ----------------------------------------------------
        private static string Version = "1.0.0";        // версия приложения
        private static bool isConsoleEnabled = false;   // показ/скрытие окна консоли
        private static int maxAppStartCount = 1;        // максимальное число попыток запуска приложения за 1 проход
        private static int checkInterval = 1;           // интервал проверки состояния приложения в минутах
        // --------------------------------------------------------------

        // скрываем окно консоли
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        private static System.Timers.Timer timer;
        private static List<App> Apps = new List<App>();


        // точка входа
        static void Main(string[] args)
        {
            // закрываем ранее запущенные экземпляры процесса мониторинга
            var monitorProcesses = Process.GetProcessesByName("RKNet_CashMonitor");
            var curProcess = Process.GetCurrentProcess();

            if (monitorProcesses.Count() > 1)
            {
                //Logging.LocalLog("обнаружен запущенный экземпляр процесса RKNet_CashClient, отмена запуска");
                //Close();

                foreach (var proc in monitorProcesses)
                {
                    if (proc.Id != curProcess.Id)
                    {
                        try
                        {
                            Log("обнаружен запущенный экземпляр процесса monitorProcesses, попытка закрыть...");
                            proc.Kill();
                            proc.Dispose();
                            Log("успешно");
                        }
                        catch (Exception ex)
                        {
                            Log($"неуспешно: {ex.Message}");
                        }
                    }
                }
            }

            timer = new System.Timers.Timer(TimeSpan.FromMinutes(checkInterval).TotalMilliseconds);            
            timer.Elapsed += WatchDog;
            timer.AutoReset = true;
            timer.Start();

            try
            {
                Log($"RKNet Монитор состояния v{Version} запущен -----------------------------------");

                // скрываем окно консоли
                var handle = GetConsoleWindow();
                if(!isConsoleEnabled)
                {
                    ShowWindow(handle, SW_HIDE);
                }

                // запускаем мониторинга за состоянием приложения
                var DeliveryClient = new App();
                DeliveryClient.name = "RKNet Клиент доставки";
                DeliveryClient.processName = "RKNet_CashClient";
                DeliveryClient.directoryPath = AppDomain.CurrentDomain.BaseDirectory;

                Apps.Add(DeliveryClient);
                Log($"старт мониторинга за приложением: {DeliveryClient.name}");

                WatchDog(null, null);

            } catch{}
            
            Console.ReadKey();          
        }

        // мониторинг
        private static void WatchDog(Object data, System.Timers.ElapsedEventArgs arg)
        {                  
            try
            {
                foreach(var app in Apps)
                {
                    var proc = Process.GetProcessesByName(app.processName);
                    if (proc.Length == 0)
                    {
                        Log($"обнаружен незапущенный процесс {app.processName}");
                        var n = 1;
                        var result = new Result();

                        while (proc.Length == 0)
                        {
                            Log($"попытка запуска процесса {app.processName} #{n}...");
                            result = app.Start();
                            proc = Process.GetProcessesByName(app.processName);
                            n++;
                            if (n > maxAppStartCount)
                            {
                                break;
                            }
                            Thread.Sleep(5 * 1000); // Пауза 5 секунд 
                        }

                        if (result.Ok)
                        {
                            Log($"приложение {app.name} успешно запущено");
                        }
                        else
                        {
                            Log($"ошибка запуска приложения {app.name}: {result.ErrorMessage}");
                        }
                    }
                }                                              
            }
            catch (Exception ex)
            {
                Log($"ошибка запуска приложения: {ex.Message}");
            }
        }
        
        // логи
        private static void Log(string msg)
        {
            var time = DateTime.Now.ToString("dd.MM.yy HH:mm:ss");
            Console.WriteLine($"{time}  {msg}");
        }
    
        // класс приложения
        private class App
        {
            public string name;
            public string processName;
            public string directoryPath;


            // запуск приложения
            public Result Start()
            {
                var result = new Result();
                try
                {
                    var exeFile = Path.Combine(this.directoryPath, this.processName + ".exe");
                    if (File.Exists(exeFile))
                    {
                        Process.Start(exeFile);
                    }
                    else
                    {
                        result.Ok = false;
                        result.ErrorMessage = $"не обнаружен файл для запуска {exeFile}";
                        return result;
                    }
                }
                catch(Exception e)
                {
                    result.Ok = false;
                    result.ErrorMessage = e.Message;
                    result.ExceptionText = e.ToString();
                    return result;
                }
                return result;
            }
        }
        
        // класс результата выполнения команды
        private class Result
        {
            public bool Ok = true;
            public string ErrorMessage;
            public string ExceptionText;
        }
    }
}
