using System.Collections.Generic;
using Umbra.Widgets;
using Una.Drawing;

namespace Umbra.Horizon.Widgets;

[ToolbarWidget(
    "Horizon",
    "Horizon",
    "Wide flattened heading strip with cardinals, minimap icons, and Umbra world markers.",
    ["compass", "horizon", "direction", "minimap"]
)]
public sealed partial class HorizonWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    protected override StandardWidgetFeatures Features => StandardWidgetFeatures.None;

    public override WidgetPopup? Popup => null;

    protected override string DefaultSizingMode => SizingModeFixed;
    protected override int    DefaultWidth      => 480;

    private HorizonNode Strip { get; } = new() {
        ClassList = ["HorizonWidget"],
        Style = new() {
            Size   = new(480, 28),
            Anchor = Anchor.MiddleLeft,
        }
    };

    protected override void OnLoad()
    {
        BodyNode.AppendChild(Strip);
        BodyNode.BeforeDraw += _ => {
            var w = GetConfigValue<int>("Width");
            var h = GetConfigValue<int>("StripHeight");
            BodyNode.Style.Size = new(w, h);
            Strip.Style.Size    = new(System.Math.Max(32, w - 6), h);
        };
    }

    protected override void OnDraw()
    {
        Strip.UseCameraAngle     = GetConfigValue<bool>("UseCameraAngle");
        Strip.AngleRangeDegrees  = GetConfigValue<int>("AngleRangeDegrees");
        Strip.FishEyePower       = GetConfigValue<float>("FishEyePower");
        Strip.IconScale          = GetConfigValue<float>("IconScale");
        Strip.MinIconScale       = GetConfigValue<float>("MinIconScale");
        Strip.MaxMarkerDistance  = GetConfigValue<int>("MaxMarkerDistance");
        Strip.ShowCardinals      = GetConfigValue<bool>("ShowCardinals");
        Strip.ShowInterCardinals = GetConfigValue<bool>("ShowInterCardinals");
        Strip.ShowTicks          = GetConfigValue<bool>("ShowTicks");
        Strip.ShowCenterMarker   = GetConfigValue<bool>("ShowCenterMarker");
        Strip.ShowWeather        = GetConfigValue<bool>("ShowWeather");
        Strip.ShowDistance       = GetConfigValue<bool>("ShowDistance");
        Strip.ShowMinimapIcons   = GetConfigValue<bool>("ShowMinimapIcons");
        Strip.ShowWorldMarkers   = GetConfigValue<bool>("ShowWorldMarkers");
        Strip.UseAreaMap         = GetConfigValue<bool>("UseAreaMap");
        Strip.VisibilityMode     = GetConfigValue<int>("Visibility");
        Strip.HideOnGathering    = GetConfigValue<bool>("HideOnGathering");
        Strip.CenterLineColor    = GetConfigValue<uint>("CenterLineColor");
        Strip.LineColor          = GetConfigValue<uint>("LineColor");
        Strip.TextColor          = GetConfigValue<uint>("TextColor");
        Strip.SetFilteredIds(GetConfigValue<string>("FilteredIconIds"));
    }

    protected override void OnUnload()
    {
    }
}
