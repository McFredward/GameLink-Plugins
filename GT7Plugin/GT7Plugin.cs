using GT7Plugin.Properties;
using PluginHelper;

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using YawGLAPI;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Quaternion = System.Numerics.Quaternion;

namespace GT7Plugin
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "Gran Turismo 7")]
    [ExportMetadata("Version", "1.0")]
    public class GT7Plugin : Game
    {
        private IProfileManager controller;
        private IMainFormDispatcher dispacther;


        private Stopwatch suspStopwatch = new Stopwatch();
        public int STEAM_ID => 0;

        public string PROCESS_NAME => string.Empty;

        public bool PATCH_AVAILABLE => false;

        public string AUTHOR => "YawVR";

        public Stream Logo => GetStream("logo.png");

        public Stream SmallLogo => GetStream("recent.png");

        public Stream Background => GetStream("wide.png");

        public string Description => "";

        private UDPListener listener;
        private Cryptor cryptor;
        private FieldInfo[] fields = typeof(SimulatorPacket).GetFields();

        public LedEffect DefaultLED()
        {
            return dispacther.JsonToLED(Resources.defProfile);
        }

        public List<Profile_Component> DefaultProfile()
        {
            return dispacther.JsonToComponents(Resources.defProfile);
        }

        public void Exit()
        {
            listener.Stop();
        }

        public Dictionary<string, ParameterInfo[]> GetFeatures()
        {
            return new Dictionary<string, ParameterInfo[]>();
        }

        public string[] GetInputData()
        {
            var inputs = new List<string>();
            inputs.AddRange(fields.Select(f => f.Name));
            inputs.Add("Sway");
            return inputs.ToArray();
        }

        public void Init()
        {
            listener = new UDPListener();
            cryptor = new Cryptor();
            listener.OnPacketReceived += Listener_OnPacketReceived;
            suspStopwatch.Restart();
        }

        private void Listener_OnPacketReceived(object sender, byte[] buffer)
        {
            cryptor.Decrypt(buffer);
            SimulatorPacket sp = new SimulatorPacket();
            sp.Read(buffer);


            Vector3 carRotation = new Vector3(sp.RotationX, sp.RotationY, sp.RotationZ);
            Vector3 worldVelocity = new Vector3(sp.VelocityX, sp.VelocityY, sp.VelocityZ);


            var Q = new Quaternion(carRotation, sp.RelativeOrientationToNorth);
            var local_velocity = Maths.WorldtoLocal(Q, worldVelocity);

            var (pitch, yaw, roll) = Maths.ToEuler(Q, true);

            sp.RotationX = pitch;
            sp.RotationY = yaw;
            sp.RotationZ = roll;

            sp.VelocityX = local_velocity.X;
            sp.VelocityY = local_velocity.Y;
            sp.VelocityZ = local_velocity.Z;

            Vector3 angularVelocity = new Vector3(sp.AngularVelocityX, sp.AngularVelocityY, sp.AngularVelocityZ);
            var sway = CalculateCentrifugalAcceleration(local_velocity, angularVelocity);

            bool updateSusp = false;
            if (suspStopwatch.ElapsedMilliseconds > 100)
            {
                updateSusp = true;
                suspStopwatch.Restart();
            }

            for (int i = 0; i < fields.Length; i++)
            {

              
                //52,53,54,55
                if (i >= 52 && i <= 55)
                {

                    if (updateSusp)
                    {
                        controller.SetInput(i, Convert.ToSingle(fields[i].GetValue(sp)));
                    }
                
                }
                else
                {
                   controller.SetInput(i, Convert.ToSingle(fields[i].GetValue(sp)));
                }
            }

            controller.SetInput(fields.Length, sway);

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
            return assembly.GetManifestResourceStream(fullResourceName);
        }
        public float CalculateCentrifugalAcceleration(Vector3 velocity, Vector3 angularVelocity)
        {
            var Fc = velocity.Length() * angularVelocity.Length();

            return Fc * (angularVelocity.Y >= 0 ? 1 : -1);

        }



    }
}
