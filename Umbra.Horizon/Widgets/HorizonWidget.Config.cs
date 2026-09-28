using System.Collections.Generic;
using Umbra.Widgets;

namespace Umbra.Horizon.Widgets;

public sealed partial class HorizonWidget
{
    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return
        [
            ..base.GetConfigVariables(),

            new BooleanWidgetConfigVariable(
                "UseCameraAngle",
                "Use camera heading",
                "If off, uses character facing instead of camera.",
                true
            ),
            new IntegerWidgetConfigVariable(
                "Width",
                "Width",
                "Strip width in pixels.",
                480,
                64,
                1400
            ),
            new IntegerWidgetConfigVariable(
                "StripHeight",
                "Height",
                "Strip height in pixels.",
                28,
                16,
                72
            ),
            new IntegerWidgetConfigVariable(
                "AngleRangeDegrees",
                "Visible angle (deg)",
                "Heading span shown on the strip. Higher is wider / flatter.",
                225,
                45,
                360
            ),
            new FloatWidgetConfigVariable(
                "FishEyePower",
                "Fish-eye",
                "1 = linear (flat). Below 1 packs the center. Above 1 packs the edges.",
                0.75f,
                0.5f,
                1.8f
            ),
            new FloatWidgetConfigVariable(
                "IconScale",
                "Icon scale",
                "Base scale for map and marker icons.",
                0.65f,
                0.25f,
                1.5f
            ),
            new FloatWidgetConfigVariable(
                "MinIconScale",
                "Minimum icon scale",
                "Floor scale for distant icons.",
                0.35f,
                0.1f,
                1.0f
            ),
            new IntegerWidgetConfigVariable(
                "MaxMarkerDistance",
                "Max marker distance",
                "World markers farther than this (yalms) are hidden. 0 = no cap.",
                0,
                0,
                2000
            ),
            new BooleanWidgetConfigVariable("ShowCardinals", "Show cardinals", null, true),
            new BooleanWidgetConfigVariable("ShowInterCardinals", "Show intercardinals", null, false),
            new BooleanWidgetConfigVariable("ShowTicks", "Show degree ticks", null, true),
            new BooleanWidgetConfigVariable("ShowCenterMarker", "Show center marker", null, true),
            new BooleanWidgetConfigVariable("ShowWeather", "Show current weather", null, true),
            new BooleanWidgetConfigVariable("ShowDistance", "Show distance to target", null, true),
            new BooleanWidgetConfigVariable("ShowMinimapIcons", "Show minimap icons", null, true),
            new BooleanWidgetConfigVariable("ShowWorldMarkers", "Show Umbra world markers", "Markers with Show on Compass from Umbra marker modules.", true),
            new BooleanWidgetConfigVariable("UseAreaMap", "Use area map as icon source", null, false),
            new IntegerWidgetConfigVariable(
                "Visibility",
                "Visibility",
                "0 = always, 1 = out of combat, 2 = in combat.",
                0,
                0,
                2
            ),
            new BooleanWidgetConfigVariable(
                "HideOnGathering",
                "Hide while gathering",
                "Hides the strip when the gathering notebook UI is open.",
                false
            ),
            new ColorWidgetConfigVariable("CenterLineColor", "Center line color", null, 0xFF0000FF),
            new ColorWidgetConfigVariable("LineColor", "Tick color", null, 0xFFACFFAA),
            new ColorWidgetConfigVariable("TextColor", "Text color", null, 0xFFFFFFFF),
            new StringWidgetConfigVariable(
                "FilteredIconIds",
                "Hidden icon IDs",
                "Comma-separated icon IDs to skip.",
                string.Empty,
                1024
            ),
        ];
    }
}
