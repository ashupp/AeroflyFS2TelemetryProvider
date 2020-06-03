using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
        private const string sharedMemoryFile = @"Local\SimToolsAerofly_FS2";
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
            TelemetryUpdateFrequency = 30;
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
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (!_isStopped)
            {
                try
                {
                    TelemetryData telemetryData = new TelemetryData();

                    FS2DataRaw telemetryDataRaw = (FS2DataRaw)readSharedMemory(typeof(FS2DataRaw), sharedMemoryFile);

                    telemetryData.Pitch = (float)telemetryDataRaw.FS2DataRawValues[0];
                    telemetryData.Roll = (float) telemetryDataRaw.FS2DataRawValues[1];
                    telemetryData.Yaw = (float)telemetryDataRaw.FS2DataRawValues[2];
                    telemetryData.Surge = (float)telemetryDataRaw.FS2DataRawValues[3];
                    telemetryData.Sway = (float)telemetryDataRaw.FS2DataRawValues[4];
                    telemetryData.Heave = (float) telemetryDataRaw.FS2DataRawValues[5];
                    telemetryData.AngularVelocityRoll = (float)telemetryDataRaw.FS2DataRawValues[6];
                    telemetryData.AngularVelocityPitch = (float) telemetryDataRaw.FS2DataRawValues[7];
                    telemetryData.AngularVelocityHeading = (float) telemetryDataRaw.FS2DataRawValues[8];

                    IsConnected = true;
                    IsRunning = true;

                    TelemetryEventArgs args = new TelemetryEventArgs(new TelemetryInfoElem(telemetryData, lastTelemetryData));
                    RaiseEvent(OnTelemetryUpdate, args);
                    lastTelemetryData = telemetryData;
                    sw.Restart();

                    if (sw.ElapsedMilliseconds > 500)
                    {
                        IsRunning = false;
                    }

                    Thread.Sleep(SamplePeriod);
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
    }
}