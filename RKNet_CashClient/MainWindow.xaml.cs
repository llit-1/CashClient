using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Configuration;
using System.Windows.Media.Animation;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Printing;
using FontAwesome.WPF;
using RKNet_CashClient.Models;
using RKNet_Model;
using System.IO;
using System.Threading;

namespace RKNet_CashClient
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // статус соединение        
        private bool isConnected = false;

        // переменная нового заказа и обработка изменения значения переменной
        private bool _needConfirm  = false; //техническая переменная
        public bool needConfirm  // сюда присваиваем значение
        {
            set
            {
                if (value != _needConfirm)
                {
                    _needConfirm = value;
                    if (value)
                    {
                        AlertNewOrderStart();
                    }
                    else
                    {
                        AlertNewOrderStop();
                    }
                }
            }
            get
            {
                return _needConfirm;
            }
        }

        // хранилище поступивших на кассу заказов
        public List<RKNet_Model.MSSQL.MarketOrder> OrdersList = new List<RKNet_Model.MSSQL.MarketOrder>();

        private Models.SignalConnection signalConnection;

        // ----------------------------------------------------------------------------------
        // КОНСТРУКТОР ГЛАВНОГО ОКНА
        // ----------------------------------------------------------------------------------
        public MainWindow()
        {
            Logging.LocalLog($"RKNet Кассовый клиент v{CashClient.Version} запущен");

            // запускаем Монитор состояния приложения
            try
            {
                Logging.LocalLog("запуск мониторинга состояния приложения RKNet Cash Monitor...");

                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;               
                var cashMonitor = System.IO.Path.Combine(appDirectory, "RKNet_CashMonitor.exe");

                if (File.Exists(cashMonitor))
                {
                    Process.Start(cashMonitor);
                    Logging.LocalLog("успешно");
                }
                else
                {
                    Logging.LocalLog($"не обнаружен файл для запуска {cashMonitor}");
                }                
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка: {ex.Message}");
            }

            // закрываем все открытые экземпляры процесса, кроме текущего
            try
            {
                var rknetProcesses = Process.GetProcessesByName("RKNet_CashClient");
                var curProcess = Process.GetCurrentProcess();

                if (rknetProcesses.Count() > 1)
                {
                    //Logging.LocalLog("обнаружен запущенный экземпляр процесса RKNet_CashClient, отмена запуска");
                    //Close();

                    foreach (var proc in rknetProcesses)
                    {
                        if (proc.Id != curProcess.Id)
                        {
                            try
                            {
                                Logging.LocalLog("обнаружен запущенный экземпляр процесса RKNet_CashClient, попытка закрыть...");
                                proc.Kill();
                                proc.Dispose();
                                Logging.LocalLog("успешно");
                            }
                            catch (Exception ex)
                            {
                                Logging.LocalLog($"неуспешно: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Logging.LocalLog($"ошибка: {ex.Message}");
            }

            // удаляем временные файлы
            try
            {
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var tempDirectory = System.IO.Path.Combine(appDirectory, "temp");

                if (System.IO.Directory.Exists(tempDirectory))
                    System.IO.Directory.Delete(tempDirectory, true);
            }
            catch(Exception ex)
            {
                Logging.LocalLog($"ошибка удаления временных файлов: {ex.Message}");
            }


            // Инициализация окна
            try
            {
                InitializeComponent();
                var appSettings = ConfigurationManager.AppSettings;


                // размер экрана
                double screenHeight = SystemParameters.FullPrimaryScreenHeight;
                double screenWidth = SystemParameters.FullPrimaryScreenWidth;

                // положение окна
                this.Top = 0;
                this.Left = screenWidth - this.Width;

                // размеры и положение кнопки доставки
                var deliveryButton_Up = double.Parse(appSettings["deliveryButton_Up"]);
                var deliveryButton_Right = double.Parse(appSettings["deliveryButton_Right"]);
                ButtonDelivery.Margin = new Thickness(0, deliveryButton_Up, deliveryButton_Right, 0);
                ButtonDelivery.Width = double.Parse(appSettings["deliveryButton_Width"]);
                ButtonDelivery.Height = double.Parse(appSettings["deliveryButton_Height"]);

                // задаем начальные значения видимости элементов
                OrderForm.Visibility = Visibility.Hidden;
                OrdersListForm.Visibility = Visibility.Hidden;

                ButtonDeliveryVersion.Content = $"v{Models.CashClient.Version}";
                ButtonDeliveryLogoDisconnected.Visibility = Visibility.Visible;
                ButtonDeliveryLogoConnected.Visibility = Visibility.Hidden;
                ButtonDeliveryLoading.Visibility = Visibility.Visible;

                ConfirmForm.Visibility = Visibility.Hidden;
                PrintGrid.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка инициализации главного окна приложения: {ex.Message}");
            }

            // получаем список принтеров и добавляем их в контекстное меню иконки в трее
            try
            {
                if (ConfigurationManager.AppSettings["printer"] == "")
                {
                    ConfigurationManager.AppSettings["printer"] = "не печатать";
                    ConfigurationManager.RefreshSection("appSettings");
                }
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка получения списка принтеров: {ex.Message}");
            }
            
            
            RefreshPrinters();

            ConnectToServer();

            // Таймер для обновления списка заказов каждый час
            var updateTimer = new System.Timers.Timer();
            updateTimer.Interval = (1000 * 60 * 60);
            updateTimer.Elapsed += OnTimerEvent;
            updateTimer.Enabled = true;
        }
        // ----------------------------------------------------------------------------------
        // Подключение к кассовому хабу событий на Апи сервере
        // ----------------------------------------------------------------------------------
        private void ConnectToServer()
        {
            try
            {
                signalConnection = new Models.SignalConnection();
                signalConnection.Connected += OnConnect;
                signalConnection.Disconnected += OnDisconnect;
                signalConnection.ClientInfoReceived += ClientInfo;
                signalConnection.NewOrderReceived += NewOrder;
                signalConnection.OrderCanelReceived += OrderCancel;
                signalConnection.OrdersListReceived += OrdersListUpdate;
                signalConnection.UpdatedOrderReceived += OrderUpdate;
                signalConnection.MessageShowReceived += MessageShow;
                signalConnection.NewConnection += ConnectToServer;
                signalConnection.Connect();
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка подключения к серверу: {ex.Message}");
            }
            
        }

        // ----------------------------------------------------------------------------------
        // ОБРАБОТЧИКИ СОБЫТИЙ
        // ----------------------------------------------------------------------------------
        private void OnConnect()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    //MessageBox.Show("connected");
                    isConnected = true;

                    ButtonDeliveryLogoConnected.Visibility = Visibility.Visible;
                    ButtonDeliveryLogoDisconnected.Visibility = Visibility.Hidden;

                    signalConnection.GetOrders();
                    signalConnection.SendPrinterInfo(ConfigurationManager.AppSettings["printer"]);
                });
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OnConnect: {ex.Message}");
            }            
        }
        private void OnDisconnect()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    //MessageBox.Show("disconnected");
                    isConnected = false;

                    ButtonDeliveryLogoConnected.Visibility = Visibility.Hidden;
                    ButtonDeliveryLogoDisconnected.Visibility = Visibility.Visible;
                    ButtonDeliveryLoading.Visibility = Visibility.Visible;

                });
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OnDisconnect: {ex.Message}");
            }            
        }
        
        private void ClientInfo(string ttName)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    ButtonDeliveryTT.Content = ttName;
                });
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода ClientInfo: {ex.Message}");
            }                        
        }
        private void NewOrder(RKNet_Model.MSSQL.MarketOrder newOrder)
        {
            try
            {
                var order = OrdersList.FirstOrDefault(o => o.Id == newOrder.Id);
                if (order == null)
                {
                    OrdersList.Add(newOrder);
                }
                else
                {
                    OrdersList.Remove(order);
                    OrdersList.Add(newOrder);
                }
                OrdersListUpdate(OrdersList);
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода NewOrder: {ex.Message}");
            }            
        }
        private void OrderUpdate(RKNet_Model.MSSQL.MarketOrder updatedOrder)
        {
            try
            {
                var order = OrdersList.FirstOrDefault(o => o.Id == updatedOrder.Id);
                if (order != null)
                {
                    //order.Updated = updatedOrder.Updated;
                    order.OrderSum = updatedOrder.OrderSum;
                    order.StatusCode = updatedOrder.StatusCode;
                    order.StatusName = updatedOrder.StatusName;
                    order.StatusComment = updatedOrder.StatusComment;
                    order.StatusUpdatedAt = updatedOrder.StatusUpdatedAt;
                    order.OrderItems = updatedOrder.OrderItems;
                    order.YandexOrder = updatedOrder.YandexOrder;
                }
                else
                {
                    OrdersList.Add(updatedOrder);
                }
                OrdersListUpdate(OrdersList);
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OrderUpdate: {ex.Message}");
            }
            
        }
        private void OrderCancel(RKNet_Model.MSSQL.MarketOrder cancelledOrder)
        {
            try
            {
                var order = OrdersList.FirstOrDefault(o => o.Id == cancelledOrder.Id);
                if (order != null)
                {
                    order.StatusCode = cancelledOrder.StatusCode;
                    order.StatusName = cancelledOrder.StatusName;
                    order.StatusUpdatedAt = cancelledOrder.StatusUpdatedAt;
                    order.StatusComment = cancelledOrder.StatusComment;
                    order.CancelReason = cancelledOrder.CancelReason;

                    OrdersListUpdate(OrdersList);
                }
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OrderCancel: {ex.Message}");
            }            
        }
        private void OrdersListUpdate(List<RKNet_Model.MSSQL.MarketOrder> ordersList)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    OrdersList = ordersList;
                    // обновление данных на кнопке доставки
                    var ordersToConfirm = OrdersList.Where(o => o.StatusCode == 1 | o.StatusCode == 3).Count();

                    // новые
                    ButtonDeliveryNew.Text = ordersToConfirm.ToString();                    
                    
                    // в работе
                    ButtonDeliveryAccepted.Text = OrdersList.Where(o => o.StatusCode == 2 | o.StatusCode == 4 | o.StatusCode == 5).Count().ToString();
                    
                    // завершенные
                    ButtonDeliveryCancelled.Text = OrdersList.Where(o => o.StatusCode >= 6).Count().ToString();

                    if (ordersToConfirm > 0)
                    {
                        needConfirm = true;
                    }
                    else
                    {
                        needConfirm = false;
                    }

                    ButtonDeliveryLoading.Visibility = Visibility.Hidden;

                    // обновление данных в списке заказов
                    OrdersListView.Items.Clear();
                    foreach (var order in OrdersList.OrderByDescending(o => o.Id))
                    {
                        var viewItem = new Models.OrderElement();
                        viewItem.Created = order.Created.ToString("MM.dd HH:mm");
                        viewItem.Number = order.Id.ToString();
                        viewItem.Operator = order.OrderTypeName;
                        viewItem.TotalSum = order.OrderSum.ToString() + "р";

                        if(order.StatusComment == "автоматически отменён")
                        {
                            viewItem.Status = "автоотмена";
                        }
                        else
                        {
                            viewItem.Status = order.StatusName;
                        }                        

                        if (order.OrderTypeCode == 1)
                        {
                            var yandexOrder = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.YandexOrder>(order.YandexOrder);
                            viewItem.AgregatorNumber = yandexOrder.eatsId;
                            viewItem.Customer = yandexOrder.deliveryInfo.clientName;
                            if (order.StatusCode < 6)
                            {
                                viewItem.isActive = true;
                            }
                        }

                        if (order.OrderTypeCode == 2)
                        {
                            var deliveryOrder = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.DeliveryOrder>(order.DeliveryOrder);
                            viewItem.AgregatorNumber = deliveryOrder.originalOrderId;
                            viewItem.Customer = deliveryOrder.customer.name;
                            if (order.StatusCode < 6)
                            {
                                viewItem.isActive = true;
                            }
                        }

                        OrdersListView.Items.Add(viewItem);
                    }

                    // активация форм
                    OrdersListView.IsEnabled = true;
                    OrderFormButtonAccept.IsEnabled = true;
                    OrderFormButtonCancel.IsEnabled = true;
                });
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OrdersListUpdate: {ex.Message}");
            }
                    
        }        
        private void OnTimerEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                signalConnection.GetOrders();
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OnTimerEvent: {ex.Message}");
            }            
        }
        private void MessageShow(string message)
        {
            try
            {
                MessageBox.Show(message);
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода MessageShow: {ex.Message}");
            }            
        }
        
        // ----------------------------------------------------------------------------------
        // КНОПКА ДОСТАВКИ
        // ----------------------------------------------------------------------------------
        
        // Кнопка доставки: левый клик
        private void ButtonDelivery_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ButtonDeliveryLoading.Visibility == Visibility.Visible)
                    return;

                if (needConfirm)
                {
                    var orderId = OrdersList.OrderBy(o => o.Created).FirstOrDefault(o => o.StatusCode == 1 | o.StatusCode == 3).Id;
                    OpenOrderForm(orderId);
                }
                else
                {
                    OrdersListForm.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода ButtonDelivery_Click: {ex.Message}");
            }            
        }

        // Кнопка доставки: запуск анимации нового заказа
        public void AlertNewOrderStart()
        {
            try
            {
                var alertOrder = new DoubleAnimation();
                alertOrder.From = 0;
                alertOrder.To = 1;
                alertOrder.AutoReverse = true;
                alertOrder.RepeatBehavior = RepeatBehavior.Forever;
                alertOrder.Duration = new Duration(TimeSpan.FromSeconds(0.5));

                ButtonDeliveryRect.Visibility = Visibility.Visible;
                ButtonDeliveryRect.BeginAnimation(Button.OpacityProperty, alertOrder);
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода AlertNewOrderStart: {ex.Message}");
            }
            
        }

        // Кнопка доставки: остановка анимации нового заказа
        public void AlertNewOrderStop()
        {
            try
            {
                ButtonDeliveryRect.BeginAnimation(Button.OpacityProperty, null);
                ButtonDeliveryRect.Opacity = 0;
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода AlertNewOrderStop: {ex.Message}");
            }            
        }

        // ----------------------------------------------------------------------------------
        // ИКОНКА В ТРЕЕ
        // ----------------------------------------------------------------------------------
        
        // Иконка в трее: левый клик
        private void TaskbarIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Show();
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода TaskbarIcon_TrayLeftMouseDown: {ex.Message}");
            }            
        }
        // Иконка в трее: закрыть приложение
        private void TaskbarIconClose(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Close();
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода TaskbarIconClose: {ex.Message}");
            }            
        }
        // выбор принтера
        private void PrinterSelectEvent(object sender, RoutedEventArgs arg)
        {
            try
            {
                var menuItem = sender as MenuItem;
                var printerName = menuItem.Header.ToString();

                String path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                Configuration config = ConfigurationManager.OpenExeConfiguration(path);

                config.AppSettings.Settings["printer"].Value = printerName;
                config.Save(ConfigurationSaveMode.Modified);

                ConfigurationManager.RefreshSection("appSettings");

                if (ConfigurationManager.AppSettings["printer"] != "не печатать")
                {
                    OrderFormButtonPrint.IsEnabled = true;
                }
                else
                {
                    OrderFormButtonPrint.IsEnabled = false;
                }

                RefreshPrinters();
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода PrinterSelectEvent: {ex.Message}");
            }
                       
        }

        // тест печати
        private void TestPrint(object sender, RoutedEventArgs arg)
        {
            try
            {
                var items = new List<RKNet_Model.MSSQL.MarketOrder.OrderItem>
            {
                new RKNet_Model.MSSQL.MarketOrder.OrderItem
                {
                    RkName = "позиция 01",
                    MarketPrice = 0,
                    Quantity = 0,
                    TotalCost = 0
                },
                new RKNet_Model.MSSQL.MarketOrder.OrderItem
                {
                    RkName = "позиция 02",
                    MarketPrice = 0,
                    Quantity = 0,
                    TotalCost = 0
                },
                new RKNet_Model.MSSQL.MarketOrder.OrderItem
                {
                    RkName = "позиция 03",
                    MarketPrice = 0,
                    Quantity = 0,
                    TotalCost = 0
                }
            };
                var order = new RKNet_Model.MSSQL.MarketOrder
                {
                    Id = 0,
                    Created = DateTime.Now,
                    OrderTypeCode = RKNet_Model.MSSQL.MarketOrder.OrderTypes.Test.Code,
                    OrderTypeName = RKNet_Model.MSSQL.MarketOrder.OrderTypes.Test.Name,
                    OrderItems = Newtonsoft.Json.JsonConvert.SerializeObject(items)
                };

                PrintOrder(order);
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода TestPrint: {ex.Message}");
            }            
        }

        // ----------------------------------------------------------------------------------
        // ФОРМА ЗАКАЗА
        // ----------------------------------------------------------------------------------
        // Открыть форму 
        private void OpenOrderForm(int orderId)
        {
            try
            {
                CancelReason.Visibility = Visibility.Hidden;
                var order = OrdersList.FirstOrDefault(o => o.Id == orderId);
                
                // Статусы заказа
                switch (order.StatusCode)
                {
                    // новый заказ
                    case 1:
                        // Заголовок окна
                        OrderFormHeader.Content = $"Новый заказ";
                        OrderFormHeader.Foreground = Brushes.Red;

                        // кнопки окна заказа
                        OrderFormButtonCancel.Content = "Отклонить";
                        OrderFormButtonAccept.Content = "Принять";
                        OrderFormButtonAccept.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4CBF00"));
                        OrderFormButtonAccept.IsEnabled = true;
                        OrderFormButtonCancel.Visibility = Visibility.Visible;
                        OrderFormButtonAccept.Visibility = Visibility.Visible;
                        OrderFormButtonPrint.Visibility = Visibility.Visible;
                        OrderFormButtonFinish.Visibility = Visibility.Hidden;
                        break;

                    // обновлён
                    case 3:
                        // данные окна заказа
                        OrderFormHeader.Content = $"Изменения по заказу";
                        OrderFormHeader.Foreground = Brushes.OrangeRed;

                        // кнопки окна заказа
                        OrderFormButtonCancel.Content = "Отменить";
                        OrderFormButtonAccept.Content = "Подтвердить";
                        OrderFormButtonAccept.Background = Brushes.Orange;
                        OrderFormButtonAccept.IsEnabled = true;
                        OrderFormButtonCancel.Visibility = Visibility.Visible;
                        OrderFormButtonAccept.Visibility = Visibility.Visible;
                        OrderFormButtonPrint.Visibility = Visibility.Hidden;
                        OrderFormButtonFinish.Visibility = Visibility.Hidden;
                        break;

                    // выдан курьеру
                    case 6:
                    case 7:
                    case 10:
                    case 11:
                        // данные окна заказа
                        OrderFormHeader.Content = $"Заказ с доставкой";
                        OrderFormHeader.Foreground = Brushes.Black;

                        // кнопки окна заказа
                        OrderFormButtonCancel.Visibility = Visibility.Hidden;
                        OrderFormButtonAccept.Visibility = Visibility.Hidden;
                        OrderFormButtonPrint.Visibility = Visibility.Visible;
                        OrderFormButtonFinish.Visibility = Visibility.Hidden;

                        // проверяем наличие принтера и активируем или блокируем кнопку печати
                        if (ConfigurationManager.AppSettings["printer"] == "не печатать")
                        {
                            OrderFormButtonPrint.IsEnabled = false;
                        }
                        else
                        {
                            OrderFormButtonPrint.IsEnabled = true;
                        }
                        break;
                    // отменён
                    case 8:
                    case 9:
                        // данные окна заказа
                        OrderFormHeader.Content = $"Отменённый заказ";
                        OrderFormHeader.Foreground = Brushes.Black;

                        CancelReason.Content = order.CancelReason;
                        CancelReason.Visibility = Visibility.Visible;

                        // кнопки окна заказа
                        OrderFormButtonCancel.Visibility = Visibility.Hidden;
                        OrderFormButtonAccept.Visibility = Visibility.Hidden;
                        OrderFormButtonPrint.Visibility = Visibility.Visible;
                        OrderFormButtonFinish.Visibility = Visibility.Hidden;

                        // проверяем наличие принтера и активируем или блокируем кнопку печати
                        if (ConfigurationManager.AppSettings["printer"] == "не печатать")
                        {
                            OrderFormButtonPrint.IsEnabled = false;
                        }
                        else
                        {
                            OrderFormButtonPrint.IsEnabled = true;
                        }
                        break;

                    // принят на тт
                    default:
                        // данные окна заказа
                        OrderFormHeader.Content = $"Заказ с доставкой";
                        OrderFormHeader.Foreground = Brushes.Black;

                        // кнопки окна заказа
                        OrderFormButtonCancel.Content = "Отменить";
                        OrderFormButtonCancel.Visibility = Visibility.Visible;
                        OrderFormButtonAccept.Visibility = Visibility.Hidden;
                        OrderFormButtonPrint.Visibility = Visibility.Visible;
                        OrderFormButtonFinish.Visibility = Visibility.Visible;

                        // проверяем наличие принтера и активируем или блокируем кнопку печати
                        if (ConfigurationManager.AppSettings["printer"] == "" || ConfigurationManager.AppSettings["printer"] == "не печатать")
                        {
                            OrderFormButtonPrint.IsEnabled = false;
                        }
                        else
                        {
                            OrderFormButtonPrint.IsEnabled = true;
                        }
                        break;
                }

                // общая информация по заказу                        
                OrderFormCreated.Text = order.Created.ToString("dd.MM.yy   HH:mm");
                OrderFormNumber.Text = order.Id.ToString();

                // Типы заказа
                switch (order.OrderTypeCode)
                {
                    // Тестовый заказ
                    case 0:
                        //OrderFormAgregatorNumber.Text = orderId.ToString();
                        //OrderFormCustomer.Text = "Фамилия Имя Отчество";
                        //OrderFormPhone.Text = "+70001112233";
                        //OrderFormCourierArrivement.Text = "чч:мм";
                        //OrderFormComment.Text = "дополнителная информация от агрегатора";
                        break;

                    // Яндекс Еда
                    case 1:
                        var yandexOrder = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.YandexOrder>(order.YandexOrder);
                        OrderFormAgregatorNumber.Text = yandexOrder.eatsId;
                        OrderFormCustomer.Text = yandexOrder.deliveryInfo.clientName;
                        OrderFormPhone.Text = yandexOrder.deliveryInfo.phoneNumber;
                        OrderFormCourierArrivement.Text = DateTime.Parse(yandexOrder.deliveryInfo.courierArrivementDate).ToString("MM.dd.yy   HH:mm");
                        OrderFormPersons.Text = yandexOrder.persons.ToString();
                        OrderFormComment.Text = yandexOrder.comment;
                        OrderFormTypeImage.Source = new BitmapImage(new Uri(@"/RKNet_CashClient;component/image/ya_logo.png", UriKind.RelativeOrAbsolute));
                        break;

                    // DeliveryClub
                    case 2:
                        var deliveryOrder = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.DeliveryOrder>(order.DeliveryOrder);
                        OrderFormAgregatorNumber.Text = deliveryOrder.originalOrderId;
                        OrderFormCustomer.Text = deliveryOrder.customer.name;
                        OrderFormPhone.Text = deliveryOrder.customer.phone;
                        OrderFormPersons.Text = deliveryOrder.personsQuantity.ToString();
                        OrderFormComment.Text = deliveryOrder.comment;
                        OrderFormTypeImage.Source = new BitmapImage(new Uri(@"/RKNet_CashClient;component/image/dc_logo.png", UriKind.RelativeOrAbsolute));
                        break;
                }

                // список позиций заказа
                OrderFormItems.Items.Clear();
                var orderItems = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RKNet_Model.MSSQL.MarketOrder.OrderItem>>(order.OrderItems);
                var total = 0;
                foreach (var item in orderItems.OrderBy(i => i.RkName))
                {
                    OrderFormItems.Items.Add(new Models.ItemElement
                    {
                        RkName = item.RkName,
                        MarketPrice = item.MarketPrice.ToString() + "р",
                        Quantity = item.Quantity.ToString() + "шт",
                        Cost = item.TotalCost.ToString() + "р"
                    });
                    total += item.TotalCost;
                }
                OrderFormTotal.Text = total.ToString() + "р";

                // окно заказа
                OrderForm.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OpenOrderForm: {ex.Message}");
            }
            
        }
        // Закрыть форму 
        private void CloseOrderForm(object sender, MouseButtonEventArgs e)
        {
            try
            {
                OrderForm.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода CloseOrderForm: {ex.Message}");
            }            
        }
        // кнопка: принять
        private void OrderFormButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var orderId = int.Parse(OrderFormNumber.Text);
                var order = OrdersList.FirstOrDefault(o => o.Id == orderId);

                signalConnection.OrderAccept(true, orderId);
                OrderForm.Visibility = Visibility.Hidden;
                ButtonDeliveryLoading.Visibility = Visibility.Visible;
                //if (order != null)
                //{
                //    PrintOrder(order);
                //}
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OrderFormButtonAccept_Click: {ex.Message}");
            }            
        }
        // кнопка: выдан
        private void OrderFormButtonFinish_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var orderId = int.Parse(OrderFormNumber.Text);
                var order = OrdersList.FirstOrDefault(o => o.Id == orderId);

                signalConnection.OrderFinish(orderId);
                OrderForm.Visibility = Visibility.Hidden;
                ButtonDeliveryLoading.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OrderFormButtonFinish_Click: {ex.Message}");
            }
            
        }
        // кнопка: отменить
        private void OrderFormButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OrdersListView.IsEnabled = false;
                OrderFormButtonAccept.IsEnabled = false;
                OrderFormButtonCancel.IsEnabled = false;

                ConfirmForm.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OrderFormButtonCancel_Click: {ex.Message}");
            }                        
        }
        // кнопка: печать
        private void OrderFormButtonPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var orderId = int.Parse(OrderFormNumber.Text);
                var order = OrdersList.FirstOrDefault(o => o.Id == orderId);
                if (order != null)
                {
                    PrintOrder(order);
                }
                Task.Delay(100).Wait();
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OrderFormButtonPrint_Click: {ex.Message}");
            }
            
        }

        // ----------------------------------------------------------------------------------
        // СПИСОК ЗАКАЗОВ
        // ----------------------------------------------------------------------------------
        // Закрыть список заказов
        private void CloseOrdersListWindow(object sender, MouseButtonEventArgs e)
        {
            try
            {
                OrdersListForm.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода CloseOrdersListWindow: {ex.Message}");
            }            
        }
        // Открыть заказ
        private void OrdersListView_ItemClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var listViewItem = sender as ListViewItem;
                if (listViewItem != null)
                {
                    // находим выбранный заказ
                    var element = (Models.OrderElement)listViewItem.Content;
                    var orderId = int.Parse(element.Number);
                    
                    OpenOrderForm(orderId);
                }
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода OrdersListView_ItemClick: {ex.Message}");
            }                                       
        }

        // ----------------------------------------------------------------------------------
        // ПОДТВЕРЖДЕНИЕ
        // ----------------------------------------------------------------------------------
        // OK
        private void ButtonConfirmOk_Click(object sender, RoutedEventArgs e)
        {            
            try
            {
                var orderId = int.Parse(OrderFormNumber.Text);
                signalConnection.OrderAccept(false, orderId);

                OrderForm.Visibility = Visibility.Hidden;
                ButtonDeliveryLoading.Visibility = Visibility.Visible;
                ConfirmForm.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода ButtonConfirmOk_Click: {ex.Message}");
            }
        }
        // CANCEL
        private void ButtonConfirmCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OrdersListView.IsEnabled = true;
                OrderFormButtonAccept.IsEnabled = true;
                OrderFormButtonCancel.IsEnabled = true;

                ConfirmForm.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода ButtonConfirmCancel_Click: {ex.Message}");
            }
            
        }

        // ----------------------------------------------------------------------------------
        // ПЕЧАТЬ И ПРИНТЕРЫ
        // ----------------------------------------------------------------------------------
        // обновление списка принтеров в контекстном меню
        private void RefreshPrinters()
        {
            try
            {
                TrayPrinters.Items.Clear();
                var printServer = new LocalPrintServer();
                var printQueuesOnLocalServer = printServer.GetPrintQueues();
                var printerName = ConfigurationManager.AppSettings["printer"];

                foreach (PrintQueue printQueue in printQueuesOnLocalServer.OrderBy(p => p.Name))
                {
                    var item = new MenuItem();
                    item.Header = printQueue.Name;
                    item.Click += PrinterSelectEvent;
                    if (printQueue.Name == printerName)
                    {
                        item.Icon = new ImageAwesome { Icon = FontAwesomeIcon.Check };
                    }
                    TrayPrinters.Items.Add(item);
                    if (isConnected)
                    {
                        signalConnection.SendPrinterInfo(printerName);
                    }
                }

                var defItem = new MenuItem();
                defItem.Header = "не печатать";
                defItem.Click += PrinterSelectEvent;
                if (printerName == "не печатать")
                {
                    defItem.Icon = new ImageAwesome { Icon = FontAwesomeIcon.Check };
                }
                TrayPrinters.Items.Add(defItem);
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода RefreshPrinters: {ex.Message}");
            }           
        }
        // печать заказа
        private void PrintOrder(RKNet_Model.MSSQL.MarketOrder order)
        {
            try
            {
                // проверяем наличие принтера
                if (ConfigurationManager.AppSettings["printer"] == "" || ConfigurationManager.AppSettings["printer"] == "не печатать")
                {
                    return;
                }

                // заполняем чек данными заказа
                //PrintOrderNumber.Text = order.Id.ToString();
                PrintOrderCreated.Text = order.Created.ToString("dd.MM.yy HH:mm");
                PrintOrderTotal.Text = $"ИТОГО: {order.OrderSum}р";

                var imagePath = string.Empty;

                switch (order.OrderTypeCode)
                {
                    // Тестовый заказ
                    case 0:
                        imagePath = @"/RKNet_CashClient;component/image/print_logo.png";
                        PrintOrderNumber.Text = "тест печати";
                        PrintOrderCustomer.Text = "Фамилия Имя Отчество";
                        PrintOrderClientPhone.Text = "+70001112233";
                        PrintOrderCourierPhone.Text = "+70001112233";
                        PrintOrderCallCentrPhone.Text = "+70001112233";
                        PrintOrderCourierArrivement.Text = "чч:мм";
                        PrintOrderPersons.Text = "0";
                        PrintOrderComment.Text = "дополнительная информация от агрегатора";
                        break;
                    // Яндекс Еда
                    case 1:
                        imagePath = @"/RKNet_CashClient;component/image/ya_logo.png";
                        var yandexOrder = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.YandexOrder>(order.YandexOrder);
                        PrintOrderNumber.Text = yandexOrder.eatsId;
                        PrintOrderCustomer.Text = yandexOrder.deliveryInfo.clientName;
                        PrintOrderClientPhone.Text = yandexOrder.deliveryInfo.phoneNumber;
                        PrintOrderCourierPhone.Text = string.Empty;
                        PrintOrderCallCentrPhone.Text = "8-800-600-12-10";
                        PrintOrderCourierArrivement.Text = DateTime.Parse(yandexOrder.deliveryInfo.courierArrivementDate).ToString("HH:mm");
                        PrintOrderPersons.Text = yandexOrder.persons.ToString();
                        PrintOrderComment.Text = yandexOrder.comment;
                        break;

                    // Delivery Club
                    case 2:
                        imagePath = @"/RKNet_CashClient;component/image/dc_logo.png";                        
                        var deliveryOrder = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.DeliveryOrder>(order.DeliveryOrder);
                        PrintOrderNumber.Text = deliveryOrder.originalOrderId;
                        PrintOrderCustomer.Text = deliveryOrder.customer.name;
                        PrintOrderClientPhone.Text = deliveryOrder.customer.phone;
                        PrintOrderCourierPhone.Text = deliveryOrder.courier.phone;
                        PrintOrderCallCentrPhone.Text = deliveryOrder.callCenter.phone;
                        PrintOrderCourierArrivement.Text = String.Empty;
                        PrintOrderPersons.Text = deliveryOrder.personsQuantity.ToString();
                        PrintOrderComment.Text = deliveryOrder.comment;
                        break;
                }

                if (!string.IsNullOrEmpty(imagePath))
                {
                    var uriImageSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                    PrintOrderTypeImage.Source = new BitmapImage(uriImageSource);
                }


                // список позиций
                PrintItems.Rows.Clear();
                var items = OrderFormItems.Items;
                var orderItems = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RKNet_Model.MSSQL.MarketOrder.OrderItem>>(order.OrderItems);
                foreach (var item in orderItems.OrderBy(i => i.RkName))
                {
                    var row = new TableRow();
                    var rkName = new Paragraph(new Run(item.RkName));
                    var price = new Paragraph(new Run(item.MarketPrice.ToString() + "р"));
                    var quantity = new Paragraph(new Run(item.Quantity.ToString() + "шт"));
                    var sum = new Paragraph(new Run(item.TotalCost.ToString() + "р"));

                    var cell = new TableCell(rkName);
                    cell.BorderThickness = new Thickness(0, 0, 10, 0);
                    row.Cells.Add(cell);

                    cell = new TableCell(price);
                    row.Cells.Add(cell);

                    cell = new TableCell(quantity);
                    cell.TextAlignment = TextAlignment.Center;
                    row.Cells.Add(cell);

                    cell = new TableCell(sum);
                    row.Cells.Add(cell);

                    PrintItems.Rows.Add(row);
                }

                // вывод на печать
                var printDialog = new PrintDialog();

                // выбираем принтер
                var printServer = new LocalPrintServer();
                var printers = printServer.GetPrintQueues();
                var printer = printers.FirstOrDefault(p => p.Name == ConfigurationManager.AppSettings["printer"]);

                if (printer == null)
                    return;

                printDialog.PrintQueue = printer;                


                // формируем страницу и выводим на печать
                var doc = ((IDocumentPaginatorSource)PrintDocument.Document).DocumentPaginator;
                doc.PageSize = new Size(290, doc.PageSize.Height);
                
                printDialog.PrintDocument(doc, $"заказ {order.OrderTypeName} №{order.Id}");
            }
            catch (Exception ex)
            {
                Logging.LocalLog($"ошибка метода PrintOrder: {ex.Message}");
            }            
        }        
    }
}
