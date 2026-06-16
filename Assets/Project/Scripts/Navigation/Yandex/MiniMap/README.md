# Yandex Following Route Mini Map

Мини-карта для VR UI состоит из тайловой подложки Yandex Tiles API и линии текущего маршрута поверх нее.
Игрок остается в центре, а тайлы, маршрут и маркер цели сдвигаются относительно текущей GPS-позиции.

## Scene setup

```text
MiniMapViewport                RectTransform + RectMask2D/Image
├── MapContent                 RectTransform
│   ├── Tiles                  RectTransform + YandexMiniMapTileLayer
│   ├── RouteLine              RectTransform + YandexMiniMapRouteLine
│   └── DestinationMarker      RectTransform
└── PlayerMarker               RectTransform
```

Add `YandexFollowingRouteMiniMap` to `MiniMapViewport` or a nearby controller object and assign:

- `Map View Root` or `Map Canvas Group` - object/group that should be shown and hidden.
- `Location Receiver` - existing `PhoneLocationUdpReceiver`.
- `Route Bridge` - existing `PhoneRouteDestinationYandexNavigatorBridge`.
- `Tile Layer` - `YandexMiniMapTileLayer` on `Tiles`.
- `Route Line` - `YandexMiniMapRouteLine` on `RouteLine`.
- `Map Content Root` - `MapContent`.
- `Player Marker` - fixed marker in the center.
- `Destination Marker` - marker under `MapContent`.

Set the Yandex Tiles API key in `YandexMiniMapTileLayer/Api Key`.

## Public API

- `ShowMap()` - shows the mini-map and refreshes tiles/route for the latest known GPS position.
- `HideMap()` - hides the mini-map without clearing the route or current GPS position.
- `ToggleMap()` - switches visibility; bind this to a `UiPressButton.OnPressed` event.
- `SetMapVisible(bool visible)` - direct visibility setter for code.
- `ClearRoute()` - removes route and destination marker while keeping the map centered on the player.
- `SetRoute(YandexRouteData route)` - manually sets the visible route.

When the map is hidden it keeps receiving GPS and route events, but does not rebuild visible tiles until shown again.
If it is shown before a route exists, it displays only the map centered on the latest GPS/current navigator coordinate.
If `PhoneRouteDestinationYandexNavigatorBridge.DisableRoute()` or direct `YandexHelmetNavigator.DisableRoute()` is called, the mini-map clears its route automatically.

## Notes

- `Zoom` and `Ui Scale` must match on the controller; it passes them to both tile and route layers.
- Enable `Rotate Map With Course` if the phone location packets provide a usable `CourseDegrees`.
- The tile URL template requests `projection=web_mercator`, matching the module projection math.
- Yandex Tiles API requires the Yandex logo to be shown on top of the map. Add the official logo image to the mini-map UI according to the Yandex requirements.
