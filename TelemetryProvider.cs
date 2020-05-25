using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using ILogger = SimFeedback.log.ILogger;

namespace SimFeedback.telemetry
{
    public sealed class TelemetryProvider : AbstractTelemetryProvider
    {

        private const int _portNum = 4123;
        private const string _ipAddr = "127.0.0.1";
        private bool _isStopped = true;
        private Thread _t;


        public TelemetryProvider()
        {
            Author = "ashupp / ashnet GmbH";
            Version = Assembly.LoadFrom(Assembly.GetExecutingAssembly().Location).GetName().Version.ToString();
            BannerImage = @"img\banner_aeroflyfs2.png";
            IconImage = @"img\icon_aeroflyfs2.png";
            TelemetryUpdateFrequency = 60;
        }

        public override string Name => "aeroflyfs2";

        public override void Init(ILogger logger)
        {
            base.Init(logger);
            Log("Initializing " + Name + "TelemetryProvider");
        }

        public override string[] GetValueList()
        {
            return GetValueListByReflection(typeof(TelemetryData));
        }

        public override void Stop()
        {
            if (_isStopped) return;
            LogDebug("Stopping " + Name + "TelemetryProvider");
            _isStopped = true;
            if (_t != null) _t.Join();
        }

        public override void Start()
        {
            if (_isStopped)
            {
                LogDebug("Starting " + Name + "TelemetryProvider");
                _isStopped = false;
                _t = new Thread(Run);
                _t.Start();
            }
        }

        private void Run()
        {
            TelemetryData lastTelemetryData = new TelemetryData();

            UdpClient socket = new UdpClient {ExclusiveAddressUse = false};
            socket.Client.Bind(new IPEndPoint(IPAddress.Parse(_ipAddr),_portNum));
            var endpoint = new IPEndPoint(IPAddress.Parse(_ipAddr), _portNum);
            Stopwatch sw = new Stopwatch();
            sw.Start();

            while (!_isStopped)
            {
                try
                {

                    // get data from game, 
                    if (socket.Available == 0)
                    {
                        if (sw.ElapsedMilliseconds > 500)
                        {
                            IsRunning = false;
                            IsConnected = false;
                            Thread.Sleep(1000);
                        }
                        continue;
                    }
                    IsConnected = true;

                    var received = socket.Receive(ref endpoint);
                    var resp = Encoding.UTF8.GetString(received);
                    TelemetryData telemetryData = ParseReponse(resp);

                    IsRunning = true;

                    var args = new TelemetryEventArgs(new AeroflyFS2TelemetryInfo(telemetryData, lastTelemetryData));
                    RaiseEvent(OnTelemetryUpdate, args);
                    lastTelemetryData = telemetryData;

                    sw.Restart();
                }
                catch (Exception e)
                {
                    LogError(Name + "TelemetryProvider Exception while processing data", e);
                    IsConnected = false;
                    IsRunning = false;
                    Thread.Sleep(1000);
                }
            }

            IsConnected = false;
            IsRunning = false;
        }

        private TelemetryData ParseReponse(string resp)
        {
            TelemetryData telemetryData = new TelemetryData();
            string[] lines = resp.Split(';');
            if (lines.Length < 11) return telemetryData;
            telemetryData.Pitch = float.Parse(lines[0], CultureInfo.InvariantCulture);
            telemetryData.Roll = float.Parse(lines[1], CultureInfo.InvariantCulture);
            telemetryData.Yaw = float.Parse(lines[2], CultureInfo.InvariantCulture);
            telemetryData.Sway = float.Parse(lines[3], CultureInfo.InvariantCulture);
            telemetryData.Surge = float.Parse(lines[5], CultureInfo.InvariantCulture);
            telemetryData.Heave = float.Parse(lines[8], CultureInfo.InvariantCulture);
            telemetryData.AirSpeed = float.Parse(lines[9], CultureInfo.InvariantCulture);
            telemetryData.GroundSpeed = float.Parse(lines[10], CultureInfo.InvariantCulture);

            return telemetryData;
        }
    }
}