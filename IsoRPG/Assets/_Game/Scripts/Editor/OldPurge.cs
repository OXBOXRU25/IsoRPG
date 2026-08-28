using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Снос старого набора: всё, что пришло из KayKit, вон из сцены.
    ///
    /// Ищем не по именам объектов, а по ИСТОЧНИКУ МЕША. Имена в сцене
    /// расставлены разными сборщиками в разное время: где-то «Wall_01», где-то
    /// «Стена», где-то вовсе без имени. По именам такая чистка всегда
    /// оставляет хвост, и хвост этот видит заказчик, а не я.
    ///
    /// Меш же честно помнит, из какого файла приехал: у него есть путь в
    /// базе. Путь начинается с папки набора — значит объект наш клиент,
    /// как бы он ни назывался и кто бы его ни поставил.
    ///
    /// Удаляем НЕ сам объект с рендерером, а корень его префаба: у стены
    /// внутри может лежать три меша, и снос по одному оставит половину
    /// стены висеть в воздухе.
    ///
    /// Землю не трогаем: под руинами лежит общий грунт 230 на 230, и после
    /// сноса плит остаётся трава, а не дыра.
    /// </summary>
    public static class OldPurge
    {
        /// <summary>Папки, чьё содержимое сносим.</summary>
        private static readonly string[] Doomed =
        {
            "Assets/_Game/Art/KayKit/Dungeon",
            "Assets/_Game/Art/KayKit/Nature",
        };

        /// <summary>
        /// Что не сносим, даже если меш оттуда.
        ///
        /// Персонажи переезжают заменой модели, а не удалением: на них висят
        /// здоровье, бой, добыча и квесты. Снести их — значит выключить
        /// половину игры и потом собирать её заново.
        /// </summary>
        private static readonly string[] Spared =
        {
            "Assets/_Game/Art/KayKit/Characters",
        };

        [MenuItem("Tools/IsoRPG/Старый набор: снести из сцены", priority = 55)]
        public static void Purge()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            var doomed = new HashSet<GameObject>();
            var why = new Dictionary<string, int>();

            foreach (var renderer in Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string source = Source(renderer);

                if (source == null) continue;
                if (Spared.Any(s => source.StartsWith(s))) continue;
                if (!Doomed.Any(d => source.StartsWith(d))) continue;

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);

                // Не префаб — значит объект собран кодом. Тогда сносим его
                // самого, но не выше: выше может лежать общий держатель со
                // всем окружением разом.
                if (root == null) root = renderer.gameObject;

                doomed.Add(root);

                string folder = Doomed.First(d => source.StartsWith(d));
                why[folder] = why.TryGetValue(folder, out int had) ? had + 1 : 1;
            }

            foreach (var go in doomed)
                if (go != null) Object.DestroyImmediate(go);

            // Пустые держатели после сноса убираем следом: «Environment» без
            // единого ребёнка это мусор в дереве, который потом ищут глазами.
            int emptied = 0;

            foreach (var name in new[] { "Environment", "Forest", "Undergrowth", "Ruins" })
            {
                var holder = GameObject.Find(name);

                if (holder != null && holder.transform.childCount == 0)
                {
                    Object.DestroyImmediate(holder);
                    emptied++;
                }
            }

            NavBake.Rebake();

            var report = string.Join(", ", why.Select(p =>
                p.Key.Substring(p.Key.LastIndexOf('/') + 1) + ": " + p.Value));

            Debug.Log("[IsoRPG] Снесено объектов: " + doomed.Count +
                      (report.Length > 0 ? "  (" + report + ")" : "") +
                      (emptied > 0 ? ", пустых держателей " + emptied : "") +
                      ". Земля осталась — под плитами лежит общий грунт.");
        }

        /// <summary>Из какого файла приехал меш этого рендерера.</summary>
        private static string Source(Renderer renderer)
        {
            Mesh mesh = null;

            if (renderer is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
            else
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }

            if (mesh == null) return null;

            string path = AssetDatabase.GetAssetPath(mesh);

            return string.IsNullOrEmpty(path) ? null : path;
        }
    }
}
