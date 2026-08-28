using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Кладёт на землю текстуру из набора вместо плоской заливки.
    ///
    /// До сих пор пол был одним цветом — осмысленно, пока шла механика: цвет
    /// не имеет швов и не стоит ничего. Но как только над ним появился лес с
    /// прорисованной корой и травой, заливка стала единственной плоской вещью
    /// в кадре, и глаз цепляется именно за неё.
    ///
    /// Тайлинг считается от размера листа, а не задаётся числом: земля у нас
    /// 240 метров, и «повторить 20 раз» на ней означает клетку в двенадцать
    /// метров — текстура превращается в мыло. Считаем от метража, тогда
    /// правка размера арены не ломает вид.
    ///
    /// Второй слой — детальная карта той же подстилки с частым повтором.
    /// Без него на близком плане видна нехватка разрешения: одна текстура не
    /// может одновременно держать и общий вид поля, и траву под ногами.
    /// </summary>
    public static class GroundSkin
    {
        private const string Textures =
            "Assets/TriForge Assets/Fantasy Forest Environment/Textures/Terrain/";

        /// <summary>Основа — луговая трава, она ближе всего к нашей поляне.</summary>
        private const string Base = "T_FFE_Grass01.tga";

        /// <summary>
        /// Детальный слой отключён. Замер: у Forestfloor01 средняя яркость 33
        /// из 255, у Grass01 — 52. Наложенные друг на друга, они дали поле
        /// темнее, чем была плоская заливка, — то есть текстура сделала хуже
        /// ровно то, ради чего её клали. Вернём, когда будет светлая деталь.
        /// </summary>
        private const string Detail = null;

        /// <summary>
        /// Сторона одной плитки основы, метров.
        ///
        /// 2.5, а не 12. Текстура нарисована для террейна и содержит
        /// отдельные травинки в натуральную величину: растянутая на двенадцать
        /// метров, она даёт мазки ростом с человека, и земля читается как
        /// ковёр с рисунком, а не как трава. Плитку берём того порядка, каков
        /// настоящий размер нарисованного на ней.
        /// </summary>
        private const float BaseTile = 2.5f;

        /// <summary>Сторона плитки детального слоя, метров.</summary>
        private const float DetailTile = 3f;

        /// <summary>
        /// Печатает среднюю яркость всех текстур земли из набора.
        ///
        /// Нужно потому, что «трава» у разных авторов бывает и салатовой, и
        /// почти чёрной: T_FFE_Grass01 это лесная трава в тени, и на большой
        /// плоскости она читается как гарь. Выбирать по имени файла — верный
        /// способ сделать поле темнее, чем было до текстуры.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Земля: яркость текстур набора", priority = 20)]
        public static void Probe()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[]
                     { Textures.TrimEnd(Char.Parse("/")) }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null) continue;

                bool wasReadable = importer.isReadable;
                if (!wasReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                var pixels = tex.GetPixels32();
                long r = 0, g = 0, b = 0;
                int step = Mathf.Max(1, pixels.Length / 20000);
                int n = 0;

                for (int i = 0; i < pixels.Length; i += step)
                {
                    r += pixels[i].r; g += pixels[i].g; b += pixels[i].b; n++;
                }

                Debug.Log("[IsoRPG] " + System.IO.Path.GetFileName(path) +
                          ": средний цвет (" + (r / n) + ", " + (g / n) + ", " + (b / n) +
                          "), яркость " + ((r + g + b) / (3f * n)).ToString("0") + " из 255");

                if (!wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }

        [MenuItem("Tools/IsoRPG/Земля: текстура подстилки", priority = 19)]
        public static void Apply()
        {
            var sheet = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                              .FirstOrDefault(g => g.name == "GroundSheet");

            if (sheet == null)
            {
                Debug.LogWarning("[IsoRPG] Не нашёл GroundSheet — земля не покрашена.");
                return;
            }

            var renderer = sheet.GetComponent<Renderer>();
            var material = renderer != null ? renderer.sharedMaterial : null;

            if (material == null)
            {
                Debug.LogWarning("[IsoRPG] У земли нет материала.");
                return;
            }

            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + Base);
            var detailMap = string.IsNullOrEmpty(Detail)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + Detail);

            if (baseMap == null)
            {
                Debug.LogWarning("[IsoRPG] Нет текстуры " + Textures + Base);
                return;
            }

            // Реальный размер листа: примитив Plane — десять метров на сторону,
            // всё остальное даёт масштаб.
            float side = sheet.transform.localScale.x * 10f;

            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", Vector2.one * (side / BaseTile));

            // Множитель БОЛЬШЕ единицы, и это не произвол, а арифметика.
            // Прежняя заливка светила примерно как 112 по зелёному, у
            // текстуры зелёный 83 — значит, чтобы поле осталось той же
            // яркости, цвет должен быть около 1.35. Берём с небольшим
            // запасом и чуть теплее: земля под кронами и так уходит в тень.
            material.color = new Color(1.55f, 1.40f, 1.15f);
            material.SetFloat("_Smoothness", 0f);

            if (detailMap != null)
            {
                material.EnableKeyword("_DETAIL_MULX2");
                material.SetTexture("_DetailAlbedoMap", detailMap);
                material.SetTextureScale("_DetailAlbedoMap", Vector2.one * (side / DetailTile));
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[IsoRPG] Земля покрыта текстурой: лист " + side.ToString("0") +
                      " м, плитка основы " + BaseTile + " м (" +
                      (side / BaseTile).ToString("0") + " повторов), детальный слой " +
                      (detailMap != null ? DetailTile + " м" : "нет") + ".");
        }
    }
}
