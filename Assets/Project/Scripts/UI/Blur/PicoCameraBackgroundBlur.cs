using UnityEngine;
using UnityEngine.UI;

namespace Project.Scripts.UI
{
    [AddComponentMenu("Project/UI/Pico Camera Background Blur")]
    [DisallowMultipleComponent]
    public sealed class PicoCameraBackgroundBlur : MonoBehaviour
    {
        public const string GlobalBlurredTextureName = "_PicoCameraBlurredTexture";

        [Header("Source")]
        [Tooltip("Источник картинки с камеры Pico. Если поле пустое, компонент попробует найти PicoCameraRenderTextureSource в сцене автоматически.")]
        [SerializeField] private PicoCameraRenderTextureSource cameraSource;
        [Tooltip("Необязательная ручная текстура-источник. Если задана, она используется вместо RenderTexture из PicoCameraRenderTextureSource.")]
        [SerializeField] private Texture sourceTextureOverride;

        [Header("Blur")]
        [Tooltip("Во сколько раз уменьшать текстуру перед размытием. Больше значение = быстрее и мягче, но меньше деталей.")]
        [SerializeField, Range(1, 6)] private int downsample = 2;
        [Tooltip("Количество проходов размытия. Больше проходов = сильнее blur, но выше нагрузка.")]
        [SerializeField, Range(1, 8)] private int iterations = 3;
        [Tooltip("Радиус смещения сэмплов в blur-шейдере. Увеличивайте для более выраженного frosted glass эффекта.")]
        [SerializeField, Range(0.25f, 8f)] private float radius = 2.5f;
        [Tooltip("Отдавать наружу полноразмерную сглаженную текстуру. Убирает квадратные пиксели после downsample.")]
        [SerializeField] private bool fullResolutionOutput = true;
        [Tooltip("Необязательный материал для размытия. Если пусто, материал создается автоматически из Hidden/Project/KawaseBlur.")]
        [SerializeField] private Material blurMaterial;

        [Header("Output")]
        [Tooltip("Публиковать размытую текстуру как глобальную shader texture _PicoCameraBlurredTexture. Обычно можно оставить включенным.")]
        [SerializeField] private bool setGlobalTexture = true;

        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly int GlobalBlurredTextureId = Shader.PropertyToID(GlobalBlurredTextureName);

        private RenderTexture blurredTexture;
        private RenderTexture pingTexture;
        private RenderTexture pongTexture;
        private Material runtimeBlurMaterial;

        public RenderTexture BlurredTexture => blurredTexture;
        public RawImage PreviewRawImage
        {
            get
            {
                if (cameraSource == null)
                {
                    cameraSource = FindFirstObjectByType<PicoCameraRenderTextureSource>();
                }

                return cameraSource != null ? cameraSource.PreviewRawImage : null;
            }
        }

        public Texture SourceTexture
        {
            get
            {
                if (sourceTextureOverride != null)
                {
                    return sourceTextureOverride;
                }

                if (cameraSource == null)
                {
                    cameraSource = FindFirstObjectByType<PicoCameraRenderTextureSource>();
                }

                return cameraSource != null ? cameraSource.TargetTexture : null;
            }
        }

        private void LateUpdate()
        {
            RenderBlurredTexture();
        }

        private void OnDisable()
        {
            ReleaseTextures();

            if (runtimeBlurMaterial != null)
            {
                DestroyUnityObject(runtimeBlurMaterial);
                runtimeBlurMaterial = null;
            }
        }

        public void RenderBlurredTexture()
        {
            var sourceTexture = SourceTexture;
            if (sourceTexture == null || sourceTexture.width <= 0 || sourceTexture.height <= 0)
            {
                return;
            }

            var material = GetBlurMaterial();
            if (material == null)
            {
                return;
            }

            var blurWidth = Mathf.Max(1, sourceTexture.width >> downsample);
            var blurHeight = Mathf.Max(1, sourceTexture.height >> downsample);
            var outputWidth = fullResolutionOutput ? sourceTexture.width : blurWidth;
            var outputHeight = fullResolutionOutput ? sourceTexture.height : blurHeight;

            EnsureRenderTextures(blurWidth, blurHeight, outputWidth, outputHeight);

            Graphics.Blit(sourceTexture, pingTexture);

            for (var i = 0; i < iterations; i++)
            {
                material.SetFloat(OffsetId, radius + i);
                Graphics.Blit(pingTexture, pongTexture, material, 0);
                (pingTexture, pongTexture) = (pongTexture, pingTexture);
            }

            Graphics.Blit(pingTexture, blurredTexture, material, 1);

            if (setGlobalTexture)
            {
                Shader.SetGlobalTexture(GlobalBlurredTextureId, blurredTexture);
            }
        }

        private Material GetBlurMaterial()
        {
            if (blurMaterial != null)
            {
                return blurMaterial;
            }

            if (runtimeBlurMaterial != null)
            {
                return runtimeBlurMaterial;
            }

            var shader = Shader.Find("Hidden/Project/KawaseBlur");
            if (shader == null)
            {
                Debug.LogError("Shader 'Hidden/Project/KawaseBlur' was not found.", this);
                return null;
            }

            runtimeBlurMaterial = new Material(shader)
            {
                name = "Runtime Pico Camera Blur",
                hideFlags = HideFlags.HideAndDontSave
            };

            return runtimeBlurMaterial;
        }

        private void EnsureRenderTextures(int blurWidth, int blurHeight, int outputWidth, int outputHeight)
        {
            if (blurredTexture != null &&
                blurredTexture.width == outputWidth &&
                blurredTexture.height == outputHeight &&
                pingTexture != null &&
                pingTexture.width == blurWidth &&
                pingTexture.height == blurHeight)
            {
                return;
            }

            ReleaseTextures();

            blurredTexture = CreateRenderTexture(outputWidth, outputHeight, "Pico Camera Blurred");
            pingTexture = CreateRenderTexture(blurWidth, blurHeight, "Pico Camera Blur Ping");
            pongTexture = CreateRenderTexture(blurWidth, blurHeight, "Pico Camera Blur Pong");
        }

        private static RenderTexture CreateRenderTexture(int width, int height, string textureName)
        {
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture.Create();
            return texture;
        }

        private void ReleaseTextures()
        {
            ReleaseRenderTexture(ref blurredTexture);
            ReleaseRenderTexture(ref pingTexture);
            ReleaseRenderTexture(ref pongTexture);
        }

        private static void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            DestroyUnityObject(texture);
            texture = null;
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
