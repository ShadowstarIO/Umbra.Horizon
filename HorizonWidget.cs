using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Umbra.Common;
using Umbra.Horizon.Services;
using Umbra.Widgets;

namespace Umbra.Horizon.Widgets;

[ToolbarWidget(
    "Horizon",
    "Horizon",
    "Wide flattened heading strip. Cardinals and minimap icons projected onto a 1D compass."
)]
public sealed class HorizonWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    protected override StandardWidgetFeatures Features => StandardWidgetFeatures.None;

    public override WidgetPopup? Popup => null;

    protected override string DefaultSizingMode => SizingModeFixed;
    protected override int    DefaultWidth      => 420;

    private readonly NaviMapReader _navi   = Framework.Service<NaviMapReader>();
    private readonly ICondition    _cond   = Framework.Service<ICondition>();
    private readonly IClientState  _player = Framework.Service<IClientState>();

    private HashSet<uint> _filtered = [];

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return
        [
            ..base.GetConfigVariables(),

            new IntegerWidgetConfigVariable("StripHeight", "Strip height", "Pixel height of the heading strip.", 28, 16, 64),
            new IntegerWidgetConfigVariable("FieldOfView", "Field of view (deg)", "Visible heading span. Higher is flatter.", 160, 60, 180),
            new FloatWidgetConfigVariable("IconScale", "Icon scale", "Base scale for projected map icons.", 0.7f, 0.3f, 1.5f),
            new FloatWidgetConfigVariable("MinIconScale", "Minimum icon scale", "Scale floor for distant icons.", 0.35f, 0.1f, 1f),
            new BooleanWidgetConfigVariable("ShowCardinals", "Show cardinals", null, true),
            new BooleanWidgetConfigVariable("ShowInterCardinals", "Show intercardinals", null, false),
            new BooleanWidgetConfigVariable("ShowCenterMarker", "Show center marker", null, true),
            new BooleanWidgetConfigVariable("ShowWeather", "Show weather icon", null, false),
            new BooleanWidgetConfigVariable("ShowDistance", "Show distance to target", null, true),
            new BooleanWidgetConfigVariable("ShowMapIcons", "Show minimap icons", null, true),
            new BooleanWidgetConfigVariable("UseAreaMap", "Use area map as icon source", null, false),
            new IntegerWidgetConfigVariable("Visibility", "Visibility (0 always, 1 out of combat, 2 in combat)", null, 0, 0, 2),
            new StringWidgetConfigVariable("FilteredIconIds", "Hidden icon IDs", "Comma-separated icon IDs to skip.", string.Empty, 512),
        ];
    }

    protected override void OnLoad()
    {
        Node.OnDraw += DrawStrip;
        RebuildFilter();
    }

    protected override void OnDraw()
    {
        var w = GetConfigValue<int>("Width");
        var h = GetConfigValue<int>("StripHeight");
        Node.Style.Size = new(w <= 0 ? 420 : w, h);
    }

    protected override void OnUnload()
    {
        Node.OnDraw -= DrawStrip;
    }

    private void RebuildFilter()
    {
        _filtered = [];
        var raw = GetConfigValue<string>("FilteredIconIds");
        if (string.IsNullOrWhiteSpace(raw)) return;
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (uint.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                _filtered.Add(id);
        }
    }

    private void DrawStrip(ImDrawListPtr _)
    {
        RebuildFilter();

        var vis = GetConfigValue<int>("Visibility");
        if (vis == 1 && _cond[ConditionFlag.InCombat]) return;
        if (vis == 2 && !_cond[ConditionFlag.InCombat]) return;

        var bounds = Node.Bounds.ContentRect;
        if (bounds.Width < 8 || bounds.Height < 8) return;

        var dl     = ImGui.GetWindowDrawList();
        var min    = new Vector2(bounds.X1, bounds.Y1);
        var max    = new Vector2(bounds.X2, bounds.Y2);
        var centre = (min + max) * 0.5f;
        var width  = max.X - min.X;
        var halfW  = width * 0.5f;

        dl.PushClipRect(min, max, true);
        dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new(0.12f, 0.12f, 0.14f, 0.55f)), 6f);
        dl.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(new(0.45f, 0.45f, 0.5f, 0.7f)), 6f);

        if (GetConfigValue<bool>("ShowCenterMarker"))
        {
            dl.AddLine(new(centre.X, min.Y + 2), new(centre.X, max.Y - 2),
                ImGui.ColorConvertFloat4ToU32(new(1f, 1f, 1f, 0.55f)), 1.2f);
        }

        var yaw      = _navi.TryGetCameraYawOrFallback();
        var forward  = Heading.ForwardFromYaw(yaw);
        var halfFov  = GetConfigValue<int>("FieldOfView") * 0.5f * Heading.Deg2Rad;
        var iconScale = GetConfigValue<float>("IconScale");
        var yText     = centre.Y;

        if (GetConfigValue<bool>("ShowCardinals"))
        {
            DrawLabel(dl, forward, Vector2.UnitY,  "N", halfFov, centre, halfW, yText, 0xFFFFFFFF);
            DrawLabel(dl, forward, Vector2.UnitX,  "E", halfFov, centre, halfW, yText, 0xFFCCCCCC);
            DrawLabel(dl, forward, -Vector2.UnitY, "S", halfFov, centre, halfW, yText, 0xFFCCCCCC);
            DrawLabel(dl, forward, -Vector2.UnitX, "W", halfFov, centre, halfW, yText, 0xFFCCCCCC);
        }

        if (GetConfigValue<bool>("ShowInterCardinals"))
        {
            const float n = 0.70710678f;
            DrawLabel(dl, forward, new(n, n),   "NE", halfFov, centre, halfW, yText, 0xFFAAAAAA);
            DrawLabel(dl, forward, new(n, -n),  "SE", halfFov, centre, halfW, yText, 0xFFAAAAAA);
            DrawLabel(dl, forward, new(-n, -n), "SW", halfFov, centre, halfW, yText, 0xFFAAAAAA);
            DrawLabel(dl, forward, new(-n, n),  "NW", halfFov, centre, halfW, yText, 0xFFAAAAAA);
        }

        if (GetConfigValue<bool>("ShowMapIcons"))
            DrawMapIcons(dl, forward, halfFov, centre, halfW, yText, iconScale, min, max);

        if (GetConfigValue<bool>("ShowDistance"))
            DrawDistance(dl, min, max);

        dl.PopClipRect();
    }

    private static void DrawLabel(
        ImDrawListPtr dl,
        Vector2 forward,
        Vector2 dir,
        string label,
        float halfFov,
        Vector2 centre,
        float halfW,
        float y,
        uint color
    )
    {
        var t = Heading.ProjectLinear(Heading.SignedAngle(dir, forward), halfFov);
        if (MathF.Abs(t) > 1f) return;
        var x = centre.X + t * halfW;
        var size = ImGui.CalcTextSize(label);
        dl.AddText(new(x - size.X * 0.5f, y - size.Y * 0.5f), color, label);
    }

    private unsafe void DrawMapIcons(
        ImDrawListPtr dl,
        Vector2 forward,
        float halfFov,
        Vector2 centre,
        float halfW,
        float y,
        float iconScale,
        Vector2 clipMin,
        Vector2 clipMax
    )
    {
        if (!_navi.TryReadMapIcons(GetConfigValue<bool>("UseAreaMap"), out var addon, out var root))
            return;
        if (root == null || root->Component == null) return;

        var list = root->Component->UldManager.NodeList;
        var count = root->Component->UldManager.NodeListCount;
        var player = NaviMapReader.NaviMapCenter;
        var minScale = GetConfigValue<float>("MinIconScale");

        // Skip chrome nodes; icon children typically start after a handful of static nodes.
        var start = GetConfigValue<bool>("UseAreaMap") ? 4 : 4;
        for (var i = start; i < count; i++)
        {
            var node = list[i];
            if (node == null || !node->IsVisible()) continue;

            var pos = new Vector2(node->X, node->Y);
            // Flip Y: minimap Y grows down.
            var mapDir = Heading.DirectionTo(player, new(pos.X, 2f * player.Y - pos.Y));
            var angle  = Heading.SignedAngle(mapDir, forward);
            var t      = Heading.ProjectLinear(angle, halfFov);
            if (MathF.Abs(t) > 1.05f) continue;

            var dist = Vector2.Distance(player, pos);
            var scale = MathF.Max(minScale, iconScale * MathF.Max(0.35f, 1f - dist / 180f));
            var size = MathF.Max(10f, MathF.Min(node->Width, node->Height) * scale);
            var x = centre.X + t * halfW;

            uint iconId = 0;
            if (node->Type == NodeType.Image)
            {
                var img = (AtkImageNode*)node;
                if (img->PartsList != null && img->PartId < img->PartsList->PartCount)
                {
                    var part = img->PartsList->Parts[img->PartId];
                    if (part.UldAsset != null)
                    {
                        var tex = part.UldAsset->AtkTexture;
                        if (tex.TextureType == TextureType.Resource && tex.Resource != null)
                            iconId = tex.Resource->IconId;
                    }
                }
            }

            if (iconId != 0 && _filtered.Contains(iconId)) continue;

            var half = size * 0.5f;
            var p0 = new Vector2(x - half, y - half);
            var p1 = new Vector2(x + half, y + half);
            if (p1.X < clipMin.X || p0.X > clipMax.X) continue;

            var tint = ImGui.ColorConvertFloat4ToU32(new(1f, 1f, 1f, 0.92f));
            dl.AddCircleFilled((p0 + p1) * 0.5f, half * 0.55f, tint);
            if (iconId != 0)
            {
                var tag = iconId.ToString();
                var ts  = ImGui.CalcTextSize(tag);
                if (ts.X < size * 2.2f)
                    dl.AddText(new(x - ts.X * 0.5f, y + half - 2), 0xAAFFFFFF, tag);
            }
        }

        _ = addon;
    }

    private unsafe void DrawDistance(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var ts = TargetSystem.Instance();
        if (ts == null) return;
        var target = ts->GetTargetObject();
        if (target == null) return;
        var local = _player.LocalPlayer;
        if (local == null) return;

        var d = Vector3.Distance(local.Position, target->Position);
        var text = $"{d:0} yalms";
        var size = ImGui.CalcTextSize(text);
        dl.AddText(new(max.X - size.X - 6, min.Y + 2), 0xFFFFFFFF, text);
    }
}
