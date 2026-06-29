using UnityEngine;
using VContainer;
using VContainer.Unity;
using Project.Scripts.Interaction;
using Project.Scripts.UI;

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
        [Tooltip("OpenRouteService client. When assigned, it is registered as IYandexRouteClient instead of Yandex Route API.")]
        [SerializeField] private OpenRouteServiceClient openRouteServiceClient;
        [Tooltip("Необязательный клиент Yandex Geocoder API, регистрируется как IYandexGeocoderClient.")]
        [SerializeField] private YandexGeocoderClient yandexGeocoderClient;
        [Tooltip("Необязательный клиент GigaChat API, регистрируется как IGigaChatClient.")]
        [SerializeField] private GigaChatClient gigaChatClient;
        [Tooltip("Необязательный presenter маршрута в шлеме, регистрируется как IYandexRoutePresenter.")]
        [SerializeField] private YandexHelmetRoutePresenter yandexHelmetRoutePresenter;
        [Tooltip("Необязательный фасад маршрутизации, регистрируется как IYandexHelmetNavigator.")]
        [SerializeField] private YandexHelmetNavigator yandexHelmetNavigator;
        [Tooltip("Необязательная проверка зданий по spatial mesh, регистрируется как ISpatialMeshYandexBuildingProbe.")]
        [SerializeField] private SpatialMeshYandexBuildingProbe spatialMeshYandexBuildingProbe;
        [Tooltip("Менеджер UI-окон, регистрируется как IUiWindowManager.")]
        [SerializeField] private UiWindowManager uiWindowManager;
        [Tooltip("Presenter, в который runtime-кнопки пробрасывают действие BuildPathTo.")]
        [SerializeField] private NavMeshPathPresenter navMeshPathPresenter;
        [SerializeField] private bool createManualQrAnchors = true;
        [SerializeField] private GameObject qrBtnPrefab;
        [SerializeField] private QrMarkerInstallNotificationPanel qrMarkerInstallNotificationPanel;
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
            builder.RegisterInstance(new QrMarkerInstallNotificationOptions(qrMarkerInstallNotificationPanel));

            if (hasQrPlacementDependencies)
            {
                builder.Register<QrManualAnchorRegistry>(Lifetime.Singleton);

                builder.Register<QrMarkerAutoAnchorService>(Lifetime.Singleton)
                    .As<IQrMarkerPlacementService>();
            }

            if (openRouteServiceClient != null)
            {
                builder.RegisterComponent(openRouteServiceClient)
                    .As<IYandexRouteClient>();
            }
            else if (yandexMapsRouteClient != null)
            {
                builder.RegisterComponent(yandexMapsRouteClient)
                    .As<IYandexRouteClient>();
            }

            if (yandexGeocoderClient != null)
            {
                builder.RegisterComponent(yandexGeocoderClient)
                    .As<IYandexGeocoderClient>();
            }

            if (gigaChatClient != null)
            {
                builder.RegisterComponent(gigaChatClient)
                    .As<IGigaChatClient>();
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

            builder.RegisterInstance(new UiGeneratedButtonOptions(navMeshPathPresenter));

            builder.RegisterComponent(uiWindowManager)
                .As<IUiWindowManager>();
            builder.Register<UiGeneratedButtonSpawner>(Lifetime.Singleton)
                .As<IUiGeneratedButtonSpawner>();
            builder.Register<UiSelectedTargetState>(Lifetime.Singleton)
                .As<IUiSelectedTargetState>();


            builder.RegisterComponent(navMeshPathPresenter);

            if (picoHandInput != null)
            {
                builder.RegisterEntryPoint<HandDragService>(Lifetime.Singleton);
            }

            builder.Register<PicoQrCodeReader>(Lifetime.Singleton);
        }
    }
}
