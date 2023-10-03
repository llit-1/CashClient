using RKNet_CashClient.Models;
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
using System.Windows.Shapes;

namespace RKNet_CashClient
{
    /// <summary>
    /// Логика взаимодействия для AlertWindow.xaml
    /// </summary>
    public partial class AlertWindow : Window
    {
        public MainWindow MainWindow { get; set; }
        public List<IDListBoxItem> IDListBoxItems { get; set; }
        public AlertWindow()
        {
            
        }
        public AlertWindow(MainWindow mainWindow, List<IDListBoxItem> idListBoxItems)
        {
            InitializeComponent();
            MainWindow = mainWindow;
            IDListBoxItems = idListBoxItems;
            foreach (var item in idListBoxItems)
            {
                item.Selected += ItemClick;
                OrderList.Items.Add(item);
            }
        }

        public void ButOkClick(object o, RoutedEventArgs eventArgs)
        {
            this.Close();
        }

        public void ItemClick(object o, RoutedEventArgs args)
        {
            IDListBoxItem idListBoxItem = (IDListBoxItem)o;
            MainWindow.OpenOrderForm(idListBoxItem.ID);
        }
    }
}
