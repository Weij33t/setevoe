using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using ip;
using OxyPlot;
using OxyPlot.Series;

namespace ip
{
    public partial class ChartWindow : Window
    {
        public PlotModel PlotModel { get; set; }

        public ChartWindow(List<Subnet> subnets,int avip, IPAddress lastAddress,IPAddress currentFreeAddress)
        {
            InitializeComponent();

            PlotModel = new PlotModel { Title = "Распределение сетевого пространства" };
            

            var pieSeries = new PieSeries
            {
                StrokeThickness = 2.0,
                InsideLabelPosition = 0.7,
                AngleSpan = 360,
                StartAngle = 0,
               // LabelFormatString = "{0:F2}%"
            };

            



            var totalIPs = subnets.Sum(s => s.availableIPs);
            var usedIPs = subnets.Sum(s => s.Users);
            var freeIPs =avip- totalIPs ;


            foreach (var subnet in subnets)
            {
                //pieSeries.Slices.Add(new PieSlice(subnet.Address+" "+ subnet.Users.ToString(), subnet.Users));
                pieSeries.Slices.Add(new PieSlice(subnet.Address + "/" + subnet.pref.ToString(), subnet.availableIPs));
            }

            // Добавляем свободное адресное пространство
            if (currentFreeAddress != null)
            {
                pieSeries.Slices.Add(new PieSlice("Свободное пространство " + Environment.NewLine + currentFreeAddress.ToString() + "-" + lastAddress.ToString(), freeIPs));
            }

            PlotModel.Series.Add(pieSeries);
            DataContext = this;
        }

        private int CalculateSubnetMaskLength(int users)
        {
            return 32 - (int)Math.Ceiling(Math.Log(users + 2, 2));
        }
    }
}
