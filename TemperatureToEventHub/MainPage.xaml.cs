using ConnectTheDotsIoT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Devices.Gpio;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Connectivity;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TemperatureToEventHub
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int DATA_PIN = 24;
        private const int SCK_PIN = 23;


        private const int LED_PIN = 5;
        private GpioPin pin;

        // Timer
        private DispatcherTimer ReadSensorTimer;
        // SHT15 Sensor
        private SHT10 sht15 = null;

        // Sensor values
        public static double TemperatureC = 0.0;
        public static double TemperatureF = 0.0;
        public static double Humidity = 0.0;
        public static double CalculatedDewPoint = 0.0;


        int counter = 0; // dummy temp counter value;

        int uploadHelper = 0;
        int uploadspac = 3;


        public static double WarningTemperature = 30.0;
        public int WarningHelper = 60;

        ConnectTheDotsHelper ctdHelper;

        /// <summary>
        /// Main page constructor
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();

            InitGPIO();

            // Hard coding guid for sensors. Not an issue for this particular application which is meant for testing and demos
            List<ConnectTheDotsSensor> sensors = new List<ConnectTheDotsSensor>
            {
                new ConnectTheDotsSensor(),
            };

            ctdHelper = new ConnectTheDotsHelper(
               serviceBusNamespace: "iotwin10msg-ns",
               eventHubName: "ioteventhub",
               keyName: "SendRule",
               key: "OxJ8Nmw3oJBmpZH/S/aXOWT2s5mE2YuRko7OJ+yziec=",
               displayName: "YOUR_DEVICE_NAME",
               organization: "YOUR_ORGANIZATION_OR_SELF",
               location: "YOUR_LOCATION",
               sensorList: sensors);

            // Start Timer every 1 seconds
            ReadSensorTimer = new DispatcherTimer();
            ReadSensorTimer.Interval = TimeSpan.FromMilliseconds(500);
            ReadSensorTimer.Tick += Timer_Tick;
            ReadSensorTimer.Start();

            Unloaded += MainPage_Unloaded;

            InitializeSensor(DATA_PIN, SCK_PIN);

            // Initialize and Start HTTP Server
            HttpServer WebServer = new HttpServer();

            WebServer.RecivedMeg += (meg, eve) =>
            {
                this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    tbmeg.Text = meg.ToString();
                }).AsTask();

            };

            var asyncAction = ThreadPool.RunAsync((w) => { WebServer.StartServer(); });
            getip();
        }


        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                pin = null;
                tbmeg.Text = "There is no GPIO controller on this device.";
                return;
            }

            pin = gpio.OpenPin(LED_PIN);

            // Show an error if the pin wasn't initialized properly
            if (pin == null)
            {
                tbmeg.Text = "There were problems initializing the GPIO pin.";
                return;
            }

            pin.Write(GpioPinValue.High);
            pin.SetDriveMode(GpioPinDriveMode.Output);

            tbmeg.Text = "GPIO pin initialized correctly.";
        }

        private void MainPage_Unloaded(object sender, object args)
        {
            // Cleanup Sensor
            sht15.Dispose();
        }

        // Timer Ro
        private void Timer_Tick(object sender, object e)
        {
            // Read Raw Temperature and Humidity
            int RawTemperature = sht15.ReadRawTemperature();

            TemperatureC = sht15.CalculateTemperatureC(RawTemperature);
            TemperatureF = sht15.CalculateTemperatureF(RawTemperature);
            Humidity = sht15.ReadHumidity(TemperatureC);
            CalculatedDewPoint = sht15.DewPoint(TemperatureC, Humidity);

            StringBuilder _sb = new StringBuilder();
            //_sb.AppendLine("Time: " + DateTime.Now.ToString("h:mm:ss tt"));
            _sb.AppendLine("摄氏度: " + MainPage.TemperatureC.ToString(".00") + "℃");
            _sb.AppendLine("华氏度: " + MainPage.TemperatureF.ToString(".00") + "℉");
            _sb.AppendLine("湿度: " + MainPage.Humidity.ToString(".00") + "%RH");

            TB.Text = _sb.ToString();
            //_sb.AppendLine();
            //+  +  + ", Dew Point: " + CalculatedDewPoint;

            return;

            if (MainPage.TemperatureC >= WarningTemperature)
            {
                //When temperature >= 30 Set Pin value to low
                pin.Write(GpioPinValue.Low);

                //Push notification to notification hub
                WarningHelper++;
                if (WarningHelper >= 60)
                {
                    //App.MobileService.InvokeApiAsync("notifyAllUsers", new JObject(new JProperty("toast", "Temperature: " + String.Format("{0:0.00}", MainPage.TemperatureC + "C"))));
                    WarningHelper = 0;
                    Debug.WriteLine("Push Message!!");
                }
            }
            else
            {
                pin.Write(GpioPinValue.High);
                WarningHelper = 60;
            }

            uploadHelper++;
            if (uploadHelper >= uploadspac)
            {
                ConnectTheDotsSensor sensor = ctdHelper.sensors[0];
                sensor.guid = Guid.NewGuid().ToString();
                sensor.location = "Beijing";
                sensor.temperatureC = TemperatureC.ToString();
                sensor.temperatureF = TemperatureF.ToString();
                sensor.humidity = Humidity.ToString();
                //upload Data To EventHub
                ctdHelper.SendSensorData(sensor);
                uploadHelper = 0;
            }
            //Debug.WriteLine("Temperature: " + TemperatureC + " C, " + TemperatureF + " F, " + "Humidity: " + Humidity + ", Dew Point: " + CalculatedDewPoint);
            //Debug.WriteLine(_sb.ToString());
        }

        private void InitializeSensor(int datapin, int sckpin)
        {
            sht15 = new SHT10(DATA_PIN, SCK_PIN);
        }

        private void SendData(object sender, RoutedEventArgs e)
        {
        }


        public void getip()
        {
            var icp = NetworkInformation.GetInternetConnectionProfile();

            if (icp != null && icp.NetworkAdapter != null)
            {
                var hostname =
                    NetworkInformation.GetHostNames()
                        .SingleOrDefault(
                            hn =>
                            hn.IPInformation != null && hn.IPInformation.NetworkAdapter != null
                            && hn.IPInformation.NetworkAdapter.NetworkAdapterId
                            == icp.NetworkAdapter.NetworkAdapterId);

                if (hostname != null)
                {
                    // the ip address
                    ipadd.Text = hostname.CanonicalName;
                }

            }
        }

    }
}
