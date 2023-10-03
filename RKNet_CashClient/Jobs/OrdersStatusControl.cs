using RKNet_CashClient.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RKNet_CashClient.Jobs
{
    public class OrdersStatusControl
    {
        private int Interval { get; set; }
        private List<RKNet_Model.MSSQL.MarketOrder> RemindOrdersList { get; set; }
        private MainWindow MainWindow { get; set; }

        public OrdersStatusControl()
        {

        }

        public OrdersStatusControl(MainWindow window)
        {
            MainWindow = window;
        }

        public async Task StartControl()
        {
            await StatusControl();
        }

        private Task StatusControl()
        {
            while (true)
            {
                GetRemindOrderList(MainWindow.OrdersList);
                Remind();
                Thread.Sleep(Interval);
            }
        }

        private void GetRemindOrderList(List<RKNet_Model.MSSQL.MarketOrder> OrdersList)
        {
            RemindOrdersList = new List<RKNet_Model.MSSQL.MarketOrder>();
            foreach (var item in OrdersList)
            {
                if ((DateTime.Now - item.Created).TotalMinutes > 60 && item.StatusCode < 6)
                {
                    RemindOrdersList.Add(item);
                }
            }
        }

        private void Remind()
        {

            Interval = 3600000;
            try
            {
                if (DateTime.Now.Hour >= 21)
                {
                    Interval = 900000;
                }
                if (RemindOrdersList.Count > 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        List<IDListBoxItem> idListBoxItems = new List<IDListBoxItem>();

                        foreach (var item in RemindOrdersList)
                        {
                            IDListBoxItem itemBox = new IDListBoxItem();
                            if (item.YandexOrder != null)
                            {
                                itemBox.Content += "Яндекс Еда " + item.OrderNumber + " " + item.Created + "\r\n";
                            }
                            else
                            {
                                itemBox.Content += "Маркет Деливери " + item.OrderNumber + " " + item.Created + "\r\n";
                            }
                            itemBox.ID = item.Id;
                            idListBoxItems.Add(itemBox);
                        }
                        AlertWindow alert = new AlertWindow(MainWindow, idListBoxItems);
                        alert.Show();
                    });
                }
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }

        }
    }
}
