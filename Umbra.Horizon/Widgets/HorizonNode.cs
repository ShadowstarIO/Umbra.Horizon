using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
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

    private readonly IPlayer          _player  = Framework.Service<IPlayer>();
    private readonly IGameCamera      _camera  = Framework.Service<IGameCamera>();
    private readonly IZoneManager     _zones   = Framework.Service<IZoneManager>();
    private readonly IGameGui         _gui     = Framework.Service<IGameGui>();
    private readonly ITextureProvider _tex     = Framework.Service<ITextureProvider>();
    private readonly NaviMapReader    _navi    = Framework.Service<NaviMapReader>();
    private readonly WorldMarkerBridge _markers = Framework.Service<WorldMarkerBridge>();

    private HashSet<uint> _filtered = [];
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
        if (HideOnGathering && AddonVisible("GatheringMasterpiece") | AddonVisible("Gathering")) return;
        if (AddonVisible("_CharaMakeBgSelector")) return;

        var rect = Bounds.ContentRect;
        if (rect.Width <= 1 || rect.Height <= 1) return;

        float x = rect.TopLeft.X;
        float y = rect.TopLeft.Y;
        float w = rect.Width;
        float h = rect.Height;
        float cx = x + w * 0.5f;
        float cy = y + h * 0.5f;

        float heading = GetHeadingDegrees();

        drawList.PushClipRect(rect.TopLeft, rect.BottomRight, true);
        drawList.AddRectFilled(rect.TopLeft, rect.BottomRight, 0x8C1C1C24, 6f);
        drawList.AddRect(rect.TopLeft, rect.BottomRight, 0xB273737F, 6f);

        if (ShowCenterMarker)
        {
            drawList.AddLine(new(cx, y + 2), new(cx, y + h - 2), CenterLineColor, 1.4f);
        }

        if (ShowTicks)
        {
            for (var i = 0; i < 360; i += 15)
            {
                var screenX = Project(i, heading, cx, w);
                if (screenX < x - 8 || screenX > x + w + 8) continue;
                var alpha = EdgeFade(screenX, x, w);
                if (alpha <= 0.01f) continue;

                var isCardinal = Cardinals.ContainsKey(i);
                var tickH = isCardinal ? h * 0.75f : (i % 30 == 0 ? h * 0.42f : h * 0.28f);
                drawList.AddLine(
                    new(screenX, cy - tickH * 0.5f),
                    new(screenX, cy + tickH * 0.5f),
                    ApplyAlpha(LineColor, alpha),
                    isCardinal ? 1.4f : 1f
                );
            }
        }

        if (ShowCardinals)
        {
            foreach (var (deg, label) in Cardinals)
                DrawHeadingLabel(drawList, deg, heading, label, cx, cy, x, y, w, h, TextColor);
        }

        if (ShowInterCardinals)
        {
            foreach (var (deg, label) in InterCardinals)
                DrawHeadingLabel(drawList, deg, heading, label, cx, cy, x, y, w, h, ApplyAlpha(TextColor, 0.75f));
        }

        if (ShowMinimapIcons)
            DrawMinimapIcons(drawList, heading, cx, cy, x, w, h);

        if (ShowWorldMarkers)
            DrawWorldMarkers(drawList, heading, cx, cy, x, w, h);

        if (ShowWeather)
            DrawWeather(drawList, x, y, h);

        if (ShowDistance)
            DrawDistance(drawList, x, y, w);

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
        while (delta > 180f) delta  -= 360f;
        while (delta <= -180f) delta += 360f;

        var view = MathF.Max(1f, AngleRangeDegrees);
        var n    = delta / view;
        var pow  = MathF.Max(0.15f, FishEyePower);
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
        float cx, float cy, float x, float y, float w, float h, uint color
    )
    {
        var screenX = Project(deg, heading, cx, w);
        var alpha   = EdgeFade(screenX, x, w);
        if (alpha <= 0.01f) return;
        var size = ImGui.CalcTextSize(label);
        dl.AddText(new(screenX - size.X * 0.5f, y + 3), ApplyAlpha(color, alpha), label);
        _ = (cy, h);
    }

    private unsafe void DrawMinimapIcons(ImDrawListPtr dl, float heading, float cx, float cy, float x, float w, float h)
    {
        if (!_navi.TryReadMapIcons(UseAreaMap, out _, out var root)) return;
        if (root == null || root->Component == null) return;

        var list  = root->Component->UldManager.NodeList;
        var count = root->Component->UldManager.NodeListCount;
        var origin = NaviMapReader.NaviMapCenter;

        for (var i = 4; i < count; i++)
        {
            var node = list[i];
            if (node == null || !node->IsVisible()) continue;

            var pos = new Vector2(node->X, -node->Y);
            var dir = pos - new Vector2(origin.X, -origin.Y);
            if (dir.LengthSquared() < 0.01f) continue;

            var angle = MathF.Atan2(dir.X, dir.Y) * (180f / MathF.PI);
            if (angle < 0) angle += 360f;

            var screenX = Project(angle, heading, cx, w);
            if (screenX < x - 16 || screenX > x + w + 16) continue;

            uint iconId = ReadIconId(node);
            if (iconId != 0 && _filtered.Contains(iconId)) continue;

            var dist  = dir.Length();
            var scale = MathF.Max(MinIconScale, IconScale * MathF.Max(0.35f, 1f - dist / 180f));
            var size  = MathF.Max(10f, 24f * scale);

            if (iconId != 0 && TryGetIcon(iconId, out var wrap) && wrap != null)
            {
                var half = size * 0.5f;
                dl.AddImage(wrap.Handle, new(screenX - half, cy - half), new(screenX + half, cy + half));
            }
            else
            {
                dl.AddCircleFilled(new(screenX, cy), size * 0.28f, 0xE0FFFFFF);
            }
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

            var scale = MathF.Max(MinIconScale, IconScale * MathF.Max(0.35f, 1f - dist / 220f));
            var iw = MathF.Max(8f, (marker.IconWidth > 0 ? marker.IconWidth : 32) * scale);
            var ih = MathF.Max(8f, (marker.IconHeight > 0 ? marker.IconHeight : 32) * scale);

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

        uint iconId = 0;
        try { iconId = (uint)(weather.GetType().GetProperty("IconId")?.GetValue(weather) ?? 0); }
        catch { }

        if (iconId == 0 || !TryGetIcon(iconId, out var wrap) || wrap == null) return;
        var s = MathF.Min(h - 4, 22);
        dl.AddImage(wrap.Handle, new(x + 4, y + (h - s) * 0.5f), new(x + 4 + s, y + (h + s) * 0.5f));
    }

    private unsafe void DrawDistance(ImDrawListPtr dl, float x, float y, float w)
    {
        var ts = TargetSystem.Instance();
        if (ts == null) return;
        var target = ts->GetTargetObject();
        if (target == null) return;

        var d    = Vector3.Distance(_player.Position, target->Position);
        var text = $"{d:0} yalms";
        var size = ImGui.CalcTextSize(text);
        dl.AddText(new(x + w - size.X - 6, y + 2), TextColor, text);
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
