using UnityEngine;
using UnityEngine.Rendering;
#if URP
using UnityEngine.Rendering.Universal;

namespace StylizedWater2
{
    /// <summary>
    /// ПЕРЕПИСАНО ПОД UNITY 6 (URP 17) — OXBOX.
    ///
    /// Проход выставлял шейдеру три переключателя: экранные отражения,
    /// каустика от солнца, карта смещения. Все три требуют дополнительных
    /// проходов отрисовки, которых версия набора под Unity 6 не даёт.
    /// Выставляем выключенными один раз при настройке.
    /// </summary>
    public class SetupConstants : ScriptableRenderPass
    {
        private static readonly int _EnableDirectionalCaustics = Shader.PropertyToID("_EnableDirectionalCaustics");
        private static readonly int _WaterSSREnabled = Shader.PropertyToID("_WaterSSREnabled");
        private static readonly int _WaterDisplacementPrePassAvailable = Shader.PropertyToID("_WaterDisplacementPrePassAvailable");

        private StylizedWaterRenderFeature settings;

        public void Setup(StylizedWaterRenderFeature renderFeature)
        {
            settings = renderFeature;

            Shader.SetGlobalInt(_WaterSSREnabled, 0);
            Shader.SetGlobalInt(_WaterDisplacementPrePassAvailable, 0);
            Shader.SetGlobalInt(_EnableDirectionalCaustics, 0);
        }

        public override void RecordRenderGraph(
            UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph,
            ContextContainer frameData)
        {
        }

        public void Dispose()
        {
            Shader.SetGlobalInt(_WaterDisplacementPrePassAvailable, 0);
            Shader.SetGlobalInt(_WaterSSREnabled, 0);
            Shader.SetGlobalInt(_EnableDirectionalCaustics, 0);
        }
    }
}
#endif
