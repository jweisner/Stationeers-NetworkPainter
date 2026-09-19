using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using HarmonyLib;
using LaunchPadBooster.Networking;
using UnityEngine;

namespace NetworkPainter
{
    public enum PaintMode : byte
    {
        Network = 0,
        Single = 1,
        Checkered = 2,
    }

    // Sent client -> server immediately before an AttackWithMessage when the
    // active hand holds a sprayer. The server reads it in OnServer.SetCustomColor.
    public class PaintModeMessage : INetworkMessage
    {
        public PaintMode Mode;

        public void Serialize(RocketBinaryWriter writer) => writer.WriteByte((byte)Mode);
        public void Deserialize(RocketBinaryReader reader) => Mode = (PaintMode)reader.ReadByte();

        public void Process(long clientId)
        {
            UnityEngine.Debug.Log($"[NetworkPainter] PaintModeMessage.Process: clientId={clientId} mode={Mode}");
            PaintModeStore.Set(clientId, Mode);
            // Also cache the clientId so SetCustomColor can look it up without
            // ThingColorMessage (which is only sent on listen-server, not dedicated).
            NetworkPainterMod.CurrentClientId = clientId;
        }
    }

    // Stores the most-recently received paint mode per client connection ID.
    public static class PaintModeStore
    {
        private static readonly Dictionary<long, PaintMode> _modes = new();

        public static void Set(long clientId, PaintMode mode) => _modes[clientId] = mode;

        public static PaintMode Get(long clientId)
        {
            _modes.TryGetValue(clientId, out var mode);
            return mode; // defaults to Network (0) if not set
        }
    }

    // Fires on both client and server when a tool is used on a thing.
    // On the client (IsClient=true, RunSimulation=false) this runs before
    // AttackWithMessage is sent, so we read key state here and send
    // PaintModeMessage to the server ahead of the paint.
    [HarmonyPatch(typeof(OnServer), nameof(OnServer.AttackWith))]
    public class PaintModeAttackWithPatch
    {
        public static void Prefix(Thing attackParent, byte activeHandSlotId)
        {
            if (!NetworkManager.IsClient)
                return;

            var item = attackParent?.Slots[activeHandSlotId]?.Get();
            if (item is not Assets.Scripts.Objects.Items.ISprayer)
                return;

            PaintMode mode;
            if (KeyManager.GetButton(KeyCode.LeftShift))
                mode = PaintMode.Single;
            else if (KeyManager.GetButton(KeyCode.LeftControl))
                mode = PaintMode.Checkered;
            else
                mode = PaintMode.Network;

            UnityEngine.Debug.Log($"[NetworkPainter] OnServer.AttackWith: sending PaintModeMessage mode={mode}");
            new PaintModeMessage { Mode = mode }.SendToHost();
        }
    }
}
