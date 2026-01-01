using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PSXShaderKit
{
    public class PSXPostProcessPass : ScriptableRenderPass
    {
        private PSXPostProcessFeature.Settings settings;

        private Material pixelationMat;
        private Material ditherMat;
        private Material ditherAccurateMat;
        private Material interlaceMat;

        private RTHandle source;
        private RTHandle tempA;
        private RTHandle tempB;
        private RTHandle previousFrame;

        private bool firstFrame = true;

        public PSXPostProcessPass(PSXPostProcessFeature.Settings settings)
        {
            this.settings = settings;

            pixelationMat = CoreUtils.CreateEngineMaterial(settings.pixelationShader);
            ditherMat = CoreUtils.CreateEngineMaterial(settings.ditheringShader);
            ditherAccurateMat = CoreUtils.CreateEngineMaterial(settings.ditheringAccurateShader);
            interlaceMat = CoreUtils.CreateEngineMaterial(settings.interlacingShader);
        }

        public void Setup(RTHandle source)
        {
            this.source = source;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            tempA = RTHandles.Alloc(desc, name: "_PSX_TempA");
            tempB = RTHandles.Alloc(desc, name: "_PSX_TempB");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("PSX Post Process");

            // ---------- Pixelation ----------
            if (settings.pixelationFactor < 1f)
            {
                pixelationMat.SetFloat("_PixelationFactor", settings.pixelationFactor);
                Blit(cmd, source, tempA, pixelationMat);
            }
            else
            {
                Blit(cmd, source, tempA);
            }

            // ---------- Dithering ----------
            ditherMat.SetVector("_ColorResolution", settings.fullscreenColorDepth);
            ditherMat.SetVector("_DitherResolution", settings.fullscreenDitherDepth);
            ditherMat.SetFloat("_DitheringScale", settings.ditheringScale);

            Blit(cmd, tempA, tempB, ditherMat);

            // ---------- Interlacing ----------
            if (settings.interlacingSize > 0)
            {
                if (previousFrame == null)
                    previousFrame = RTHandles.Alloc(tempB.rt.descriptor, name: "_PSX_Prev");

                interlaceMat.SetFloat("_InterlacedFrameIndex", Time.frameCount % 2);
                interlaceMat.SetFloat("_InterlacingSize", settings.interlacingSize);
                interlaceMat.SetTexture("_PreviousFrame", firstFrame ? tempB : previousFrame);

                firstFrame = false;

                Blit(cmd, tempB, source, interlaceMat);
                Blit(cmd, source, previousFrame);
            }
            else
            {
                Blit(cmd, tempB, source);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            RTHandles.Release(tempA);
            RTHandles.Release(tempB);
        }

        public override void OnFinishCameraStackRendering(CommandBuffer cmd)
        {
            firstFrame = true;
        }
    }
}
