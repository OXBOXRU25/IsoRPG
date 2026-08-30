using System;
using UnityEngine;
using UnityEngine.Rendering;

#if URP
using UnityEngine.Rendering.Universal;

namespace StylizedWater2
{
    /// <summary>
    /// ЗАГЛУШКА ПОД UNITY 6 (URP 17) — OXBOX.
    ///
    /// Проход рисовал карту смещения для волн от объектов. Он переопределял
    /// методы, удалённые из нового URP, и из-за него не собирался весь
    /// набор. Волн в нашей игре нет, поэтому проход оставлен пустым.
    /// </summary>
    public class DisplacementPrePass : ScriptableRenderPass
    {
        public const string KEYWORD = "WATER_DISPLACEMENT_PASS";
        public const float VOID_THRESHOLD = -1000f;

        [Serializable]
        public class Settings
        {
            public bool enable;
            public float range = 500f;

            [Range(0.1f, 4f)]
            public float cellSize = 0.25f;
        }

        private Settings settings;

        public void Setup(Settings settings)
        {
            this.settings = settings;
            Shader.DisableKeyword(KEYWORD);
        }

        public override void RecordRenderGraph(
            UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph,
            ContextContainer frameData)
        {
        }

        public void Dispose()
        {
            Shader.DisableKeyword(KEYWORD);
        }
    }
}
#endif
