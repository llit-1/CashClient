using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR.Client;
using System.Configuration;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using RKNet_CashClient.Models;

namespace RKNet_CashClient.Models
{
    internal class SignalConnection
    {
        public HubConnection hubConnection;

        // событие подключения
        public delegate void connected();
        public event connected Connected;

        // событие отключения
        public delegate void disconnected();
        public event disconnected Disconnected;

        // событие переподключения (создание новго подключения)
        public delegate void newConnection();
        public event newConnection NewConnection;

        // событие получения информации о кассовом клиенте
        public delegate void clientInfo(string ttName);
        public event clientInfo ClientInfoReceived;

        // событие получения нового заказа
        public delegate void newOrder(RKNet_Model.MSSQL.MarketOrder order);
        public event newOrder NewOrderReceived;

        // событие получения обновлённого заказа
        public delegate void updateOrder(RKNet_Model.MSSQL.MarketOrder order);
        public event updateOrder UpdatedOrderReceived;

        // событие получения списка заказов
        public delegate void ordersList(List<RKNet_Model.MSSQL.MarketOrder> OrdersList);
        public event ordersList OrdersListReceived;

        // событие получения отмены принятого заказа
        public delegate void orderCancel(RKNet_Model.MSSQL.MarketOrder order);
        public event orderCancel OrderCanelReceived;

        // событие для вызова MessageBox в клиенте
        public delegate void messageShow(string message);
        public event messageShow MessageShowReceived;

        // переменные для отслеживания состояние соединения
        private bool allowConnect = true;        
        private bool isPingSended = false;
        System.Timers.Timer checkConnectionTimer = new System.Timers.Timer(TimeSpan.FromSeconds(60).TotalMilliseconds);

        public SignalConnection()
        {
            checkConnectionTimer.Elapsed += CheckConnection;
            checkConnectionTimer.AutoReset = true;
            checkConnectionTimer.Enabled = false;
            checkConnectionTimer.Start();
        }

        //------------------------------------------------------------------------------------
        // Соединение с Апи Сервером
        //------------------------------------------------------------------------------------
        
        // подключение
        public void Connect()
        {
            Logging.LocalLog($"{this.GetHashCode()} новое подключение к серверу...");
            allowConnect = true;
            StartConnection();
        }
        private async void StartConnection()
        {
            if (!allowConnect) return;

            // получение ip клиента
            var cashIp = string.Empty;
            try
            {                                
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    cashIp = endPoint.Address.ToString();
                }
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка получения ip клиента: {ex.Message}");
                return;
            }                        

            // подключение к хабу SignalR            
            try
            {
                hubConnection = new HubConnectionBuilder()
                .WithUrl(new Uri(ConfigurationManager.AppSettings["apiHost"] + "/casheshub"), (options) =>
                {
                    options.Headers.Add("CashClientIp", cashIp);
                    options.Headers.Add("CashClientVersion", CashClient.Version);
                })
                .Build();

                //hubConnection.Closed += async (error) =>
                //{
                //    Disconnected?.Invoke();
                //    await Task.Delay(new Random().Next(0, 5) * 1000);
                //    StartConnection();
                //};

                hubConnection.Closed += reconnect;

                hubConnection.On<RKNet_Model.MSSQL.MarketOrder>("NewOrder", NewOrder);
                hubConnection.On<RKNet_Model.MSSQL.MarketOrder>("OrderCancel", OrderCancel);
                hubConnection.On<RKNet_Model.MSSQL.MarketOrder>("OrderUpdate", OrderUpdate);
                hubConnection.On<List<RKNet_Model.MSSQL.MarketOrder>>("GetOrders", OrdersList);
                hubConnection.On<string>("ClientInfo", ClientInfo);
                hubConnection.On("AutoUpdate", AutoUpdate);
                hubConnection.On("ResponsePingReceived", ResponsePingReceived);

                await hubConnection.StartAsync();
                Connected?.Invoke();
                Logging.LocalLog($"{this.GetHashCode()} подключено");
                checkConnectionTimer.Enabled = true;
                AutoUpdate();
            }
            catch(Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка подключения: {ex.Message}");
                await Task.Delay(new Random().Next(0, 5) * 1000);
                StartConnection();
            }
        }

        private async Task reconnect(Exception err)
        {
            if(err != null)
                Logging.LocalLog($"{this.GetHashCode()} переподключение, причина: {err.Message}");
            else
                Logging.LocalLog($"{this.GetHashCode()} переподключение...");
            try
            {
                Disconnected?.Invoke();
                await Task.Delay(new Random().Next(0, 5) * 1000);
                StartConnection();
            }
            catch(Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка попытки переподключения: {ex.Message}");
            }
            
        }

        // отключение
        public void Disconnect()
        {            
            Logging.LocalLog($"{this.GetHashCode()} отключение от сервера...");
            try
            {
                Disconnected?.Invoke();
                allowConnect = false;

                hubConnection.Closed -= reconnect;
                hubConnection.StopAsync();
                hubConnection.DisposeAsync();
                
                Logging.LocalLog($"{this.GetHashCode()} отключено");
            }
            catch(Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка отключения: {ex.Message}");
            }            
        }

        // проверка соединения
        private void CheckConnection(Object data, System.Timers.ElapsedEventArgs arg)
        {
            try
            {
                if (isPingSended)
                {
                    // нет ответа на пинг
                    Logging.LocalLog($"{this.GetHashCode()} таймаут ответа от сервера");
                    ClientInfoReceived?.Invoke("connection lost");

                    if (allowConnect)
                    {
                        Disconnect();
                        checkConnectionTimer.Enabled = false;
                        NewConnection?.Invoke();
                    }
                }
                else
                {                    
                    AutoUpdate(); // проверяем наличие обновлений клиента
                    SendPing();
                }
                
            }
            catch(Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка проверки соединения: {ex.Message}");
            }
                      
        }
        

