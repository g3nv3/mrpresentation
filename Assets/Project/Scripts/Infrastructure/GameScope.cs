using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Project.Scripts
{
    public class GameScope : LifetimeScope
    {
        [SerializeField] private PicoHandInput picoHandInput;
        [SerializeField] private PicoCameraRenderTextureSource picoCameraRenderTextureSource;
        [SerializeField] private QrMarkerRegistry qrMarkerRegistry;
        [SerializeField] private QrCameraRayPoseResolver qrCameraRayPoseResolver;
        [SerializeField] private bool createManualQrAnchors = true;

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

            builder.RegisterInstance(new QrManualAnchorOptions(createManualQrAnchors));

            if (hasQrPlacementDependencies)
            {
                builder.Register<QrManualAnchorRegistry>(Lifetime.Singleton);

                builder.Register<QrMarkerAutoAnchorService>(Lifetime.Singleton)
                    .As<IQrMarkerPlacementService>();
            }
            else
            {
                builder.Register<NullQrMarkerPlacementService>(Lifetime.Singleton)
                    .As<IQrMarkerPlacementService>();
            }

            if (picoHandInput != null)
            {
                builder.RegisterEntryPoint<HandDragService>(Lifetime.Singleton);
            }

            builder.Register<PicoQrCodeReader>(Lifetime.Singleton);
        }
    }
}
