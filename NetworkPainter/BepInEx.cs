using System;
using BepInEx;
using HarmonyLib;
using LaunchPadBooster;
using UnityEngine;

namespace NetworkPainter
{
    [BepInPlugin("net.elmo.stationeers.NetworkPainter", "NetworkPainter", "1.8")]
    public class NetworkPainterPlugin : BaseUnityPlugin
    {
        public static readonly Mod MOD = new Mod("NetworkPainter", "1.8");

        void Awake()
        {
            MOD.Networking.RegisterMessage<PaintModeMessage>();
            var harmony = new Harmony("net.elmo.stationeers.NetworkPainter");
            foreach (var type in typeof(NetworkPainterPlugin).Assembly.GetTypes())
            {
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Debug.Log($"[NetworkPainter]: Patch failed for {type.Name}: {e.Message}");
                }
            }
            Debug.Log("[NetworkPainter]: PatchAll complete");
        }
    }
}