        //------------------------------------------------------------------------------------
        // Методы SignalR
        //------------------------------------------------------------------------------------

        // исходящие сообщения
        public async void GetOrders()
        {
            try
            {
                await hubConnection.InvokeAsync("GetOrders");
            }
            catch (Exception ex) 
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка запроса списка заказов: {ex.Message}");
            }
        }
        public async void OrderAccept(bool isAccepted, int orderId)
        {
            try
            {
                await hubConnection.InvokeAsync("OrderAccept", isAccepted, orderId);
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка подтверждения заказа: {ex.Message}");
            }

        }
        public async void OrderFinish(int orderId)
        {
            try
            {
                await hubConnection.InvokeAsync("OrderFinish", orderId);
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка завершения заказа: {ex.Message}");
            }
        }
        public async void SendPrinterInfo(string printerName)
        {
            try
            {
                await hubConnection.InvokeAsync("PrinterInfo", printerName);
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка отправки данных принтера: {ex.Message}");
            }

        }
        public async void SendPing()
        {
            if(allowConnect)
            {
                try
                {
                    isPingSended = true;
                    await hubConnection.InvokeAsync("PingReceived");
                }
                catch (Exception ex)
                {
                    Logging.LocalLog($"{this.GetHashCode()} ошибка отправки пинга на сервер: {ex.Message}");
                }
            }            
        }
        
        // входящие сообщения
        public void NewOrder(RKNet_Model.MSSQL.MarketOrder newOrder)
        {
            try
            {
                NewOrderReceived?.Invoke(newOrder);
            }            
            catch (Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка получения нового заказа: {ex.Message}");
            }
        }
        public void OrdersList(List<RKNet_Model.MSSQL.MarketOrder> ordersList)
        {
            try
            {
                OrdersListReceived?.Invoke(ordersList);
            }            
            catch (Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка получения списка заказов: {ex.Message}");
            }
        }
        public void ClientInfo(string ttName)
        {
            try
            {
                ClientInfoReceived?.Invoke(ttName);
            }            
            catch (Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка получения инофрмации о клиенте: {ex.Message}");
            }
        }
        public void OrderCancel(RKNet_Model.MSSQL.MarketOrder cancelledOrder)
        {
            try
            {
                OrderCanelReceived?.Invoke(cancelledOrder);
            }            
            catch (Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка получения отмены заказа: {ex.Message}");
            }
        }
        public void OrderUpdate(RKNet_Model.MSSQL.MarketOrder updatedOrder)
        {
            try
            {
                UpdatedOrderReceived?.Invoke(updatedOrder);
            }            
            catch (Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка получения корректировок заказа: {ex.Message}");
            }
        }
        public void ResponsePingReceived()
        {
            try
            {
                isPingSended = false;
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"{this.GetHashCode()} ошибка получения ответа на пинг: {ex.Message}");
            }
        }

        // Автообновление
        public void AutoUpdate()
        {
            if(allowConnect && !isPingSended)
            {                
                try
                {
                    //Logging.LocalLog("проверка наличия обновления клиента...");

                    // запрос строки обновления из БД
                    var result = ApiRequest.GetClientInfo(hubConnection.ConnectionId);
                    if (result.Ok)
                    {
                        var clientInfo = result.Data;

                        if (CashClient.Version != clientInfo.UpdateToVersion && !string.IsNullOrEmpty(clientInfo.UpdateToVersion))
                        {
                            Logging.LocalLog($"{this.GetHashCode()} текущая версия: {CashClient.Version}, обнаружено задание на обновление на версию {clientInfo.UpdateToVersion}");

                            // обновление
                            Logging.LocalLog($"{this.GetHashCode()} загрузка файла обновления...");

                            var newVersionResult = ApiRequest.GetUpdateFile(clientInfo.UpdateToVersion);                            

                            if (newVersionResult.Result.Ok)
                            {
                                Logging.LocalLog($"{this.GetHashCode()} файл успешно загружен");
                                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                                var tempDirectory = Path.Combine(appDirectory, "temp");
                                var zipFilename = Path.Combine(tempDirectory, "update.zip");

                                var newVersion = newVersionResult.Result.Data;

                                if (Directory.Exists(tempDirectory))
                                {
                                    Directory.Delete(tempDirectory, true);
                                }
                                Directory.CreateDirectory(tempDirectory);
                                File.WriteAllBytes(zipFilename, newVersion);


                                Logging.LocalLog($"{this.GetHashCode()} распаковка архива...");
                                // распкаовка zip архива
                                ZipFile.ExtractToDirectory(zipFilename, tempDirectory);
                                //File.Delete(zipFilename);

                                Logging.LocalLog($"{this.GetHashCode()} запуск утилиты обновления...");
                                var cashClientUpdater = Path.Combine(tempDirectory, "cash_client_updater.exe");
                                //Process.Start(cashClientUpdater);

                                var psi = new ProcessStartInfo
                                {
                                    FileName = cashClientUpdater,
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,                                    
                                };
                                Process.Start(psi);
                            }
                            else
                            {
                                Logging.LocalLog($"{this.GetHashCode()} ошибка загрузки файла обновления: {newVersionResult.Result.ErrorMessage}");
                            }
                        }
                    }
                    else
                    {
                        Logging.LocalLog($"{this.GetHashCode()} ошибка проверки обновления клиента: {result.ErrorMessage}");
                    }
                }
                catch(Exception ex)
                {
                    Logging.LocalLog($"{this.GetHashCode()} сбой во время обновления: {ex.ToString()}");
                }
                
            }
            
        }                      
    }
}
