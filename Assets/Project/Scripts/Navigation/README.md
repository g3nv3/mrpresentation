# Navigation

Каталог содержит два независимых сценария навигации:

- `NavMesh` - локальная навигация по Unity NavMesh, построенному из Pico spatial mesh.
- `Yandex` - навигация и проверка объектов через Yandex Maps API без построения маршрута по NavMesh.

## Структура

```text
Navigation/
├── README.md
├── NavMesh/
│   ├── NavMeshPathPresenter.cs
│   └── PicoSpatialMeshNavMeshBuilder.cs
├── Presentation/
│   ├── IRoutePointProjector.cs
│   ├── RouteEndpointMarkerView.cs
│   ├── RouteFloorProjector.cs
│   ├── RouteLineRendererView.cs
│   └── RoutePathView.cs
└── Yandex/
    ├── Api/
    │   ├── YandexGeocoderClient.cs
    │   └── YandexMapsRouteClient.cs
    ├── Presentation/
    │   └── YandexHelmetRoutePresenter.cs
    ├── Runtime/
    │   ├── SpatialMeshYandexBuildingProbe.cs
    │   └── YandexHelmetNavigator.cs
    └── Shared/
        ├── YandexGeocoderModels.cs
        └── YandexRouteModels.cs
```

`README.md`
: общий документ по назначению, настройке, публичному API и ограничениям навигационных классов.

`NavMesh/NavMeshPathPresenter.cs`
: строит локальный путь через `NavMesh.CalculatePath`, при необходимости привязывает начало и цель к полу через `IRoutePointProjector` и передает рассчитанные точки в `RoutePathView`.

`NavMesh/PicoSpatialMeshNavMeshBuilder.cs`
: слушает события `PXR_SpatialMeshManager` и перестраивает `NavMeshSurface`, чтобы Unity NavMesh соответствовал актуальному Pico spatial mesh.

`Presentation/RoutePathView.cs`
: общий visual facade маршрута. Управляет основной линией, endpoint-маркером и подложкой/тенью; если тень включена и `Floor Shadow Line` не назначен, создает дочерний `LineRenderer` автоматически.

`Presentation/RouteLineRendererView.cs`
: рисует переданные Unity-точки через `LineRenderer`. Может использовать `IRoutePointProjector`, поэтому подходит и для NavMesh, и для Yandex-маршрутов.

`Presentation/RouteFloorProjector.cs`
: raycast-проектор точки на пол. Используется NavMesh-логикой для начала/цели и visual-компонентами для линии, тени и маркера.

`Presentation/RouteEndpointMarkerView.cs`
: переносит и включает/выключает объект маркера конечной точки маршрута.

`Yandex/Api/YandexMapsRouteClient.cs`
: низкоуровневый клиент Yandex Route API. Собирает URL запроса маршрута, отправляет `UnityWebRequest`, парсит `legs[].steps[].polyline.points` и возвращает `YandexRouteResult`.

`Yandex/Api/YandexGeocoderClient.cs`
: низкоуровневый клиент Yandex Geocoder API. Делает reverse geocode по координате, запрашивает `kind=house` и возвращает адресные данные через `YandexReverseGeocodeResult`.

`Yandex/Presentation/YandexHelmetRoutePresenter.cs`
: преобразует географические точки маршрута Yandex в локальные Unity-точки около игрока или шлема по `Route Offset` и передает их в общий `RoutePathView`.

`Yandex/Runtime/YandexHelmetNavigator.cs`
: фасад для запроса маршрута и его отображения. Объединяет `YandexMapsRouteClient` и `YandexHelmetRoutePresenter`, хранит текущую координату и дает удобные методы `ShowRoute`, `ShowRouteTo`, `RequestAndShowRoute`.

`Yandex/Runtime/SpatialMeshYandexBuildingProbe.cs`
: сервис проверки объекта под raycast. Кидает луч в Pico spatial mesh, переводит `RaycastHit.point` в геокоординату через geo anchor и спрашивает Yandex Geocoder, здание ли это и какой у него полный адрес.

`Yandex/Shared/YandexRouteModels.cs`
: общие модели и интерфейсы для маршрутов: `GeoCoordinate`, `YandexRouteRequest`, `YandexRouteStep`, `YandexRouteData`, `YandexRouteResult`, `IYandexRouteClient`, `IYandexRoutePresenter`, `IYandexHelmetNavigator`, `GeoCoordinateUtility`.

