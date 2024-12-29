using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OverloadPlugin
{
    internal class PlayerData
    {
        /// <summary>
        /// pitch (x), yaw (y), roll (z)
        /// </summary>
        public Vector3 Rotation = Vector3.Zero;

        /// <summary>
        /// pitch (x), yaw (y), roll (z)
        /// </summary>
        public Vector3 AngularVelocity = Vector3.Zero;

        /// <summary>
        /// sway (x), heave (y), surge (z)
        /// </summary>
        public Vector3 GForce = Vector3.Zero;

        /// <summary>
        /// pitch (x), yaw (y), roll (z)
        /// </summary>
        public Vector3 LocalAngularVelocity = Vector3.Zero;

        /// <summary>
        /// sway (x), heave (y), surge (z)
        /// </summary>
        public Vector3 LocalVelocity = Vector3.Zero;

        /// <summary>
        /// sway (x), heave (y), surge (z)
        /// </summary>
        public Vector3 LocalGForce = Vector3.Zero;

        public float EventBoosting;
        public float EventPrimaryFire;
        public float EventSecondaryFire;
        public float EventItemPickup;
        public float EventDamageTaken;

        public static PlayerData Parse(string packetString)
        {
            var playerData = new PlayerData();
            float[] parts = packetString.Split(';').Select(s => float.Parse(s)).ToArray();
            int expectedLength = 23;

            playerData.Rotation = new Vector3(parts[1], parts[2], parts[0]);
            playerData.AngularVelocity = new Vector3(parts[5], parts[4], parts[3]);
            playerData.GForce = new Vector3(parts[6], parts[7], parts[8]);
            playerData.EventBoosting = parts[9];
            playerData.EventPrimaryFire = parts[10];
            playerData.EventSecondaryFire = parts[11];
            playerData.EventItemPickup = parts[12];
            playerData.EventDamageTaken = parts[13];
            playerData.LocalGForce = new Vector3(parts[14], parts[15], parts[16]);
            playerData.LocalAngularVelocity = new Vector3(parts[17], parts[18], parts[19]);
            playerData.LocalVelocity = new Vector3(parts[20], parts[21], parts[22]);

            return playerData;
        }
    }
}
