using UnityEngine;
using VContainer;
using VContainer.Unity;
using Project.Scripts.Interaction;

namespace Project.Scripts
{
    public class GameScope : LifetimeScope
    {
        [SerializeField] private PicoHandInput picoHandInput;
        [SerializeField] private PicoCameraRenderTextureSource picoCameraRenderTextureSource;
        [SerializeField] private QrMarkerRegistry qrMarkerRegistry;
        [SerializeField] private QrCameraRayPoseResolver qrCameraRayPoseResolver;
        [Tooltip("Необязательный клиент Yandex Route API, регистрируется как IYandexRouteClient.")]
        [SerializeField] private YandexMapsRouteClient yandexMapsRouteClient;
        [Tooltip("Необязательный клиент Yandex Geocoder API, регистрируется как IYandexGeocoderClient.")]
        [SerializeField] private YandexGeocoderClient yandexGeocoderClient;
        [Tooltip("Необязательный presenter маршрута в шлеме, регистрируется как IYandexRoutePresenter.")]
        [SerializeField] private YandexHelmetRoutePresenter yandexHelmetRoutePresenter;
        [Tooltip("Необязательный фасад маршрутизации, регистрируется как IYandexHelmetNavigator.")]
        [SerializeField] private YandexHelmetNavigator yandexHelmetNavigator;
        [Tooltip("Необязательная проверка зданий по spatial mesh, регистрируется как ISpatialMeshYandexBuildingProbe.")]
        [SerializeField] private SpatialMeshYandexBuildingProbe spatialMeshYandexBuildingProbe;
        [SerializeField] private bool createManualQrAnchors = true;
        [SerializeField] private GameObject qrBtnPrefab;
        protected override void Configure(IContainerBuilder builder)
        {
            if (picoHandInput != null)
            {
                builder.RegisterComponent(picoHandInput)
                    .As<IPicoHandInput>();
            }

            if (picoCameraRenderTextureSource != null)
            {
                builder.RegisterComponent(picoCameraRenderTextureSource);
            }

            var hasQrPlacementDependencies =
                qrMarkerRegistry != null && qrCameraRayPoseResolver != null;

            if (qrMarkerRegistry != null)
            {
                builder.RegisterComponent(qrMarkerRegistry)
                    .As<IQrMarkerRegistry>();
            }

            if (qrCameraRayPoseResolver != null)
            {
                builder.RegisterComponent(qrCameraRayPoseResolver)
                    .As<IQrPoseResolver>();
            }

            builder.Register<QrMarkerPayloadParser>(Lifetime.Singleton)
                .As<IQrMarkerPayloadParser>();

            if (qrMarkerRegistry != null)
            {
                builder.Register<QrFactory>(Lifetime.Singleton);
            }

            builder.RegisterInstance(new QrManualAnchorOptions(createManualQrAnchors));
            builder.RegisterInstance(new QrManualAnchorVisualOptions(qrBtnPrefab));

            if (hasQrPlacementDependencies)
            {
                builder.Register<QrManualAnchorRegistry>(Lifetime.Singleton);

                builder.Register<QrMarkerAutoAnchorService>(Lifetime.Singleton)
                    .As<IQrMarkerPlacementService>();
            }

            if (yandexMapsRouteClient != null)
            {
                builder.RegisterComponent(yandexMapsRouteClient)
                    .As<IYandexRouteClient>();
            }

            if (yandexGeocoderClient != null)
            {
                builder.RegisterComponent(yandexGeocoderClient)
                    .As<IYandexGeocoderClient>();
            }

            if (yandexHelmetRoutePresenter != null)
            {
                builder.RegisterComponent(yandexHelmetRoutePresenter)
                    .As<IYandexRoutePresenter>();
            }

            if (yandexHelmetNavigator != null)
            {
                builder.RegisterComponent(yandexHelmetNavigator)
                    .As<IYandexHelmetNavigator>();
            }

            if (spatialMeshYandexBuildingProbe != null)
            {
                builder.RegisterComponent(spatialMeshYandexBuildingProbe)
                    .As<ISpatialMeshYandexBuildingProbe>();
            }

            if (picoHandInput != null)
            {
                builder.RegisterEntryPoint<HandDragService>(Lifetime.Singleton);
            }

            builder.Register<PicoQrCodeReader>(Lifetime.Singleton);
        }
    }
}
