using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Вытаскивает из демо-сцен Synty готовые постройки и сохраняет их
    /// нашими префабами.
    ///
    /// Зачем. Я строил дом из стен — стена, стена, проём, — и получилась
    /// коробка. А у набора в демо-сцене лежит `Preset_Buildings_Group`: дома
    /// собраны целиком, с крышами, балконами, трубами и навесами, ровно те,
    /// что на промо-картинках. Класть кирпичи, когда рядом стоит готовый
    /// дом, — не бережливость, а потеря дня.
    ///
    /// Отсюда же берутся «обстановки» (`*_Dressing`) — готовые наборы
    /// реквизита, которыми авторы одевают сцену.
    ///
    /// Что делает: открывает демо-сцену набора, сохраняет каждую постройку
    /// префабом в наш проект и возвращает сцену, которая была открыта.
    /// Ничего в чужих файлах не меняет.
    /// </summary>
    public static class SyntyBuildings
    {
        private const string Target = "Assets/_Game/Prefabs/Synty";

        /// <summary>
        /// Откуда тащим. Порядок важен: сперва здания, потом обстановки —
        /// так удобнее смотреть получившийся список.
        /// </summary>
        private static readonly (string scene, string[] groups)[] Sources =
        {
            ("Assets/Synty/PolygonFantasyKingdom/Scenes/Demo.unity",
             new[] { "Preset_Buildings_Group", "Preset_Buildings_Optimized",
                     "Castle_Exterior_Dressing", "Castle_Interior_Dressing" }),
        };

        [MenuItem("Tools/IsoRPG/Вытащить здания Synty в префабы", priority = 54)]
        public static void Extract()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "Открытие сцен в режиме Play не работает.", "Понятно");
                return;
            }

            // Запоминаем, где были. Возврат обязателен: человек открывал
            // демо посмотреть, а не переезжать в неё.
            string current = SceneManager.GetActiveScene().path;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[IsoRPG] Отменено.");
                return;
            }

            Directory.CreateDirectory(Target);

            int saved = 0, skipped = 0;
            var names = new HashSet<string>();

            foreach (var (scenePath, groups) in Sources)
            {
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning("[IsoRPG] Нет сцены " + scenePath);
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                foreach (string groupName in groups)
                {
                    var group = scene.GetRootGameObjects()
                                     .FirstOrDefault(g => g.name == groupName);

                    if (group == null)
                    {
                        Debug.LogWarning("[IsoRPG] В сцене нет группы " + groupName);
                        continue;
                    }

                    foreach (Transform child in group.transform)
                    {
                        // Пустышки-контейнеры не нужны: сохраняем только то,
                        // что видно.
                        if (child.GetComponentsInChildren<Renderer>().Length == 0)
                        {
                            skipped++;
                            continue;
                        }

                        string name = Safe(child.name);

                        // Имена в демо повторяются («House_01» в трёх местах);
                        // без разведения второй перезаписал бы первый, и
                        // половина домов пропала бы молча.
                        string unique = name;
                        int n = 2;
                        while (!names.Add(unique)) unique = name + "_" + n++;

                        string path = Target + "/" + unique + ".prefab";

                        PrefabUtility.SaveAsPrefabAsset(child.gameObject, path, out bool ok);

                        if (ok) saved++;
                        else Debug.LogWarning("[IsoRPG] Не сохранилось: " + child.name);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Возвращаемся туда, где были.
            if (!string.IsNullOrEmpty(current) && File.Exists(current))
                EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
            else
                Debug.LogWarning("[IsoRPG] Прежняя сцена неизвестна — открой Sandbox вручную.");

            Debug.Log("[IsoRPG] Вытащено построек: " + saved +
                      (skipped > 0 ? ", пропущено пустых " + skipped : "") +
                      ". Лежат в " + Target);
        }

        private static string Safe(string name)
        {
            foreach (char bad in Path.GetInvalidFileNameChars())
                name = name.Replace(bad, '_');

            return name.Replace(' ', '_');
        }
    }
}
