using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Umbra.Common;
using Umbra.Game;
using Umbra.Horizon.Services;
using Umbra.Markers.System;
using Una.Drawing;

namespace Umbra.Horizon.Widgets;

internal sealed class HorizonNode : Node
{
    public bool  UseCameraAngle     { get; set; } = true;
    public float AngleRangeDegrees  { get; set; } = 170f;
    public float FishEyePower       { get; set; } = 1.0f;
    public float IconScale          { get; set; } = 0.65f;
    public float MinIconScale       { get; set; } = 0.35f;
    public int   MaxMarkerDistance  { get; set; }
    public bool  ShowCardinals      { get; set; } = true;
    public bool  ShowInterCardinals { get; set; }
    public bool  ShowTicks          { get; set; } = true;
    public bool  ShowCenterMarker   { get; set; } = true;
    public bool  ShowWeather        { get; set; } = true;
    public bool  ShowDistance       { get; set; } = true;
    public bool  ShowMinimapIcons   { get; set; } = true;
    public bool  ShowWorldMarkers   { get; set; } = true;
    public bool  UseAreaMap         { get; set; }
    public int   VisibilityMode     { get; set; }
    public bool  HideOnGathering    { get; set; }
    public uint  CenterLineColor    { get; set; } = 0xFF0000FF;
    public uint  LineColor          { get; set; } = 0xFFACFFAA;
    public uint  TextColor          { get; set; } = 0xFFFFFFFF;

    private readonly IPlayer           _player  = Framework.Service<IPlayer>();
    private readonly IGameCamera       _camera  = Framework.Service<IGameCamera>();
    private readonly IZoneManager      _zones   = Framework.Service<IZoneManager>();
    private readonly IGameGui          _gui     = Framework.Service<IGameGui>();
    private readonly ITextureProvider  _tex     = Framework.Service<ITextureProvider>();
    private readonly NaviMapReader     _navi    = Framework.Service<NaviMapReader>();
    private readonly WorldMarkerBridge _markers = Framework.Service<WorldMarkerBridge>();

    private HashSet<uint> _filtered  = [];
    private string        _filterRaw = string.Empty;

    private static readonly Dictionary<int, string> Cardinals = new() {
        { 0, "N" }, { 90, "W" }, { 180, "S" }, { 270, "E" }
    };

    private static readonly Dictionary<int, string> InterCardinals = new() {
        { 45, "NW" }, { 135, "SW" }, { 225, "SE" }, { 315, "NE" }
    };