`Yandex/Shared/YandexGeocoderModels.cs`
: общие модели и интерфейсы для reverse geocode и проверки зданий: `YandexReverseGeocodeResult`, `SpatialMeshYandexProbeResult`, `IYandexGeocoderClient`, `ISpatialMeshYandexBuildingProbe`.

## Настройка в сцене

В `SampleScene` на `Entry Point` уже добавлены:

- `YandexGeocoderClient`
- `SpatialMeshYandexBuildingProbe`

`GameScope` регистрирует их как:

- `IYandexGeocoderClient`
- `ISpatialMeshYandexBuildingProbe`

Для реальной работы нужно заполнить:

- `YandexGeocoderClient/Api Key` - ключ Yandex Maps API с доступом к Geocoder API.
- `SpatialMeshYandexBuildingProbe/Geo Anchor Coordinate` - реальную широту и долготу точки `Geo Anchor Transform`.

Сейчас `Geo Anchor Coordinate` оставлен невалидным (`999, 999`), чтобы случайно не отправлять запросы с ложной координатой.

`Spatial Mesh Mask` настроен на слой `MRMesh` (`m_Bits: 8`). Это слой, на котором находится `MeshPrefab` Pico spatial mesh.

Отображение маршрута собирается из общих presentation-компонентов:

- `RoutePathView` - назначает основную линию, тень и endpoint-маркер.
- `RouteLineRendererView` - ставится на объект с `LineRenderer`.
- `RouteFloorProjector` - назначается в `RouteLineRendererView/Point Projector`, `RouteEndpointMarkerView/Point Projector` и `NavMeshPathPresenter/Floor Projector`, если точки нужно класть на физический пол.

По умолчанию `RoutePathView/Auto Create Floor Shadow` создает дочерний объект тени сам. Для кастомной тени можно вручную назначить свой `RouteLineRendererView` в `RoutePathView/Floor Shadow Line`; тогда автогенерация не используется.

`NavMeshPathPresenter` и `YandexHelmetRoutePresenter` используют один и тот же `RoutePathView`, поэтому стили линии, подложка маршрута и endpoint-маркер переиспользуются без зависимости от источника маршрута.

## Публичный API

Проверка здания по raycast:

```csharp
probe.Probe(rayOrigin, rayDirection, result =>
{
    if (!result.HasSpatialMeshHit)
        return;

    if (result.IsBuilding)
    {
        Debug.Log(result.FullAddress);
        return;
    }

    if (result.IsPicoMeshOnly)
    {
        Debug.Log("Spatial mesh hit is not confirmed as a building by Yandex.");
    }
});
```

Запрос маршрута и отображение в шлеме:

```csharp
navigator.ShowRoute(
    new GeoCoordinate(startLatitude, startLongitude),
    new GeoCoordinate(finishLatitude, finishLongitude),
    result =>
    {
        if (!result.Succeeded)
            Debug.LogWarning(result.Error);
    });
```

Прямой запрос маршрута без отображения:

```csharp
routeClient.RequestRoute(startCoordinate, finishCoordinate, result =>
{
    if (result.Succeeded)
        Debug.Log(result.Route.TotalLengthMeters);
});
```

## DI

Компоненты можно прокидывать через VContainer:

- `IYandexRouteClient`
- `IYandexRoutePresenter`
- `IYandexHelmetNavigator`
- `IYandexGeocoderClient`
- `ISpatialMeshYandexBuildingProbe`

Для этого назначь соответствующие компоненты в `GameScope`.

## Гео-якорь

`SpatialMeshYandexBuildingProbe` не может сам узнать широту и долготу Unity-позиции. Ему нужен якорь:

- `Geo Anchor Transform` - Transform в Unity-сцене.
- `Geo Anchor Coordinate` - реальная координата этого Transform.
- `Meters To Unity Scale` - сколько Unity units соответствует одному реальному метру. Для Pico spatial mesh обычно `1`.

После этого hit point переводится в east/north offset относительно якоря и затем в `GeoCoordinate`.

## Ограничения

- Yandex-проверка здания зависит от точности гео-якоря.
- Если `Geo Anchor Coordinate` невалиден, `Probe` вернет ошибку и не отправит запрос.
- Если raycast попал в spatial mesh, но Yandex не вернул `kind=house`, результат считается `IsPicoMeshOnly`.
- NavMesh-навигация и Yandex-навигация не зависят друг от друга.
