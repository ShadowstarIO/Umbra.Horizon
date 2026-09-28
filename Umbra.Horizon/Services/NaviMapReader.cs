using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Umbra.Common;

namespace Umbra.Horizon.Services;

[Service]
internal sealed class NaviMapReader(IGameGui gameGui)
{
    public const int NaviMapIconsNodeIndex = 2;
    public const int AreaMapIconsNodeIndex = 3;
    public const int PlayerViewRotationOffset = 0x254;

    public unsafe bool TryGetCameraYaw(out float yawRadians)
    {
        yawRadians = 0f;
        var ptr = gameGui.GetAddonByName("_NaviMap").Address;
        if (ptr == nint.Zero) return false;

        var naviMap = (AtkUnitBase*)ptr;
        if (naviMap->UldManager.LoadedState != AtkLoadState.Loaded) return false;

        var rot = *(float*)(ptr + PlayerViewRotationOffset);
        yawRadians = rot * Heading.Deg2Rad;
        return true;
    }

    public unsafe float TryGetCameraYawOrFallback()
    {
        if (TryGetCameraYaw(out var yaw)) return yaw;

        var cam = CameraManager.Instance();
        if (cam == null) return 0f;
        var active = cam->GetActiveCamera();
        if (active == null) return 0f;
        return active->DirH;
    }

    public unsafe bool TryReadMapIcons(bool useAreaMap, out AtkUnitBase* addon, out AtkComponentNode* iconsRoot)
    {
        addon     = null;
        iconsRoot = null;

        var name  = useAreaMap ? "AreaMap" : "_NaviMap";
        var index = useAreaMap ? AreaMapIconsNodeIndex : NaviMapIconsNodeIndex;
        var ptr   = gameGui.GetAddonByName(name).Address;
        if (ptr == nint.Zero) return false;

        addon = (AtkUnitBase*)ptr;
        if (addon->UldManager.LoadedState != AtkLoadState.Loaded) return false;
        if (!addon->IsVisible) return false;
        if (addon->UldManager.NodeListCount <= index) return false;

        iconsRoot = (AtkComponentNode*)addon->UldManager.NodeList[index];
        return iconsRoot != null;
    }

    public static Vector2 NaviMapCenter => new(72f, 72f);
}
