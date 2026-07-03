using System;
using HarmonyLib;
using LaunchPadBooster;
using UnityEngine;

namespace NetworkPainter
{
    public class NetworkPainterEntrypoint : MonoBehaviour
    {
        public static readonly Mod MOD = new Mod("net.elmo.stationeers.NetworkPainter", "1.5");

        public void OnLoaded()
        {
            try
            {
                var harmony = new Harmony(MOD.ID.Name);
                harmony.PatchAll(typeof(NetworkPainterEntrypoint).Assembly);
                Debug.Log("[NetworkPainter]: Patch succeeded");
            }
            catch (Exception e)
            {
                Debug.Log("[NetworkPainter]: Patch failed");
                Debug.Log(e.ToString());
            }
        }
    }
}
