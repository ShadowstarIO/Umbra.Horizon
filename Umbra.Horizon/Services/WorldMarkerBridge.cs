using System;
using System.Collections.Generic;
using System.Reflection;
using Umbra.Common;
using Umbra.Markers.System;

namespace Umbra.Horizon.Services;

/// <summary>
/// Reads Umbra world markers. Registry is internal, so this uses the public
/// WorldMarker type and resolves the registry through the service container.
/// </summary>
[Service]
internal sealed class WorldMarkerBridge
{
    private readonly object?  _registry;
    private readonly MethodInfo? _getMarkers;

    public WorldMarkerBridge()
    {
        try
        {
            var type = typeof(WorldMarker).Assembly.GetType("Umbra.Markers.System.WorldMarkerRegistry");
            if (type == null) return;
            _registry   = Framework.Service<object>(type);
            _getMarkers = type.GetMethod("GetMarkers", BindingFlags.Instance | BindingFlags.Public);
        }
        catch
        {
            _registry   = null;
            _getMarkers = null;
        }
    }

    public IReadOnlyList<WorldMarker> GetCompassMarkers()
    {
        if (_registry == null || _getMarkers == null) return [];

        try
        {
            if (_getMarkers.Invoke(_registry, null) is not IEnumerable<WorldMarker> list) return [];
            var result = new List<WorldMarker>();
            foreach (var marker in list)
            {
                if (marker.ShowOnCompass && marker.IsVisible)
                    result.Add(marker);
            }

            return result;
        }
        catch
        {
            return [];
        }
    }
}
