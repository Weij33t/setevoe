using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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

namespace ip
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<Subnet> subnets = new List<Subnet>();
        int availableIPs = 0;
        IPAddress lastAddress ;
        IPAddress currentFreeAddress;

        public MainWindow()
        {
            InitializeComponent();
            InitializeAdapters();
        }

        private void InitializeAdapters()
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Select(ni => ni.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                .Where(ip => ip != null)
                .Select(ip => $"{ip.Address}/{ip.IPv4Mask}");

            AdapterComboBox.ItemsSource = adapters;
        }

        private void AdapterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AdapterComboBox.SelectedItem != null)
            {
                var adapterInfo = AdapterComboBox.SelectedItem.ToString().Split('/');
                IPAddress ipAddress = IPAddress.Parse(adapterInfo[0]);
                IPAddress subnetMask = IPAddress.Parse(adapterInfo[1]);
                IPAddress networkAddress=GetNetworkAddress(ipAddress, subnetMask);                
                NetworkAddressTextBox.Text = networkAddress.ToString(); 
                SubnetMaskTextBox.Text = adapterInfo[1];
            }
        }

        static IPAddress GetNetworkAddress(IPAddress ipAddress, IPAddress subnetMask)
        {
            // Преобразуем IP-адрес и маску подсети в массивы байтов
            byte[] ipAddressBytes = ipAddress.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            // Проверяем, что длина массивов одинакова
            if (ipAddressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Длина IP-адреса и маски подсети должна совпадать.");

            // Вычисляем сетевой адрес
            byte[] networkAddressBytes = new byte[ipAddressBytes.Length];
            for (int i = 0; i < networkAddressBytes.Length; i++)
            {
                networkAddressBytes[i] = (byte)(ipAddressBytes[i] & subnetMaskBytes[i]);
            }

            // Возвращаем сетевой адрес как объект IPAddress
            return new IPAddress(networkAddressBytes);
        }
        private void AddSubnetsButton_Click(object sender, RoutedEventArgs e)
        {
            SubnetsItemsControl.ItemsSource = null;
            ResultsDataGrid.ItemsSource = null;
            if (int.TryParse(SubnetCountTextBox.Text, out int subnetCount))
            {
                SubnetsItemsControl.ItemsSource = Enumerable.Range(0, subnetCount)
                    .Select(i => new Subnet { Index = i });
            }
        }

        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            subnets.Clear();
            string SubnetMaskText = SubnetMaskTextBox.Text;
            if (SubnetMaskText.IndexOf(".") ==-1)
            {
                int mask = 0;
                if(!Int32.TryParse(SubnetMaskText,out mask))
                {
                    MessageBox.Show("Неверный формат адреса сети или маски.");
                    return;
                }
                if (mask < 0 || mask > 32)
                {
                    MessageBox.Show("Префикс должен находится в диапазоне от 0 до 32");
                    return;
                }
                SubnetMaskText = GetSubnetMaskFromPrefix(mask).ToString();
            }

            if (!IPAddress.TryParse(NetworkAddressTextBox.Text, out IPAddress networkAddress) ||
                !IPAddress.TryParse(SubnetMaskText, out IPAddress subnetMask))
            {
                MessageBox.Show("Неверный формат адреса сети или маски.");
                return;
            }

            if (!int.TryParse(SubnetCountTextBox.Text, out int subnetCount))
            {
                MessageBox.Show("Неверное количество подсетей.");
                return;
            }

            var usersPerSubnet = new List<int>();
            foreach (var item in SubnetsItemsControl.Items)
            {
                var subnet = item as Subnet;
                //if (subnet != null && int.TryParse(subnet.UsersTextBox.Text, out int users))
                {
                    usersPerSubnet.Add(subnet.Users);
                }
            }

            if (usersPerSubnet.Count != subnetCount)
            {
                MessageBox.Show("Неверное количество пользователей в подсетях.");
                return;
            }

            // Расчет количества доступных IP-адресов в сети
            availableIPs = CalculateAvailableIPs(subnetMask)+2;
            int useip = 0;
            // Сортировка подсетей по количеству пользователей (от большего к меньшему)
            usersPerSubnet.Sort((a, b) => b.CompareTo(a));

            // Распределение IP-адресов между подсетями
            var currentAddress = networkAddress;
            bool f = false;
            foreach (var users in usersPerSubnet)
            {
                var subnetMaskLength = CalculateSubnetMaskLength(users+2);
                var subnetMaskBytes = SubnetMaskFromLength(subnetMaskLength);
                var subnetMaskAddress = new IPAddress(subnetMaskBytes);
                useip+= CalculateAvailableIPs(subnetMaskAddress)+2;

                if (useip > availableIPs)
                {                    
                    f=true;
                    break;
                }
                var subnet = new Subnet
                {
                    Address = currentAddress.ToString(),
                    Mask = subnetMaskAddress.ToString()+"/"+ GetSubnetPrefixLength(subnetMaskAddress),
                    pref= GetSubnetPrefixLength(subnetMaskAddress),
                    Users = users,
                    SubnetIP = currentAddress.ToString(),
                    availableIPs = CalculateAvailableIPs(subnetMaskAddress) + 2,
                    BroadcastAddress = CalculateBroadcastAddress(currentAddress, subnetMaskAddress).ToString(),
                    AddressRange = $"{CalculateFirstUsableAddress(currentAddress, subnetMaskAddress)} - {CalculateLastUsableAddress(currentAddress, subnetMaskAddress)}"
                };
                if (CalculateBroadcastAddress(networkAddress, subnetMask).ToString() == subnet.BroadcastAddress)
                { 
                    f= true;
                }

                    subnets.Add(subnet);

                // Переход к следующей подсети
                currentAddress = NextSubnetAddress(currentAddress, subnetMaskAddress);
            }
           
                      

            // Отображение результатов в таблице
            ResultsDataGrid.ItemsSource = null;
            ResultsDataGrid.ItemsSource = subnets;
            if (!f)
            {
                lastAddress = CalculateBroadcastAddress(networkAddress, subnetMask);
                currentFreeAddress = NextSubnetAddress(currentAddress, subnetMask);
                FreeAddressesTextBlock.Text = "Свободное пространство " + currentFreeAddress.ToString() + "-" + lastAddress.ToString();
            }
        }

        static int GetSubnetPrefixLength(IPAddress subnetMask)
        {
            byte[] maskBytes = subnetMask.GetAddressBytes();
            int prefixLength = 0;

            foreach (byte maskByte in maskBytes)
            {
                // Преобразуем байт в двоичное представление и считаем единицы
                prefixLength += CountBits(maskByte);
            }

            return prefixLength;
        }

        static int CountBits(byte value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= (byte)(value - 1); // Сбрасываем младший установленный бит
            }
            return count;
        }

        static IPAddress GetSubnetMaskFromPrefix(int prefixLength)
        {           
            uint mask = 0xFFFFFFFF << (32 - prefixLength);
            byte[] bytes = BitConverter.GetBytes(mask);

            // Если система использует little-endian, нужно перевернуть байты
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return new IPAddress(bytes);
        }
        private int CalculateAvailableIPs(IPAddress subnetMask)
        {
            var maskBytes = subnetMask.GetAddressBytes();
            var maskLength = BitConverter.ToInt32(maskBytes.Reverse().ToArray(), 0);
            var availableIPs = (int)Math.Pow(2, 32 - BitCount(maskLength)) - 2;
            return availableIPs;
        }

        private int BitCount(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= value - 1;
            }
            return count;
        }

        private int CalculateSubnetMaskLength(int users)
        {
            return 32 - (int)Math.Ceiling(Math.Log(users + 2, 2));
        }

        private byte[] SubnetMaskFromLength(int length)
        {
            var mask = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (length >= 8)
                {
                    mask[i] = 255;
                    length -= 8;
                }
                else if (length > 0)
                {
                    mask[i] = (byte)(256 - (1 << (8 - length)));
                    length = 0;
                }
                else
                {
                    mask[i] = 0;
                }
            }
            return mask;
        }

        private IPAddress NextSubnetAddress(IPAddress currentAddress, IPAddress subnetMask)
        {
            var currentBytes = currentAddress.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();
            for (int i = 3; i >= 0; i--)
            {
                currentBytes[i] += (byte)((~maskBytes[i] & 0xFF)+1);
                if (currentBytes[i] != 0) break;
            }
            return new IPAddress(currentBytes);
        }


        private IPAddress CalculateBroadcastAddress(IPAddress networkAddress, IPAddress subnetMask)
        {
            var networkBytes = networkAddress.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();
            for (int i = 0; i < 4; i++)
            {
                networkBytes[i] |= (byte)(~maskBytes[i] & 0xFF);
            }
            return new IPAddress(networkBytes);
        }

        private string CalculateFirstUsableAddress(IPAddress networkAddress, IPAddress subnetMask)
        {
            var networkBytes = networkAddress.GetAddressBytes();
            networkBytes[3] += 1;
            return new IPAddress(networkBytes).ToString();
        }

        private string CalculateLastUsableAddress(IPAddress networkAddress, IPAddress subnetMask)
        {
            var broadcastBytes = CalculateBroadcastAddress(networkAddress, subnetMask).GetAddressBytes();
            broadcastBytes[3] -= 1;
            return new IPAddress(broadcastBytes).ToString();
        }

        private void BuildChartButton_Click(object sender, RoutedEventArgs e)
        {
            var chartWindow = new ChartWindow(subnets, availableIPs, lastAddress, currentFreeAddress);
            chartWindow.Show();
        }
    }

    public class Subnet
    {
        public int Index { get; set; }
        public string Address { get; set; }
        public string Mask { get; set; }
        public string SubnetIP { get; set; }
        public string AddressRange { get; set; }
        public string BroadcastAddress { get; set; }
        public int Users { get; set; }
        public TextBox UsersTextBox { get; set; }
        public int availableIPs { get; set; }
        public int pref {  get; set; }

        public Subnet()
        {
            UsersTextBox = new TextBox();
            UsersTextBox.DataContext = this;
        }
    }




}

