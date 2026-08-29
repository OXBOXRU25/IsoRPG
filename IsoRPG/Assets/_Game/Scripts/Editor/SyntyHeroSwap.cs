using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит игроку выбранного героя Synty.
    ///
    /// Номер берётся из файла-подсказки рядом со сценой, а не зашит в код:
    /// выбор меняется чаще, чем правится код, и пересобирать проект ради
    /// цифры незачем.
    ///
    /// Почему тут не будет вчерашних бед. Персонажи и анимации из одного
    /// набора Synty, скелет общий — ретаргета нет вовсе. Значит нет ни
    /// несовпадения типа рига, ни смещения корня, ни разъезжающихся
    /// пропорций. Остаётся ровно одно правило, которое всё равно надо
    /// соблюсти: аниматор вешаем НА модель, а не на родителя, иначе аватар не
    /// найдёт своих костей и персонаж замрёт без единой ошибки.
    /// </summary>
    public static class SyntyHeroSwap
    {
        private const string Presets =
            "Assets/PolygonFantasyHeroCharacters/Prefabs/Characters_Presets";

        private const string Controller =
            "Assets/_Game/Art/Animations/Controllers/AC_Rogue.controller";

        private const string VisualName = "Визуал: герой Synty";

        /// <summary>Файл с номером выбранного героя.</summary>
        private const string Choice = "Assets/_Game/Scenes/hero-number.txt";

        [MenuItem("Tools/IsoRPG/Игрок: поставить героя Synty по номеру", priority = 35)]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            // В файле-подсказке либо номер пресета Polygon, либо имя
            // префаба целиком — с приходом Sidekick героев стало два
            // набора, а строка выбора должна остаться одна.
            string choice = System.IO.File.Exists(Choice)
                ? System.IO.File.ReadAllText(Choice).Trim()
                : "23";

            if (choice.Length == 0) choice = "23";

            GameObject prefab;
            string what;

            if (int.TryParse(choice, out int number))
            {
                what = "герой номер " + number;
                string path = Presets + "/Chr_FantasyHero_Preset_" + number + ".prefab";
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            else
            {
                what = "герой " + choice;

                var guid = AssetDatabase.FindAssets(choice + " t:Prefab").FirstOrDefault();

                prefab = guid == null
                    ? null
                    : AssetDatabase.LoadAssetAtPath<GameObject>(
                        AssetDatabase.GUIDToAssetPath(guid));
            }

            if (prefab == null)
            {
                Debug.LogWarning("[IsoRPG] Не найден " + what);
                return;
            }

            var player = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                               .FirstOrDefault(g => g.name == "Player");

            if (player == null)
            {
                Debug.LogWarning("[IsoRPG] Игрока в сцене нет.");
                return;
            }

            // Снимаем прежнее: капсулу и предыдущего героя.
            foreach (var old in player.GetComponentsInChildren<Transform>(true)
                                      .Where(t => t != null &&
                                                  (t.name == VisualName ||
                                                   t.name == "Капсула вместо героя" ||
                                                   t.name == "Визуал: Stylized Hero"))
                                      .Select(t => t.gameObject)
                                      .ToList())
            {
                Object.DestroyImmediate(old);
            }

            // Прежнего героя УДАЛЯЕМ, а не гасим.
            //
            // Здесь стояло `r.enabled = false`. Погашенный рендерер — это
            // объект, который остался в сцене и уехал в сборку целиком, с
            // мешами и текстурами; он просто не рисуется, пока его кто-то
            // не включит. А включить есть кому: подсветка силуэтов сквозь
            // препятствия перебирает рендереры героя, LOD-группа зажигает
            // свои уровни по дальности камеры. Отсюда и «старый герой
            // периодически вылезает» — четыре раза подряд, и каждый раз я
            // гасил его заново вместо того, чтобы убрать.
            //
            // Правило записано в STATUS проекта дословно: выключить объект
            // недостаточно, он остаётся в файле и создаётся при загрузке.
            //
            // Новый визуал создаётся ниже, поэтому здесь под удаление
            // попадает только старое.
            int removed = 0;

            foreach (var r in player.GetComponentsInChildren<Renderer>(true).ToList())
            {
                if (r == null) continue;

                Object.DestroyImmediate(r.gameObject);
                removed++;
            }

            // LOD-группа осталась бы без уровней и всё равно пыталась их
            // включать.
            foreach (var lod in player.GetComponentsInChildren<LODGroup>(true).ToList())
            {
                if (lod != null) Object.DestroyImmediate(lod);
            }

            int hidden = removed;

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.name = VisualName;
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Аниматор снимаем со всего игрока и ставим заново на модель.
            foreach (var a in player.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(a);

            var animator = visual.AddComponent<Animator>();

            animator.avatar = FindAvatar(prefab);
            animator.applyRootMotion = false;   // двигает навигация
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);

            // Перенацеливаем пустые ссылки на аниматор в наших компонентах:
            // иначе они держат ссылку на удалённый и молча перестают работать.
            int rebound = 0;

            foreach (var mb in player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;

                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                bool changed = false;

                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue != null) continue;
                    if (!it.name.ToLower().Contains("animator")) continue;

                    it.objectReferenceValue = animator;
                    changed = true;
                }

                if (changed) { so.ApplyModifiedProperties(); rebound++; }
            }

            Debug.Log("[IsoRPG] Игрок стал: " + what + " (" + prefab.name +
                      "), аватар " + (animator.avatar != null
                          ? animator.avatar.name + ", human " + animator.avatar.isHuman
                          : "НЕТ") +
                      ", контроллер " + (animator.runtimeAnimatorController != null
                          ? animator.runtimeAnimatorController.name : "НЕТ") +
                      ", УДАЛЕНО прежних мешей " + hidden +
                      ", ссылок перенацелено " + rebound + ".");

            MarkDirty();
        }

        /// <summary>Аватар из модели, на которой построен префаб.</summary>
        private static Avatar FindAvatar(GameObject prefab)
        {
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;

                string meshPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (string.IsNullOrEmpty(meshPath)) continue;

                var a = AssetDatabase.LoadAllAssetsAtPath(meshPath)
                                     .OfType<Avatar>()
                                     .FirstOrDefault();

                if (a != null) return a;
            }

            // Второй заход: аватар рядом с мешем, а не внутри него.
            //
            // У Polygon модель — это FBX, и аватар лежит подобъектом внутри
            // него, первый проход его находит. У Sidekick меш и аватар —
            // два отдельных ассета в одной папке (Starter_03.asset и
            // Starter_03-avatar.asset), и первый проход возвращает пусто.
            // Без аватара humanoid-клипы не играют вовсе, персонаж встаёт
            // буквой «Т» — молча, без единой строчки в журнале.
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;

                string meshPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (string.IsNullOrEmpty(meshPath)) continue;

                string folder = System.IO.Path.GetDirectoryName(meshPath).Replace('\\', '/');
                if (string.IsNullOrEmpty(folder)) continue;

                foreach (var guid in AssetDatabase.FindAssets("t:Avatar", new[] { folder }))
                {
                    var a = AssetDatabase.LoadAssetAtPath<Avatar>(
                        AssetDatabase.GUIDToAssetPath(guid));

                    if (a != null) return a;
                }
            }

            return null;
        }

        private static void MarkDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
