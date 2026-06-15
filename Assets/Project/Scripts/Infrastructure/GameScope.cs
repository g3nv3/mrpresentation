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
        [Tooltip("Необязательный клиент Yandex Geocoder API, регистрируется как IYandexGeocoderClient.")]
        [SerializeField] private YandexGeocoderClient yandexGeocoderClient;
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
        [Tooltip("Scene toggle components, registered as IUiToggleState by UiToggleId key.")]
        [SerializeField] private UiToggleRegistration[] uiToggles;
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

            builder.RegisterInstance(new UiGeneratedButtonOptions(navMeshPathPresenter));

            RegisterUiToggles(builder);

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

        private void RegisterUiToggles(IContainerBuilder builder)
        {
            if (uiToggles == null)
            {
                return;
            }

            for (var i = 0; i < uiToggles.Length; i++)
            {
                var registration = uiToggles[i];
                if (registration == null || registration.TargetObject == null)
                {
                    continue;
                }

                if (!registration.TryGetState(out var state))
                {
                    var stateCount = registration.GetStateCount();
                    Debug.LogError(
                        $"{registration.TargetObject.name} must have exactly one component implementing {nameof(IUiToggleState)} to be registered as '{registration.Id}'. Found: {stateCount}.",
                        registration.TargetObject);
                    continue;
                }

                builder.RegisterInstance(state)
                    .Keyed(registration.Id);
            }
        }
    }
}
