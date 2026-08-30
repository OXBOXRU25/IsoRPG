using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Починка мира автора в URP — по шейдеру, а не по имени.
    ///
    /// Три беды заказчика (розовое колесо, пруд без воды, синий диск в небе)
    /// оказались одного корня и нашлись переписью шейдеров за один прогон:
    ///
    /// 1. материал воды создавался В ПАМЯТИ и не сохранялся файлом. После
    ///    перезагрузки сцены ссылка умирает, и объект остаётся вовсе без
    ///    материала — Unity рисует такой розовым. Поэтому здесь материал
    ///    сохраняется ассетом;
    /// 2. отбор шёл по куску имени меша («water»), и под него попадало
    ///    мельничное колесо (WaterWheel), а водоём с другим именем не
    ///    попадал вовсе. Отбираем по ШЕЙДЕРУ — он не врёт;
    /// 3. облачные кольца автора сидят на шейдере `Synty/Clouds`, который в
    ///    URP не рисуется, и кольцо в два с половиной километра читается как
    ///    синий купол над головой.
    /// </summary>
    public static class AuthorRepair
    {
        private const string WaterAsset = "Assets/_Game/Art/Materials/Water_Lake_URP.mat";

        [MenuItem("Tools/IsoRPG/Мир автора: починить материалы", priority = 31)]
        public static void Fix()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");

            if (lit == null)
            {
                Debug.LogError("[IsoRPG] Нет шейдера URP/Lit — чинить нечем.");
                return;
            }

            // --- 1. Вода: материал ФАЙЛОМ, а не в памяти ---------------------
            var water = AssetDatabase.LoadAssetAtPath<Material>(WaterAsset);

            if (water == null)
            {
                water = new Material(lit) { name = "Water_Lake_URP" };

                // Прозрачность: гладь должна пропускать дно, иначе пруд
                // читается как крашеная жесть.
                water.SetFloat("_Surface", 1f);            // Transparent
                water.SetFloat("_Blend", 0f);              // Alpha
                water.SetFloat("_ZWrite", 0f);
                water.SetFloat("_Smoothness", 0.85f);
                water.SetColor("_BaseColor", new Color(0.16f, 0.47f, 0.44f, 0.78f));
                water.renderQueue = 3000;

                System.IO.Directory.CreateDirectory(
                    System.IO.Path.GetDirectoryName(WaterAsset));

                AssetDatabase.CreateAsset(water, WaterAsset);
                AssetDatabase.SaveAssets();

                Debug.Log("[IsoRPG] Материал воды создан файлом: " + WaterAsset);
            }

            int wet = 0, orphan = 0, clouds = 0, domes = 0;

            foreach (var r in Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mats = r.sharedMaterials;
                bool touched = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];

                    // Потерянный материал — это и есть розовое пятно в игре.
                    // Чиним по соседям: у мельничного колеса тот же материал,
                    // что у самой мельницы.
                    if (m == null || m.shader == null)
                    {
                        mats[i] = NeighbourMaterial(r) ?? new Material(lit);
                        touched = true;
                        orphan++;
                        continue;
                    }

                    string shader = m.shader.name;

                    if (shader == "SyntyStudios/WaterShader")
                    {
                        mats[i] = water;
                        touched = true;
                        wet++;
                    }
                    // Небо НЕ ТРОГАЕМ.
                    //
                    // Купол автора рисовался в URP исправно; я перевёл его
                    // «за компанию» с облаками и потерял текстуру — небо
                    // стало плоским тёмно-синим силуэтом. Починка сломала
                    // исправное, и это худший род правки. Купол чинится
                    // только одним способом: не чинить.
                    else if (shader == "Synty/Clouds" && unlit != null)
                    {
                        // Облака переводим на прозрачный Unlit: свет им не
                        // нужен, а прозрачность — единственное, что делает
                        // их облаками, а не синим диском.
                        var cloud = new Material(unlit) { name = m.name + "_URP" };

                        cloud.SetFloat("_Surface", 1f);
                        cloud.SetFloat("_Blend", 0f);
                        cloud.SetFloat("_ZWrite", 0f);
                        cloud.renderQueue = 3000;

                        if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null)
                            cloud.SetTexture("_BaseMap", m.GetTexture("_MainTex"));

                        cloud.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.85f));

                        mats[i] = cloud;
                        touched = true;
                        clouds++;
                    }
                }

                if (touched) r.sharedMaterials = mats;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Мир автора починен: воды " + wet +
                      ", восстановлено потерянных материалов " + orphan +
                      ", облаков переведено " + clouds + ", куполов неба " + domes + ".");
        }

        /// <summary>
        /// Материал соседа по объекту: у колеса он тот же, что у мельницы.
        ///
        /// Ищем вверх по иерархии — у родителя или у братьев почти всегда
        /// стоит тот самый материал набора, который потерялся здесь.
        /// </summary>
        private static Material NeighbourMaterial(Renderer self)
        {
            var parent = self.transform.parent;

            for (int step = 0; step < 3 && parent != null; step++)
            {
                foreach (var r in parent.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == self) continue;

                    var m = r.sharedMaterial;
                    if (m != null && m.shader != null) return m;
                }

                parent = parent.parent;
            }

            return null;
        }
    }
}
