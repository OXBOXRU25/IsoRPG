using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Переводит материалы новых наборов мобов на URP.
    ///
    /// Старые паки (Malbers Boar, POLYGON Horse 2018 года) несут материалы
    /// на встроенном шейдере Standard — в URP он не поддерживается вовсе и
    /// рисуется сплошным розовым, той же бедой, что была у воды набора.
    /// Härdcode-имя шейдера не проверяем: годится любой материал, чей
    /// шейдер не из Universal Render Pipeline.
    ///
    /// Материал СОХРАНЯЕМ ФАЙЛОМ (AssetDatabase.CreateAsset), а не держим
    /// в памяти: несохранённый материал живёт до перезагрузки сцены, а
    /// потом объект остаётся вовсе без материала и становится розовым —
    /// на этом уже горели с водой.
    /// </summary>
    public static class MobMaterials
    {
        private const string Folder = "Assets/_Game/Art/Materials/Mobs";

        /// <summary>
        /// Проходит по всем рендерерам модели и меняет материалы с
        /// устаревшим шейдером на URP-копии с тем же текстурой/цветом.
        /// Копии кладутся рядом, по одной на исходный материал — повторный
        /// вызов на другом экземпляре той же модели находит готовую и
        /// новую не создаёт.
        /// </summary>
        public static int FixLegacyShaders(GameObject model)
        {
            if (!EnsureFolder()) return 0;

            int fixedCount = 0;

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;   // рендерер вовсе без материала — не наш случай, чинить нечего
                    if (src.shader != null && src.shader.name.StartsWith("Universal Render Pipeline"))
                        continue;   // уже URP — трогать нечего

                    mats[i] = Convert(src);
                    changed = true;
                    fixedCount++;
                }

                if (changed) renderer.sharedMaterials = mats;
            }

            if (fixedCount > 0) AssetDatabase.SaveAssets();

            return fixedCount;
        }

        /// <summary>
        /// Поставить КОНКРЕТНЫЙ материал набора на все рендереры модели —
        /// в URP-переводе. Нужен, когда у модели рендереры вовсе БЕЗ
        /// материала (materialImportMode: 0 в мете FBX — так было у
        /// «Horse Poly Art»): FixLegacyShaders такое не чинит, потому что
        /// нечего чинить с его точки зрения — слот пуст, а не занят чужим
        /// шейдером. Тогда материал берём явно, а не ищем на модели.
        /// </summary>
        public static void ApplyMaterial(GameObject model, Material src)
        {
            if (src == null || !EnsureFolder()) return;

            var urp = src.shader != null && src.shader.name.StartsWith("Universal Render Pipeline")
                ? src
                : Convert(src);

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = urp;
                renderer.sharedMaterials = mats;
            }
        }

        private static bool EnsureFolder()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("[IsoRPG] Шейдера URP/Lit нет — материалы мобов не поправить.");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Game/Art/Materials", "Mobs");

            return true;
        }

        private static Material Convert(Material src)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");

            string path = Folder + "/" + src.name + "_URP.mat";
            var urp = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (urp != null) return urp;

            urp = new Material(lit) { name = src.name + "_URP" };

            foreach (var key in new[] { "_MainTex", "_BaseMap", "_Albedo" })
                if (src.HasProperty(key) && src.GetTexture(key) != null)
                {
                    urp.SetTexture("_BaseMap", src.GetTexture(key));
                    break;
                }

            foreach (var key in new[] { "_Color", "_BaseColor", "_Tint" })
                if (src.HasProperty(key))
                {
                    var c = src.GetColor(key);
                    if (c.maxColorComponent > 0.02f) urp.SetColor("_BaseColor", c);
                    break;
                }

            AssetDatabase.CreateAsset(urp, path);
            return urp;
        }
    }
}
