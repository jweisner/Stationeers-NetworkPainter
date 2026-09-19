using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using HarmonyLib;
using UnityEngine;

namespace NetworkPainter
{
    [HarmonyPatch(typeof(OnServer), nameof(OnServer.SetCustomColor))]
    public class NetworkPainterMod
    {
        // Set by ThingColorMessagePatch.Prefix immediately before this fires.
        // 0 means a local paint (listen-server host painting their own items).
        internal static long CurrentClientId;

        public static void Prefix(Thing thing, int colorIndex)
        {
            UnityEngine.Debug.Log($"[NetworkPainter] SetCustomColor.Prefix: thing={thing?.GetType().Name} colorIndex={colorIndex} CurrentClientId={CurrentClientId} IsServer={NetworkManager.IsServer} IsClient={NetworkManager.IsClient}");

            PaintMode mode;
            if (CurrentClientId == 0)
            {
                var shift = KeyManager.GetButton(KeyCode.LeftShift);
                var ctrl = KeyManager.GetButton(KeyCode.LeftControl);
                if (shift)
                    mode = PaintMode.Single;
                else if (ctrl)
                    mode = PaintMode.Checkered;
                else
                    mode = PaintMode.Network;
                UnityEngine.Debug.Log($"[NetworkPainter] SetCustomColor: local path shift={shift} ctrl={ctrl} mode={mode}");
            }
            else
            {
                mode = PaintModeStore.Get(CurrentClientId);
                UnityEngine.Debug.Log($"[NetworkPainter] SetCustomColor: remote path clientId={CurrentClientId} mode={mode}");
            }

            if (mode == PaintMode.Single)
                return;

            bool checkered = mode == PaintMode.Checkered;

            if (thing is HydroponicTray tray)
            {
                foreach (var item in tray.PipeNetwork.StructureList)
                    if (item is HydroponicTray && (!checkered || NPutility.CheckeredPaintCheck(thing, item.GetAsThing)))
                        NPutility.Paint(item.GetAsThing, colorIndex);
                return;
            }
            if (thing is PassiveVent pv)
            {
                foreach (var item in pv.PipeNetwork.StructureList)
                    if (item is PassiveVent && (!checkered || NPutility.CheckeredPaintCheck(thing, item.GetAsThing)))
                        NPutility.Paint(item.GetAsThing, colorIndex);
                return;
            }
            if (thing is Pipe pipe)
            {
                foreach (var item in pipe.PipeNetwork.StructureList)
                    if (!(item is PassiveVent) && !(item is HydroponicTray) && (!checkered || NPutility.CheckeredPaintCheck(thing, item.GetAsThing)))
                        NPutility.Paint(item.GetAsThing, colorIndex);
                return;
            }
            if (thing is Cable cable)
            {
                foreach (Cable item in cable.CableNetwork.CableList)
                    if (!checkered || NPutility.CheckeredPaintCheck(thing, item))
                        NPutility.Paint(item, colorIndex);
                return;
            }
            if (thing is Chute chute)
            {
                foreach (var item in chute.ChuteNetwork.StructureList)
                    if (!checkered || NPutility.CheckeredPaintCheck(thing, item.GetAsThing))
                        NPutility.Paint(item.GetAsThing, colorIndex);
                return;
            }
        }
    }

    public static class NPutility
    {
        public static void Paint(Thing thing, int colorIndex)
        {
            thing.SetCustomColor(colorIndex);
        }

        public static bool CheckeredPaintCheck(Thing original, Thing thing)
        {
            float one   = (Mathf.Round(Mathf.Abs(original.Position.x) * 2) % 2) == (Mathf.Round(Mathf.Abs(thing.Position.x) * 2) % 2) ? 1 : 0;
            float two   = (Mathf.Round(Mathf.Abs(original.Position.y) * 2) % 2) == (Mathf.Round(Mathf.Abs(thing.Position.y) * 2) % 2) ? 1 : 0;
            float three = (Mathf.Round(Mathf.Abs(original.Position.z) * 2) % 2) == (Mathf.Round(Mathf.Abs(thing.Position.z) * 2) % 2) ? 1 : 0;

            return (one + two + three) % 2 != 0;
        }
    }

    // Captures the clientId from ThingColorMessage.Process before it calls
    // SetCustomColor, so the SetCustomColor Prefix can read it.
    [HarmonyPatch(typeof(ThingColorMessage), nameof(ThingColorMessage.Process))]
    public class ThingColorMessagePatch
    {
        public static void Prefix(long hostId)
        {
            UnityEngine.Debug.Log($"[NetworkPainter] ThingColorMessage.Prefix: hostId={hostId}");
            NetworkPainterMod.CurrentClientId = hostId;
        }
    }
}
