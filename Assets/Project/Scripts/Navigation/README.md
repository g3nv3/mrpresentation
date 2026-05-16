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
: строит локальный путь через `NavMesh.CalculatePath`, проецирует точки на пол при необходимости и рисует маршрут через `LineRenderer`.

`NavMesh/PicoSpatialMeshNavMeshBuilder.cs`
: слушает события `PXR_SpatialMeshManager` и перестраивает `NavMeshSurface`, чтобы Unity NavMesh соответствовал актуальному Pico spatial mesh.

`Yandex/Api/YandexMapsRouteClient.cs`
: низкоуровневый клиент Yandex Route API. Собирает URL запроса маршрута, отправляет `UnityWebRequest`, парсит `legs[].steps[].polyline.points` и возвращает `YandexRouteResult`.

`Yandex/Api/YandexGeocoderClient.cs`
: низкоуровневый клиент Yandex Geocoder API. Делает reverse geocode по координате, запрашивает `kind=house` и возвращает адресные данные через `YandexReverseGeocodeResult`.

`Yandex/Presentation/YandexHelmetRoutePresenter.cs`
: преобразует географические точки маршрута Yandex в локальные Unity-точки и рисует их около игрока или шлема по `Route Offset`.

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
