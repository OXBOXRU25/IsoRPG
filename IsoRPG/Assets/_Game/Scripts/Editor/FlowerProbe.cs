using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Есть ли у цветка стебель: замер САМОГО ПРЕФАБА, а не бокса в сцене.
    ///
    /// Первый заход я померил габарит рендерера прямо в сцене и объявил, что
    /// стебли есть у всех — высота выходила больше метра. Заказчик показал
    /// кадр, где лепестки лежат на земле, и был прав: бокс рендерера
    /// захватывает узлы уровней детализации и соседние объекты, поэтому
    /// мерил я не цветок, а куст рядом с ним.
    ///
    /// Здесь берётся сам файл префаба и его меши. Стебель виден по одному
    /// числу — отношению высоты к ширине: у лежащего лепестка оно около
    /// нуля, у цветка на стебле — около единицы и выше.
    /// </summary>
    public static class FlowerProbe
    {
        private static readonly string[] Kinds =
        {
            "SM_Env_Flowers_Flat_01", "SM_Env_Flowers_Flat_02", "SM_Env_Flowers_Flat_03",
            "SM_Env_Wildflowers_01", "SM_Env_Wildflowers_02", "SM_Env_Wildflowers_03",
            "SM_Env_Wildflowers_Patch_02", "SM_Env_Wildflowers_Patch_03",
            "SM_Env_Sunflower_01",
        };

        [MenuItem("Tools/IsoRPG/Щуп: у цветов есть стебли?", priority = 56)]
        public static void Measure()
        {
            Debug.Log("[IsoRPG] === ЕСТЬ ЛИ У ЦВЕТКА СТЕБЕЛЬ (замер префаба) ===");

            foreach (var name in Kinds)
            {
                var guid = AssetDatabase.FindAssets(name + " t:Prefab",
                               new[] { "Assets/PolygonNatureBiomes" }).FirstOrDefault();

                if (guid == null)
                {
                    Debug.Log("[IsoRPG] " + name + ": префаба нет.");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                                 AssetDatabase.GUIDToAssetPath(guid));

                if (prefab == null) continue;

                // Только САМЫЙ подробный уровень: у LOD-узлов геометрия
                // упрощена, и по ним о стебле судить нельзя.
                var lod = prefab.GetComponentInChildren<LODGroup>();

                Renderer[] rs = lod != null && lod.GetLODs().Length > 0
                    ? lod.GetLODs()[0].renderers.Where(r => r != null).ToArray()
                    : prefab.GetComponentsInChildren<Renderer>(true);

                if (rs.Length == 0) continue;

                var box = new Bounds();
                bool first = true;

                foreach (var r in rs)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    var b = mf.sharedMesh.bounds;

                    if (first) { box = b; first = false; }
                    else box.Encapsulate(b);
                }

                if (first) continue;

                float h = box.size.y;
                float w = Mathf.Max(box.size.x, box.size.z);
                float ratio = w > 0.001f ? h / w : 0f;

                Debug.Log("[IsoRPG] " + name + ": высота " + h.ToString("0.00") +
                          " м, ширина " + w.ToString("0.00") +
                          " м, отношение " + ratio.ToString("0.00") +
                          " — " + (ratio < 0.35f
                              ? "ЛЕЖИТ НА ЗЕМЛЕ, стебля нет"
                              : "стоит на стебле"));
            }
        }
    }
}
