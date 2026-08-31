using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Возвращает эффектам набора прозрачность и цвет.
    ///
    /// Что было сломано. Общая URP-починка (`materials`, <see cref="UrpMaterialFix"/>)
    /// переводит материалы покупных наборов со встроенных шейдеров на
    /// `Universal Render Pipeline/Lit`. Для стен и бочек это верно, для ЧАСТИЦ —
    /// нет: Lit не читает вершинный цвет частицы и рисует её непрозрачной.
    /// В игре это выглядело так, что огонь, искры и дым превратились в
    /// одинаковые белые кляксы — 31.08.2026 Павлон увидел «дым вместо огня»
    /// и был прав: цвета у пламени действительно не осталось.
    ///
    /// Розовым такой материал не становится, ошибок в журнале нет — поломку
    /// нечем заметить, кроме как глазами в игре.
    ///
    /// Лечение: тем же частицам ставим `Particles/Unlit` из Universal и
    /// смешивание по смыслу материала — свечение (огонь, искры, вспышки)
    /// складывается с фоном, дым и пыль кладутся по альфе.
    /// </summary>
    public static class FxMaterials
    {
        private const string Folder = "Assets/Synty/PolygonParticleFX";

        /// <summary>
        /// Материалы свечения. Отбираем по имени, потому что исходный шейдер
        /// уже потерян общей починкой и спросить, каким он был, не у кого.
        /// Имена у Synty говорящие: Additive, Emissive, Glow, Light.
        /// </summary>
        private static readonly string[] AdditiveMarks =
        {
            "additive", "emissive", "emission", "glow", "light", "flare", "spark",
        };

        public static void Apply()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                Debug.LogError("[IsoRPG] Нет шейдера Universal Render Pipeline/Particles/Unlit.");
                return;
            }

            int fixedCount = 0, additive = 0, alpha = 0, skipped = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null) continue;

                // Уже на частичном шейдере — не трогаем: повторный прогон не
                // должен переназначать то, что кто-то настроил руками.
                if (material.shader != null &&
                    material.shader.name.Contains("Particles"))
                {
                    skipped++;
                    continue;
                }

                // Забираем ДО смены шейдера: после неё старых свойств у
                // материала уже нет.
                Texture map = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap")
                            : material.HasProperty("_MainTex") ? material.GetTexture("_MainTex")
                            : null;

                Color tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                           : material.HasProperty("_Color") ? material.GetColor("_Color")
                           : Color.white;

                bool glow = false;
                string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                foreach (var mark in AdditiveMarks)
                    if (name.Contains(mark)) { glow = true; break; }

                material.shader = shader;

                if (map != null)
                {
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", map);
                    material.mainTexture = map;
                }

                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);

                // Прозрачность включается набором свойств И ключевым словом:
                // хоть одно несовпадение — и альфа игнорируется молча. Тот же
                // урок, что с кольцом выделения цели.
                material.SetFloat("_Surface", 1f);                 // Transparent
                material.SetFloat("_Blend", glow ? 2f : 0f);       // Additive / Alpha
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_AlphaClip", 0f);

                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", glow ? (float)BlendMode.One
                                                    : (float)BlendMode.OneMinusSrcAlpha);

                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;

                // Вершинный цвет частицы — то, ради чего всё и затевалось:
                // именно им система красит пламя от жёлтого к красному.
                if (material.HasProperty("_ColorMode")) material.SetFloat("_ColorMode", glow ? 1f : 0f);

                EditorUtility.SetDirty(material);

                fixedCount++;
                if (glow) additive++; else alpha++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Эффекты на URP: починено материалов " + fixedCount +
                      " (свечение " + additive + ", прозрачность " + alpha +
                      "), пропущено " + skipped + ".");

            Check();
        }

        /// <summary>Щуп: читаем результат, а не журнал.</summary>
        private static void Check()
        {
            int lit = 0, particles = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (material == null || material.shader == null) continue;

                if (material.shader.name.Contains("Particles")) particles++;
                else if (material.shader.name.EndsWith("/Lit")) lit++;
            }

            Debug.Log("[IsoRPG] Материалы эффектов: на шейдере частиц " + particles +
                      ", осталось на Lit " + lit + ".");

            if (lit > 0)
                Debug.LogError("[IsoRPG] " + lit + " материалов эффектов всё ещё на Lit — будут белыми.");
        }
    }
}
