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
        [SerializeField] private QrRaycastPoseResolver qrRaycastPoseResolver;

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

            var hasQrPlacementDependencies = qrMarkerRegistry != null && qrRaycastPoseResolver != null;

            if (qrMarkerRegistry != null)
            {
                builder.RegisterComponent(qrMarkerRegistry)
                    .As<IQrMarkerRegistry>();
            }

            if (qrRaycastPoseResolver != null)
            {
                builder.RegisterComponent(qrRaycastPoseResolver)
                    .As<IQrPoseResolver>();
            }

            builder.Register<QrMarkerPayloadParser>(Lifetime.Singleton)
                .As<IQrMarkerPayloadParser>();

            if (hasQrPlacementDependencies)
            {
                builder.Register<QrMarkerPlacementService>(Lifetime.Singleton)
                    .As<IQrMarkerPlacementService>();
            }
            else
            {
                builder.Register<NullQrMarkerPlacementService>(Lifetime.Singleton)
                    .As<IQrMarkerPlacementService>();
            }

            builder.Register<PicoQrCodeReader>(Lifetime.Singleton);
        }
    }
}
