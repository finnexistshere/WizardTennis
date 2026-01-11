using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PSXShaderKit
{
    [RequireComponent(typeof(Camera))]
    public class PSXPostProcessEffect : MonoBehaviour
    {
        private enum ColorEmulationMode
        {
            Off = 0,
            Fullscreen_Customizable = 1,
            Fullscreen_Accurate = 2,
            PerObject_Accurate = 3
        };

        private enum DitheringMatrixSize
        {
            Dither2x2 = 0,
            Dither4x4 = 1,
            Dither4x4_PS1Pattern = 2
        }

        [Header("Resolution")]
        [SerializeField, Range(0f, 1f)]
        private float _PixelationFactor = 1f;

        [Header("Color")]
        [SerializeField]
        private ColorEmulationMode _ColorEmulationMode = ColorEmulationMode.PerObject_Accurate;
        [SerializeField] private Vector3 _FullscreenColorDepth = new Vector3(256, 256, 256);
        [SerializeField] private Vector3 _FullscreenDitherDepth = new Vector3(32, 32, 32);
        [SerializeField] private DitheringMatrixSize _DitheringMatrixSize = DitheringMatrixSize.Dither4x4_PS1Pattern;
        [SerializeField, Range(0f, 1f)]
        private float _DitheringScale = 1f;

        [Header("Interlacing")]
        [SerializeField] private int _InterlacingSize = 1;

        [Header("Shaders")]
        [SerializeField] private Shader _PostProcessShader;
        [SerializeField] private Shader _PostProcessShaderAccurate;
        [SerializeField] private Shader _PixelationShader;
        [SerializeField] private Shader _InterlacingShader;

        private Material _PostProcessMaterial;
        private Material _PostProcessMaterialAccurate;
        private Material _PixelationMaterial;
        private Material _InterlacingMaterial;

        private RenderTexture _PreviousFrame;
        private bool _IsFirstFrame = true;

        private void Awake()
        {
            // Create materials dynamically on Awake so it works for instantiated cameras
            if (_PostProcessShader != null && _PostProcessShader.isSupported)
                _PostProcessMaterial = new Material(_PostProcessShader);

            if (_PostProcessShaderAccurate != null && _PostProcessShaderAccurate.isSupported)
                _PostProcessMaterialAccurate = new Material(_PostProcessShaderAccurate);

            if (_PixelationShader != null && _PixelationShader.isSupported)
                _PixelationMaterial = new Material(_PixelationShader);

            if (_InterlacingShader != null && _InterlacingShader.isSupported)
                _InterlacingMaterial = new Material(_InterlacingShader);
            else
                _InterlacingSize = -1;

            UpdateValues();
        }

        private void OnValidate()
        {
            UpdateValues();
        }

        private void UpdateValues()
        {
            Shader.SetGlobalFloat("_PSX_ObjectDithering", _ColorEmulationMode == ColorEmulationMode.PerObject_Accurate ? 1f : 0f);
        }

        private void OnDisable()
        {
            _IsFirstFrame = true;

            if (_PreviousFrame != null)
            {
                RenderTexture.ReleaseTemporary(_PreviousFrame);
                _PreviousFrame = null;
            }
        }

        private void ApplyPixelationEffect(RenderTexture source, RenderTexture destination)
        {
            if (_PixelationFactor >= 1f || _PixelationMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            FilterMode oldFilter = source.filterMode;
            source.filterMode = FilterMode.Point;

            _PixelationMaterial.SetFloat("_PixelationFactor", _PixelationFactor);
            Graphics.Blit(source, destination, _PixelationMaterial);

            source.filterMode = oldFilter;
        }

        private void ApplyDitheringEffect(RenderTexture source, RenderTexture destination)
        {
            if (_ColorEmulationMode == ColorEmulationMode.Off || _ColorEmulationMode == ColorEmulationMode.PerObject_Accurate)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (_ColorEmulationMode == ColorEmulationMode.Fullscreen_Customizable && _PostProcessMaterial != null)
            {
                _PostProcessMaterial.SetVector("_ColorResolution", _FullscreenColorDepth);
                _PostProcessMaterial.SetVector("_DitherResolution", _FullscreenDitherDepth);
                _PostProcessMaterial.SetFloat("_DitheringScale", _DitheringScale);

                float matrixVal = _DitheringMatrixSize switch
                {
                    DitheringMatrixSize.Dither2x2 => 0f,
                    DitheringMatrixSize.Dither4x4 => 0.5f,
                    DitheringMatrixSize.Dither4x4_PS1Pattern => 1f,
                    _ => 1f
                };

                _PostProcessMaterial.SetFloat("_HighResDitherMatrix", matrixVal);
                Graphics.Blit(source, destination, _PostProcessMaterial);
            }
            else if (_ColorEmulationMode == ColorEmulationMode.Fullscreen_Accurate && _PostProcessMaterialAccurate != null)
            {
                _PostProcessMaterialAccurate.SetFloat("_DitheringScale", _DitheringScale);
                Graphics.Blit(source, destination, _PostProcessMaterialAccurate);
            }
        }

        private void ApplyInterlacingEffect(RenderTexture source, RenderTexture destination)
        {
            if (_InterlacingSize <= 0 || _InterlacingMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            _InterlacingMaterial.SetFloat("_InterlacedFrameIndex", Time.frameCount % 2);
            _InterlacingMaterial.SetFloat("_InterlacingSize", _InterlacingSize);
            _InterlacingMaterial.SetTexture("_PreviousFrame", _IsFirstFrame ? source : _PreviousFrame);
            _IsFirstFrame = false;

            Graphics.Blit(source, destination, _InterlacingMaterial);

            if (_PreviousFrame != null)
                RenderTexture.ReleaseTemporary(_PreviousFrame);

            _PreviousFrame = RenderTexture.GetTemporary(source.descriptor);
            Graphics.Blit(source, _PreviousFrame);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            RenderTexture pixelRT = RenderTexture.GetTemporary(source.descriptor);
            pixelRT.filterMode = FilterMode.Point;
            ApplyPixelationEffect(source, pixelRT);

            RenderTexture ditherRT = RenderTexture.GetTemporary(source.descriptor);
            ditherRT.filterMode = FilterMode.Point;
            ApplyDitheringEffect(pixelRT, ditherRT);
            RenderTexture.ReleaseTemporary(pixelRT);

            ApplyInterlacingEffect(ditherRT, destination);
            RenderTexture.ReleaseTemporary(ditherRT);
        }
    }
}
