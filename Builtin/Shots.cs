using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Api;
using MarcosBodyshop.Shop;
using UnityEngine;

namespace MarcosBodyshop.Builtin
{
    /// <summary>Camera shots around a vehicle, from its visible size, for the built-in categories.</summary>
    internal static class Shots
    {
        private static void Box(LandVehicle v, out Vector3 c, out Vector3 half)
        {
            var b = Session.LocalBox(v);
            c = b.center;
            half = b.extents;
        }

        /// <summary>Over the bonnet from the front left.</summary>
        internal static CameraShot? Front(LandVehicle v)
        {
            Box(v, out var c, out var h);
            return CameraShot.Around(v, c + new Vector3(-h.x * 1.6f, h.y * 1.4f, h.z * 1.9f), c + new Vector3(0f, h.y * 0.3f, h.z * 0.6f), 55f);
        }

        /// <summary>Low at the front left wheel.</summary>
        internal static CameraShot? Wheel(LandVehicle v)
        {
            Box(v, out var c, out var h);
            Vector3 wheel = c + new Vector3(-h.x, -h.y * 0.55f, h.z * 0.6f);
            return CameraShot.Around(v, wheel + new Vector3(-h.x * 2.2f, h.y * 0.3f, h.z * 0.35f), wheel, 50f);
        }

        /// <summary>From the rear left, a little low.</summary>
        internal static CameraShot? Rear(LandVehicle v)
        {
            Box(v, out var c, out var h);
            return CameraShot.Around(v, c + new Vector3(-h.x * 1.8f, h.y * 0.2f, -h.z * 1.9f), c + new Vector3(0f, -h.y * 0.2f, -h.z * 0.3f), 55f);
        }

        /// <summary>The whole side, low.</summary>
        internal static CameraShot? Side(LandVehicle v)
        {
            Box(v, out var c, out var h);
            return CameraShot.Around(v, c + new Vector3(-h.x - h.z * 2.3f, h.y * 0.1f, 0f), c + new Vector3(0f, -h.y * 0.3f, 0f), 55f);
        }
    }
}
