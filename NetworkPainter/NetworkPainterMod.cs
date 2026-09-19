using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using HarmonyLib;
using Objects.RoboticArm;
using UnityEngine;

namespace NetworkPainter
{
    [HarmonyPatch(typeof(OnServer), nameof(OnServer.SetCustomColor))]
    public class NetworkPainterMod
    {
        // Set by PaintModeMessage.Process (or ThingColorMessagePatch on a
        // listen server) immediately before this fires, and cleared again at the
        // end of this Prefix. 0 means a local paint: singleplayer, or the
        // listen-server host painting their own items.
        //
        // It MUST be cleared per paint. Leaving it set means the next local
        // paint takes the remote path and reads a stale client's mode, so on a
        // listen server the host's own Shift/Ctrl stop working as soon as any
        // client has painted.
        //
        // Clearing at the end is safe because the network walk is synchronous
        // on the server main thread, and because the walk calls the unpatched
        // instance method Thing.SetCustomColor rather than re-entering
        // OnServer.SetCustomColor.
        internal static long CurrentClientId;

        public static void Prefix(Thing thing, int colorIndex)
        {
            UnityEngine.Debug.Log($"[NetworkPainter] SetCustomColor.Prefix: thing={thing?.GetType().Name} colorIndex={colorIndex} CurrentClientId={CurrentClientId} IsServer={NetworkManager.IsServer} IsClient={NetworkManager.IsClient}");

            try
            {
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

                switch (thing)
                {
                    case HydroponicTray tray when tray.PipeNetwork != null:
                        foreach (var item in tray.PipeNetwork.StructureList)
                            if (item is HydroponicTray)
                                NPutility.TryPaint(thing, item.GetAsThing, colorIndex, checkered);
                        break;
                    case PassiveVent pv when pv.PipeNetwork != null:
                        foreach (var item in pv.PipeNetwork.StructureList)
                            if (item is PassiveVent)
                                NPutility.TryPaint(thing, item.GetAsThing, colorIndex, checkered);
                        break;
                    case Pipe pipe when pipe.PipeNetwork != null:
                        // Only actual pipe segments (Piping / PipingLong). Everything
                        // else deriving from Pipe is a device sitting on the run --
                        // tanks, vents/cowls, drains, connectors, planters. Painting
                        // those is wrong under checkered paint in particular, where
                        // the alternation encodes a gas mix or a two-colour label and
                        // a device picking up one half of it reads as wrong.
                        foreach (var item in pipe.PipeNetwork.StructureList)
                            if (item is Piping)
                                NPutility.TryPaint(thing, item.GetAsThing, colorIndex, checkered);
                        break;
                    case Cable cable when cable.CableNetwork != null:
                        foreach (var item in cable.CableNetwork.CableList)
                            NPutility.TryPaint(thing, item, colorIndex, checkered);
                        break;
                    case Chute chute when chute.ChuteNetwork != null:
                        foreach (var item in chute.ChuteNetwork.StructureList)
                            NPutility.TryPaint(thing, item.GetAsThing, colorIndex, checkered);
                        break;
                    case RoboticArmRailBase rail when rail.RoboticArmNetwork != null:
                        foreach (var item in rail.RoboticArmNetwork.StructureList)
                            if (!(item is RoboticArmDock))
                                NPutility.TryPaint(thing, item.GetAsThing, colorIndex, checkered);
                        break;
                }
            }
            finally
            {
                // Always clear, including on the early return for Single mode
                // and if the walk throws, so a stale id can never leak into the
                // next paint.
                CurrentClientId = 0;
            }
        }
    }

    public static class NPutility
    {
        public static void TryPaint(Thing original, Thing item, int colorIndex, bool checkered)
        {
            if (item == null)
                return;
            if (checkered && !CheckeredPaintCheck(original, item))
                return;

            Paint(item, colorIndex);
        }

        private static void Paint(Thing thing, int colorIndex)
        {
            thing.SetCustomColor(colorIndex);
        }

        private static bool CheckeredPaintCheck(Thing original, Thing thing)
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
