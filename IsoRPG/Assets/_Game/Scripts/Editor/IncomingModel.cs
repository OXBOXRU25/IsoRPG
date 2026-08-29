using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит присланную модель в игру, рядом с героем.
    ///
    /// Числа и снимки отвечают на вопрос «что это», но не на вопрос «наша
    /// ли она». На него отвечает только соседство: модель рядом с нашим
    /// героем, нашими постройками и старым скелетом — и разницу в стиле
    /// видно за секунду, без обсуждений.
    ///
    /// Ставим у зала, где игрок появляется: туда всё равно первым делом
    /// попадаешь.
    /// </summary>
    public static class IncomingModel
    {
        private const string HolderName = "IncomingModel";
        private const string Model = "Assets/_Game/Art/Incoming/Knight_Game.fbx";

        [MenuItem("Tools/IsoRPG/Присланная модель: поставить рядом с героем", priority = 63)]
        public static void Place()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            Clear();

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Model);

            if (asset == null)
            {
                Debug.LogError("[IsoRPG] Не нашёл модель " + Model);
                return;
            }

            var holder = new GameObject(HolderName);

            // Чуть в стороне от точки появления: под ноги героя ставить
            // нельзя — он окажется внутри модели и не поймёт, что видит.
            Vector3 at = RuinsLayout.HallCentre + new Vector3(3f, 0f, 3f);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.transform);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 200f, 0f);

            // Сажаем на землю и сообщаем настоящий рост: если модель придёт
            // в сантиметрах или в дюймах, это видно сразу по числу, а не по
            // недоумению в игре.
            var renderers = go.GetComponentsInChildren<Renderer>()
                              .Where(r => !(r is ParticleSystemRenderer))
                              .ToArray();

            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                go.transform.position += new Vector3(0f, at.y - bounds.min.y, 0f);

                Debug.Log("[IsoRPG] Модель поставлена у зала, в " + at +
                          ". Её рост " + bounds.size.y.ToString("0.00") +
                          " м при нашем герое 1.9 м.");
            }

            Selection.activeGameObject = holder;
        }

        [MenuItem("Tools/IsoRPG/Присланная модель: убрать", priority = 64)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }
    }
}
