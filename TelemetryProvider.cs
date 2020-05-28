using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using static System.Decimal;
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

        public static double lerp(double a, double b, double f)
        {
            return a + f * (b - a);
        }

        TelemetryData lastTelemetryData = new TelemetryData();
        TelemetryData telemetryDataA = new TelemetryData();
        TelemetryData telemetryDataB = new TelemetryData();
        Queue<TelemetryData> telemetryQueue = new Queue<TelemetryData>();
        Stopwatch packetStopwatch = new Stopwatch();
        private bool deQueueStarted = false;


        private void DequeueThread()
        {
            while (true)
            {

                // Dieser Timer schlägt alle 10 Millisekunden zu. Hier sollten die neuesten Daten verfügbar sein.
                File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  TIMER " + Environment.NewLine);
                var telemetryQueueCount = telemetryQueue.Count;

                if (telemetryQueueCount > 0)
                {
                    File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  Queue Count " + telemetryQueueCount + Environment.NewLine);
                    RaiseEvent(OnTelemetryUpdate, new TelemetryEventArgs(new AeroflyFS2TelemetryInfo(telemetryQueue.Dequeue(), lastTelemetryData)));
                }
                else
                {
                    File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  Keine Daten mehr in Queue" + Environment.NewLine);
                }
                Thread.Sleep(10);
            }
        }

        private void UdpListenerThread()
        {


            UdpClient socket = new UdpClient { ExclusiveAddressUse = true };
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, _portNum));
            var endpoint = new IPEndPoint(IPAddress.Parse(_ipAddr), _portNum);
            Stopwatch sw = new Stopwatch();
            sw.Start();


            /*
            var mmTimer = new System.Timers.Timer();
            mmTimer.Interval = 10;


            mmTimer.Elapsed += delegate (object o, ElapsedEventArgs args)
            {
                DequeueThread
            };*/



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



                    // Alle Daten sind immer gefüllt


                    // Der Erste Datensatz ist noch nicht gesetzt
                    if (!telemetryDataA.isSet)
                    {
                        File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  setzte telemetry data A" + Environment.NewLine);
                        telemetryDataA = ParseReponse(Encoding.UTF8.GetString(socket.Receive(ref endpoint)));
                        packetStopwatch.Start();    // Erster Datensatz gelesen, warte auf zweiten

                    }
                    else
                    {
                        // Der Erste Datensatz war bereits gesetzt.
                        packetStopwatch.Stop();
                        var elapsedMillisecondsBetweenPackets = packetStopwatch.ElapsedMilliseconds;
                        File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  setzte telemetry data B (" + elapsedMillisecondsBetweenPackets + " Millisekunden später) " + Environment.NewLine);
                        telemetryDataB = ParseReponse(Encoding.UTF8.GetString(socket.Receive(ref endpoint)));

                        // Hier alle fehlenden Daten dazwischen interpolieren
                        // TODO: Evtl. direkt in dieser Funktion aufdie Queue schieben, ohne die unten folgende Schleife
                        List<TelemetryData> interpolatedElems = interpolateBetween(telemetryDataA, telemetryDataB, elapsedMillisecondsBetweenPackets);

                        // Auf die Queue packen
                        foreach (var interpolatedElem in interpolatedElems)
                        {
                            telemetryQueue.Enqueue(interpolatedElem);
                            File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  Füge generierten Datensatz ein..." + Environment.NewLine);
                        }

                        // Nach dem interpolieren Am Ende B nach A kopieren.
                        telemetryDataA = telemetryDataB;
                        telemetryDataA.isSet = false;
                        packetStopwatch.Reset();

                        // Nach dem ersten interpolieren eigene Frequenz starten:
                        //mmTimer.Start();
                        if (!deQueueStarted)
                        {
                            File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " -  Starting Dequeue" + Environment.NewLine);
                            deQueueStarted = true;

                            var m_ListeningThread = new Thread(DequeueThread);
                            m_ListeningThread.IsBackground = false;
                            m_ListeningThread.Start();

                            //mmTimer.Start();
                        }


                    }

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
        

        private void Run()
        {
            var m_ListeningThread = new Thread(UdpListenerThread);
            m_ListeningThread.IsBackground = false;
            m_ListeningThread.Start();


        }

        private List<TelemetryData> interpolateBetween(TelemetryData telemetryDataA, TelemetryData telemetryDataB, long elapsedMillisecondsBetweenPackets)
        {
            // hier alle Werte durchgehen und interpolieren

            List<TelemetryData> interpolatedElems = new List<TelemetryData>();

            var missingElems = (double)Floor(elapsedMillisecondsBetweenPackets / 10);

            for (int i = 0; i <= missingElems; i++)
            {
                var interpolatedTelemetryData = new TelemetryData();
                interpolatedTelemetryData = telemetryDataA;

                interpolatedTelemetryData.Pitch = (float)lerp(telemetryDataA.Pitch, telemetryDataB.Pitch, i / missingElems);
                interpolatedTelemetryData.Roll = (float) lerp(telemetryDataA.Roll, telemetryDataB.Roll, i / missingElems);
                interpolatedTelemetryData.Yaw = (float)lerp(telemetryDataA.Yaw, telemetryDataB.Yaw, i / missingElems);
                interpolatedTelemetryData.Sway = (float) lerp(telemetryDataA.Sway, telemetryDataB.Sway, i / missingElems);
                interpolatedTelemetryData.Surge = (float)lerp(telemetryDataA.Surge, telemetryDataB.Surge, i / missingElems);
                interpolatedTelemetryData.Heave = (float) lerp(telemetryDataA.Heave, telemetryDataB.Heave, i / missingElems);
                interpolatedTelemetryData.AirSpeed = (float) lerp(telemetryDataA.AirSpeed, telemetryDataB.AirSpeed, i / missingElems);
                interpolatedTelemetryData.GroundSpeed = (float)lerp(telemetryDataA.GroundSpeed, telemetryDataB.GroundSpeed, i / missingElems);

                interpolatedElems.Add(interpolatedTelemetryData);
            }
            return interpolatedElems;
        }


        private TelemetryData ParseReponse(string resp)
        {
            File.AppendAllText("D:\\test.txt", DateTime.Now + ": " + DateTime.Now.Ticks + " - " + resp + Environment.NewLine);
            // TODO: telemetryData könnte auch direkt A oder B sein
            TelemetryData telemetryData = new TelemetryData();
            string[] lines = resp.Split(';');
            telemetryData.Pitch = float.Parse(lines[0], CultureInfo.InvariantCulture);
            telemetryData.Roll = float.Parse(lines[1], CultureInfo.InvariantCulture);
            telemetryData.Yaw = float.Parse(lines[2], CultureInfo.InvariantCulture);
            telemetryData.Sway = float.Parse(lines[3], CultureInfo.InvariantCulture);
            telemetryData.Surge = float.Parse(lines[5], CultureInfo.InvariantCulture);
            telemetryData.Heave = float.Parse(lines[4], CultureInfo.InvariantCulture);
            telemetryData.AirSpeed = float.Parse(lines[9], CultureInfo.InvariantCulture);
            telemetryData.GroundSpeed = float.Parse(lines[10], CultureInfo.InvariantCulture);
            telemetryData.isSet = true;
            return telemetryData;
        }
    }
}