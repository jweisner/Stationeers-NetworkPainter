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
            PaintModeStore.Set(clientId, Mode);
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

    // Patches OnServer.AttackWith on the client: if the active-hand item is a
    // sprayer and we're a client, send PaintModeMessage before the game sends
    // its own AttackWithMessage, so the server has the mode ready.
    [HarmonyPatch(typeof(OnServer), nameof(OnServer.AttackWith))]
    public class PaintModeClientPatch
    {
        public static void Prefix(Thing attackParent, byte activeHandSlotId)
        {
            if (!NetworkManager.IsClient)
                return;

            var slot = attackParent?.Slots?[activeHandSlotId];
            if (slot?.Get() is not ISprayer)
                return;

            var mode = PaintMode.Network;
            if (KeyManager.GetButton(KeyCode.LeftShift))
                mode = PaintMode.Single;
            else if (KeyManager.GetButton(KeyCode.LeftControl))
                mode = PaintMode.Checkered;

            new PaintModeMessage { Mode = mode }.SendToHost();
        }
    }
}
