﻿using Microsoft.VisualBasic;

using PluginHelper;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;

using YawGLAPI;

using Quaternion = System.Numerics.Quaternion;

namespace GT7Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Gran Turismo 7")]
    [ExportMetadata("Version", "1.1")]
    public class GranTurismo7Plugin : Game
    {
        private IProfileManager controller;
        private IMainFormDispatcher dispacther;


        private Stopwatch suspStopwatch = new Stopwatch();
        public int STEAM_ID => 0;

        public string PROCESS_NAME => string.Empty;

        public bool PATCH_AVAILABLE => false;

        public string AUTHOR => "Trevor Jones";

        public Stream Logo => GetStream("logo.png");

        public Stream SmallLogo => GetStream("recent.png");

        public Stream Background => GetStream("wide.png");

        public string Description => GetString("description.html");

        private string defProfilejson => GetString("DefaultProfile.yawglprofile");

        private UDPListener listener;
        private Cryptor cryptor;
        private FieldInfo[] fields = typeof(GT7Output).GetFields();
        private bool _seenPacket = false;
        private Vector3 _previous_local_velocity = new Vector3(0, 0, 0);
        private const float _samplerate = 1 / 60f;        

        public LedEffect DefaultLED() => dispacther.JsonToLED(defProfilejson);

        public List<Profile_Component> DefaultProfile() => dispacther.JsonToComponents(defProfilejson);


        public void Exit() => listener?.Stop();

        public Dictionary<string, ParameterInfo[]> GetFeatures() => new();

        public string[] GetInputData() => fields.Select(f => f.Name).ToArray();

        private int inputPort = 33740;

        public void Init()
        {
            

            if (!int.TryParse(Interaction.InputBox("Enter the incoming data port for Gran Turismo 7 \nLeave default value if its running on the Playstation", "Endpoint", "33740"), out inputPort)
                || inputPort < 0 || inputPort > 65535)
            {
                inputPort = 33740;
            }

            listener = new UDPListener(inputPort);
            cryptor = new Cryptor();
            listener.OnPacketReceived += Listener_OnPacketReceived;
            suspStopwatch.Restart();
        }

        private void Listener_OnPacketReceived(object sender, byte[] buffer)
        {
            cryptor.Decrypt(buffer);
            var sp = new SimulatorPacket();
            sp.Read(buffer);

            if (sp.Flags.HasFlag(SimulatorFlags.CarOnTrack) && !sp.Flags.HasFlag(SimulatorFlags.Paused) && !sp.Flags.HasFlag(SimulatorFlags.LoadingOrProcessing))
            {
                ReadFunction(sp);

            }

            _seenPacket = _seenPacket || true;

        }

        private void ReadFunction(SimulatorPacket sp)
        {
            var Q = new Quaternion(new Vector3(sp.RotationX, sp.RotationY, sp.RotationZ), sp.RelativeOrientationToNorth);
            var local_velocity = Maths.WorldtoLocal(Q, new Vector3(sp.VelocityX, sp.VelocityY, sp.VelocityZ));


            var sway = CalculateCentripetalAcceleration(local_velocity, new Vector3(sp.AngularVelocityX, sp.AngularVelocityY, sp.AngularVelocityZ));
            var surge = 0f;
            var heave = 0f;

            if (_seenPacket)
            {
                var delta_velocity = local_velocity - _previous_local_velocity;

                surge = delta_velocity.Z / _samplerate / 9.81f;
                heave = delta_velocity.Y / _samplerate / 9.81f;
            }



            _previous_local_velocity = local_velocity;


            var (pitch, yaw, roll) = Maths.ToEuler(Q, true);


            bool updateSusp = false;

            if (suspStopwatch.ElapsedMilliseconds > 100)
            {
                updateSusp = true;
                suspStopwatch.Restart();
            }

            var output = new GT7Output()
            {
                Yaw = yaw,
                Pitch = pitch,
                Roll = roll,
                Sway = sway,
                Surge = surge,
                Heave = heave,
                Kph = sp.MetersPerSecond * 3.6f,
                MaxKph = sp.CalculatedMaxSpeed * 3.6f,
                RPM = sp.EngineRPM,                
                OnTrack = sp.Flags.HasFlag(SimulatorFlags.CarOnTrack) ? 1 : 0,
                IsPaused = sp.Flags.HasFlag(SimulatorFlags.Paused) ? 1 : 0,
                Loading = sp.Flags.HasFlag(SimulatorFlags.LoadingOrProcessing) ? 1 : 0
            };

            if (updateSusp)
            {
                output.TireFL_SusHeight = sp.TireFL_SusHeight;
                output.TireFR_SusHeight = sp.TireFR_SusHeight;
                output.TireRL_SusHeight = sp.TireRL_SusHeight;
                output.TireRR_SusHeight = sp.TireRR_SusHeight;
            }

            for (int i = 0; i < fields.Length; i++)
            {
                controller.SetInput(i, Convert.ToSingle(fields[i].GetValue(output)));
            }
        }

        public void PatchGame()
        {
        }

        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.controller = controller;
            this.dispacther = dispatcher;
        }

        Stream GetStream(string resourceName)
        {
            var assembly = GetType().Assembly;
            var rr = assembly.GetManifestResourceNames();
            
            string fullResourceName = $"{assembly.GetName().Name}.Resources.{resourceName}";

            if (!rr.Contains(fullResourceName))
            {
                dispacther.ShowNotification(NotificationType.ERROR, "Resource not found - " + fullResourceName);
            }
            
            

            return assembly.GetManifestResourceStream(fullResourceName);
        }

        private string GetString(string resourceName)
        {

            var result = string.Empty;
            try
            {
                using var stream = GetStream(resourceName);

                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    result = reader.ReadToEnd();
                }
            }
            catch(Exception e)
            {
                Debug.WriteLine(e.Message);
                dispacther.ShowNotification(NotificationType.ERROR, "Error loading resource - " + e.Message);
            }


            return result;
        }


        public float CalculateCentripetalAcceleration(Vector3 velocity, Vector3 angularVelocity)
        {
            var Fc = velocity.Length() * angularVelocity.Length();

            return Fc * (angularVelocity.Y >= 0 ? -1 : 1);

        }


        
    }
}
