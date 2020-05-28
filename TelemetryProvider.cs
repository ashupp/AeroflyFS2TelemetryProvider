using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
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
            TelemetryUpdateFrequency = 10;
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
            TelemetryData telemetryDataA = new TelemetryData();
            TelemetryData telemetryDataB = new TelemetryData();
            
            UdpClient socket = new UdpClient {ExclusiveAddressUse = true};
            socket.Client.Bind(new IPEndPoint(IPAddress.Any,_portNum));
            var endpoint = new IPEndPoint(IPAddress.Parse(_ipAddr), _portNum);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int emptyCount = -1;

            var mmTimer = new System.Timers.Timer();
            mmTimer.Interval = 10;


            // 100 Hz Timer
            Random rnd = new Random();
            
            mmTimer.Elapsed += delegate(object o, ElapsedEventArgs args)
            {
                // Dieser Timer schlägt alle 10 Millisekunden zu. Hier sollten die neuesten Daten verfügbar sein.


                //TelemetryData telemetryData = ParseReponse(Encoding.UTF8.GetString(socket.Receive(ref endpoint)));
                TelemetryData telemetryData = new TelemetryData();
                telemetryData.Pitch = rnd.Next(-90, 90);
                    var argsx = new TelemetryEventArgs(new AeroflyFS2TelemetryInfo(telemetryData, lastTelemetryData));
                RaiseEvent(OnTelemetryUpdate, argsx);
                lastTelemetryData = telemetryData;
            };



            // Telemetriedaten entgegennehmen
            // Eine Stoppuhr verwenden 






            //mmTimer.Start();
            IsConnected = true;
            IsRunning = true;


             while (!_isStopped)
             {
                 try
                 {

                     // get data from game, 
                     if (socket.Available == 0)
                     {
                         if (sw.ElapsedMilliseconds > 200)
                         {
                             IsRunning = false;
                             IsConnected = false;
                             Thread.Sleep(1000);
                         }
                         continue;
                     }
                     IsConnected = true;
                     IsRunning = true;
                     


                            // Hier kommen die Daten rein
                            
                            TelemetryData currentTelemetryData = ParseReponse(Encoding.UTF8.GetString(socket.Receive(ref endpoint)));
                            if (currentTelemetryData.isEmptyTelemetry())
                            {
                                File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  is empty" + Environment.NewLine);
                                if (emptyCount == -1)
                                {
                                    File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  was -1 will be 1" + Environment.NewLine);
                                    emptyCount = 1;
                                }
                                else
                                {
                                    File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  will be counted up " + emptyCount + Environment.NewLine);
                                    emptyCount++;
                                }
                            }
                            else
                            {
                                File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  is not empty" + Environment.NewLine);

                                if (telemetryDataA.isSet)
                                {
                                    File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  setzte telemetry data B - hier beginnen auszugeben und auf null setzen" + Environment.NewLine);
                                    telemetryDataB = currentTelemetryData;
                                    telemetryDataA.isSet = false;

                                    // Puffer voll - ausgeben

                                    emptyCount = -1;
                                }
                                else
                                {
                                    File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  setzte telemetry data A" + Environment.NewLine);
                                    telemetryDataA = currentTelemetryData;
                                }


                            }


                            /*
                            TelemetryData telemetryData = ParseReponse(Encoding.UTF8.GetString(socket.Receive(ref endpoint)));
                            var args = new TelemetryEventArgs(new AeroflyFS2TelemetryInfo(telemetryData, lastTelemetryData));
                            RaiseEvent(OnTelemetryUpdate, args);
                            lastTelemetryData = telemetryData;
                            */
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
            //IsConnected = false;
            //IsRunning = false;
        }

        private void MmTimer_Elapsed(object sender, MultimediaElapsedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private TelemetryData ParseReponse(string resp)
        {
            File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " - " + resp + Environment.NewLine);
            TelemetryData telemetryData = new TelemetryData();
            string[] lines = resp.Split(';');
            telemetryData.Pitch = float.Parse(lines[0], CultureInfo.InvariantCulture);
            telemetryData.Roll = float.Parse(lines[1], CultureInfo.InvariantCulture);
            telemetryData.Yaw = float.Parse(lines[2], CultureInfo.InvariantCulture);
            telemetryData.Sway = float.Parse(lines[3], CultureInfo.InvariantCulture);
            telemetryData.Surge = float.Parse(lines[5], CultureInfo.InvariantCulture);
            telemetryData.Heave = float.Parse(lines[4], CultureInfo.InvariantCulture);
            //telemetryData.Heave = float.Parse(lines[8], CultureInfo.InvariantCulture);
            //telemetryData.AirSpeed = float.Parse(lines[9], CultureInfo.InvariantCulture);
            //telemetryData.GroundSpeed = float.Parse(lines[10], CultureInfo.InvariantCulture);
            return telemetryData;
        }
    }
}