using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PSXShaderKit
{
    public class PSXPostProcessFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Header("Resolution")]
            [Range(0f, 1f)]
            public float pixelationFactor = 1f;

            [Header("Color")]
            public Vector3 fullscreenColorDepth = new Vector3(256, 256, 256);
            public Vector3 fullscreenDitherDepth = new Vector3(32, 32, 32);
            [Range(0f, 1f)]
            public float ditheringScale = 1f;

            [Header("Interlacing")]
            public int interlacingSize = 1;

            [Header("Shaders")]
            public Shader pixelationShader;
            public Shader ditheringShader;
            public Shader ditheringAccurateShader;
            public Shader interlacingShader;
        }

        public Settings settings = new Settings();

        private PSXPostProcessPass pass;

        public override void Create()
        {
            pass = new PSXPostProcessPass(settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.pixelationShader == null ||
                settings.ditheringShader == null ||
                settings.interlacingShader == null)
                return;

            pass.Setup(renderer.cameraColorTargetHandle);
            renderer.EnqueuePass(pass);
        }
    }
}