    public void SetFilteredIds(string raw)
    {
        if (raw == _filterRaw) return;
        _filterRaw = raw ?? string.Empty;
        _filtered  = [];
        foreach (var part in _filterRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (uint.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                _filtered.Add(id);
        }
    }

    protected override void OnDraw(ImDrawListPtr drawList)
    {
        if (VisibilityMode == 1 && _player.IsInCombat) return;
        if (VisibilityMode == 2 && !_player.IsInCombat) return;
        if (HideOnGathering && (AddonVisible("GatheringMasterpiece") || AddonVisible("Gathering"))) return;

        var rect = Bounds.ContentRect;
        if (rect.Width <= 1 || rect.Height <= 1) return;

        float x  = rect.TopLeft.X;
        float y  = rect.TopLeft.Y;
        float w  = rect.Width;
        float h  = rect.Height;
        float cx = x + w * 0.5f;
        float cy = y + h * 0.5f;

        float heading = GetHeadingDegrees();

        drawList.PushClipRect(rect.TopLeft, rect.BottomRight, true);

        if (ShowCenterMarker)
        {
            var a = EdgeFade(cx, x, w, 18f);
            drawList.AddLine(new(cx, y + h * 0.55f), new(cx, y + h - 2), ApplyAlpha(CenterLineColor, a), 1.4f);
        }

        if (ShowTicks)
        {
            for (var i = 0; i < 360; i += 15)
            {
                var screenX = Project(i, heading, cx, w);
                if (screenX < x - 8 || screenX > x + w + 8) continue;
                var alpha = EdgeFade(screenX, x, w);
                if (alpha <= 0.01f) continue;

                var labeled = Cardinals.ContainsKey(i) || InterCardinals.ContainsKey(i);
                float tickTop;
                float tickBot;
                float thick;
                if (labeled)
                {
                    tickTop = y + h - 6f;
                    tickBot = y + h - 2f;
                    thick   = 1.2f;
                    alpha  *= 0.35f;
                }
                else if (i % 30 == 0)
                {
                    tickTop = cy - h * 0.18f;
                    tickBot = y + h - 2f;
                    thick   = 1.0f;
                }
                else
                {
                    tickTop = y + h - 8f;
                    tickBot = y + h - 2f;
                    thick   = 1.0f;
                }

                drawList.AddLine(new(screenX, tickTop), new(screenX, tickBot), ApplyAlpha(LineColor, alpha), thick);
            }
        }

        if (ShowCardinals)
        {
            foreach (var (deg, label) in Cardinals)
                DrawHeadingLabel(drawList, deg, heading, label, cx, x, y, w, h, TextColor);
        }

        if (ShowInterCardinals)
        {
            foreach (var (deg, label) in InterCardinals)
                DrawHeadingLabel(drawList, deg, heading, label, cx, x, y, w, h, ApplyAlpha(TextColor, 0.85f));
        }

        if (ShowMinimapIcons)
            DrawMinimapIcons(drawList, heading, cx, cy, x, w, h);

        if (ShowWorldMarkers)
            DrawWorldMarkers(drawList, heading, cx, cy, x, w, h);

        if (ShowWeather)
            DrawWeather(drawList, x, y, h);

        if (ShowDistance)
            DrawDistance(drawList, x, y, w, h);

        drawList.PopClipRect();
    }

    private float GetHeadingDegrees()
    {
        const float rad2Deg = 180f / MathF.PI;
        if (UseCameraAngle)
        {
            var rad = _camera.GetCameraAngle();
            return (-(rad * rad2Deg + 90f - 360f) % 360f + 360f) % 360f;
        }

        return (_player.Rotation * rad2Deg + 180f + 360f) % 360f;
    }

    private float Project(float targetDeg, float headingDeg, float centerX, float width)
    {
        var delta = targetDeg - headingDeg;
        while (delta > 180f) delta   -= 360f;
        while (delta <= -180f) delta += 360f;

        var view      = MathF.Max(1f, AngleRangeDegrees);
        var n         = delta / view;
        var pow       = MathF.Max(0.15f, FishEyePower);
        var distorted = MathF.Sign(n) * MathF.Pow(MathF.Abs(n), pow);
        var halfPow   = MathF.Max(0.0001f, MathF.Pow(0.5f, pow));
        var scale     = (width / 2f) / halfPow;
        return centerX - distorted * scale;
    }

    private static float EdgeFade(float screenX, float widgetX, float width, float threshold = 28f)
    {
        var nearest = MathF.Min(screenX - widgetX, widgetX + width - screenX);
        if (nearest >= threshold) return 1f;
        if (nearest < 0) return 0f;
        return nearest / threshold;
    }

    private static uint ApplyAlpha(uint color, float mul)
    {
        mul = Math.Clamp(mul, 0f, 1f);
        if (mul >= 0.999f) return color;
        if (mul <= 0.001f) return color & 0x00FFFFFF;
        return (color & 0x00FFFFFF) | ((uint)(((color >> 24) & 0xFF) * mul) << 24);
    }

    private void DrawHeadingLabel(
        ImDrawListPtr dl, float deg, float heading, string label,
        float cx, float x, float y, float w, float h, uint color
    )
    {
        var screenX = Project(deg, heading, cx, w);
        var alpha   = EdgeFade(screenX, x, w);
        if (alpha <= 0.01f) return;
        var size = ImGui.CalcTextSize(label);
        var tx   = screenX - size.X * 0.5f;
        var ty   = y + MathF.Max(1f, (h - size.Y) * 0.22f);
        dl.AddText(new(tx + 1, ty + 1), ApplyAlpha(0xCC000000, alpha), label);
        dl.AddText(new(tx, ty), ApplyAlpha(color, alpha), label);
    }

    private unsafe void DrawMinimapIcons(ImDrawListPtr dl, float heading, float cx, float cy, float x, float w, float h)
    {
        if (!_navi.TryReadMapIcons(UseAreaMap, out _, out var root)) return;
        if (root == null) return;
        WalkIcons((AtkResNode*)root, heading, cx, cy, x, w, h, dl);
    }

    private unsafe void WalkIcons(AtkResNode* node, float heading, float cx, float cy, float x, float w, float h, ImDrawListPtr dl)
    {
        if (node == null) return;

        if (node->IsVisible())
        {
            var iconId = ReadIconId(node);
            if (iconId != 0 || node->Type == NodeType.Image)
            {
                var pos = new Vector2(node->X, -node->Y);
                var dir = pos - new Vector2(NaviMapReader.NaviMapCenter.X, -NaviMapReader.NaviMapCenter.Y);
                if (dir.LengthSquared() > 1f)
                    DrawProjectedIcon(dl, heading, dir, iconId, cx, cy, x, w, h);
            }
        }

        if (node->Type is NodeType.Component or (NodeType)1001)
        {
            var comp = (AtkComponentNode*)node;
            if (comp->Component != null)
            {
                var list  = comp->Component->UldManager.NodeList;
                var count = comp->Component->UldManager.NodeListCount;
                for (var i = 0; i < count; i++)
                    WalkIcons(list[i], heading, cx, cy, x, w, h, dl);
            }
        }

        if (node->ChildNode != null)
            WalkIcons(node->ChildNode, heading, cx, cy, x, w, h, dl);
        if (node->PrevSiblingNode != null)
            WalkIcons(node->PrevSiblingNode, heading, cx, cy, x, w, h, dl);
    }

    private void DrawProjectedIcon(
        ImDrawListPtr dl, float heading, Vector2 dir, uint iconId,
        float cx, float cy, float x, float w, float h
    )
    {
        if (iconId != 0 && _filtered.Contains(iconId)) return;

        var angle = MathF.Atan2(dir.X, dir.Y) * (180f / MathF.PI);
        if (angle < 0) angle += 360f;

        var screenX = Project(angle, heading, cx, w);
        if (screenX < x - 16 || screenX > x + w + 16) return;

        var dist  = dir.Length();
        var scale = MathF.Max(MinIconScale, IconScale * MathF.Max(0.4f, 1f - dist / 180f));
        var size  = MathF.Max(12f, 22f * scale);

        if (iconId != 0 && TryGetIcon(iconId, out var wrap) && wrap != null)
        {
            var half = size * 0.5f;
            dl.AddImage(wrap.Handle, new(screenX - half, cy - half), new(screenX + half, cy + half));
        }
        else
        {
            dl.AddCircleFilled(new(screenX, cy), size * 0.28f, 0xE0FFFFFF);
        }

        _ = h;
    }

    private void DrawWorldMarkers(ImDrawListPtr dl, float heading, float cx, float cy, float x, float w, float h)
    {
        if (!_zones.HasCurrentZone) return;
        var player = _player.Position;

        foreach (var marker in _markers.GetCompassMarkers())
        {
            if (!marker.IsVisible || !marker.ShowOnCompass) continue;
            if (marker.IconId != 0 && _filtered.Contains(marker.IconId)) continue;

            var wp   = marker.Position;
            var flat = new Vector2(wp.X - player.X, wp.Z - player.Z);
            var dist = flat.Length();
            if (MaxMarkerDistance > 0 && dist > MaxMarkerDistance) continue;
            if (marker.MaxVisibleDistance > 0 && dist > marker.MaxVisibleDistance) continue;
            if (dist < 0.05f) continue;

            var angle = MathF.Atan2(flat.X, flat.Y) * (180f / MathF.PI);
            if (angle < 0) angle += 360f;

            var screenX = Project(angle, heading, cx, w);
            if (screenX < x - 16 || screenX > x + w + 16) continue;

            var scale = MathF.Max(MinIconScale, IconScale * MathF.Max(0.4f, 1f - dist / 220f));
            var iw    = MathF.Max(10f, (marker.IconWidth > 0 ? marker.IconWidth : 32) * scale);
            var ih    = MathF.Max(10f, (marker.IconHeight > 0 ? marker.IconHeight : 32) * scale);

            if (marker.IconId != 0 && TryGetIcon(marker.IconId, out var wrap) && wrap != null)
            {
                dl.AddImage(wrap.Handle, new(screenX - iw * 0.5f, cy - ih * 0.5f), new(screenX + iw * 0.5f, cy + ih * 0.5f));
            }
            else
            {
                dl.AddCircleFilled(new(screenX, cy), iw * 0.28f, 0xE0FFD080);
            }
        }

        _ = h;
    }

    private void DrawWeather(ImDrawListPtr dl, float x, float y, float h)
    {
        if (!_zones.HasCurrentZone) return;
        var weather = _zones.CurrentZone.CurrentWeather;
        if (weather == null) return;

        var iconId = ReadUintMember(weather, "IconId", "Icon", "WeatherIconId", "Id");
        if (iconId == 0 || !TryGetIcon(iconId, out var wrap) || wrap == null) return;

        var s = MathF.Min(h - 2, 20);
        dl.AddImage(wrap.Handle, new(x + 2, y + (h - s) * 0.5f), new(x + 2 + s, y + (h + s) * 0.5f));
    }

    private unsafe void DrawDistance(ImDrawListPtr dl, float x, float y, float w, float h)
    {
        var ts = TargetSystem.Instance();
        if (ts == null) return;
        var target = ts->GetTargetObject();
        if (target == null) return;

        var d    = Vector3.Distance(_player.Position, target->Position);
        var text = $"{d:0}";
        var size = ImGui.CalcTextSize(text);
        var tx   = x + w - size.X - 4;
        var ty   = y + MathF.Max(1f, (h - size.Y) * 0.5f);
        dl.AddText(new(tx + 1, ty + 1), 0xCC000000, text);
        dl.AddText(new(tx, ty), TextColor, text);
    }

    private static uint ReadUintMember(object obj, params string[] names)
    {
        var t = obj.GetType();
        foreach (var name in names)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null)
            {
                try
                {
                    var v = p.GetValue(obj);
                    if (v is uint u) return u;
                    if (v is int i && i > 0) return (uint)i;
                    if (v is ushort us) return us;
                    if (v is short s && s > 0) return (uint)s;
                }
                catch { }
            }

            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
            {
                try
                {
                    var v = f.GetValue(obj);
                    if (v is uint u) return u;
                    if (v is int i && i > 0) return (uint)i;
                }
                catch { }
            }
        }

        return 0;
    }

    private bool TryGetIcon(uint iconId, out IDalamudTextureWrap? wrap)
    {
        wrap = null;
        try
        {
            if (_tex.TryGetFromGameIcon(new GameIconLookup(iconId), out var did) && did.TryGetWrap(out wrap, out _))
                return wrap != null;
        }
        catch
        {
            return false;
        }

        return wrap != null;
    }

    private unsafe uint ReadIconId(AtkResNode* node)
    {
        if (node->Type != NodeType.Image) return 0;
        var img = (AtkImageNode*)node;
        if (img->PartsList == null || img->PartId >= img->PartsList->PartCount) return 0;
        var part = img->PartsList->Parts[img->PartId];
        if (part.UldAsset == null) return 0;
        var tex = part.UldAsset->AtkTexture;
        if (tex.TextureType != TextureType.Resource || tex.Resource == null) return 0;
        return tex.Resource->IconId;
    }

    private bool AddonVisible(string name)
    {
        var addon = _gui.GetAddonByName(name);
        return addon.Address != nint.Zero && addon.IsVisible;
    }
}
