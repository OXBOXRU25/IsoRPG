using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит вместо игрока голую капсулу.
    ///
    /// Нужна, когда персонаж мешает смотреть на всё остальное: пока идёт
    /// перебор моделей, каждая тянет за собой свой риг, свои анимации и свои
    /// поломки, а вопрос стоит про мир, а не про героя. Капсула ничего этого
    /// не тянет и не врёт: она всегда стоит там, где на самом деле находится
    /// точка игрока.
    ///
    /// Аниматор при этом снимаем совсем. Оставленный с чужим аватаром, он
    /// продолжает дёргать несуществующие кости и пишет предупреждения в
    /// журнал — а нам нужен чистый кадр.
    /// </summary>
    public static class PlayerCapsule
    {
        private const string Name = "Капсула вместо героя";

        /// <summary>Рост капсулы — прежний рост нашего героя.</summary>
        private const float Height = 1.9f;

        private const float Radius = 0.35f;

        [MenuItem("Tools/IsoRPG/Игрок: заменить на капсулу", priority = 33)]
        public static void Apply()
        {
            var player = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                               .FirstOrDefault(g => g.name == "Player");

            if (player == null)
            {
                Debug.LogWarning("[IsoRPG] Игрока в сцене нет.");
                return;
            }

            Remove(silent: true);

            // Гасим ВСЁ, что рисуется: и тело, и экипировку, и то, что
            // навешано в рантайме на кости.
            int hidden = 0;

            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                r.enabled = false;
                hidden++;
            }

            // Аниматор снимаем: без него не будет ни поз, ни предупреждений
            // про пропавшие кости.
            int animators = 0;

            foreach (var a in player.GetComponentsInChildren<Animator>(true))
            {
                Object.DestroyImmediate(a);
                animators++;
            }

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = Name;
            capsule.transform.SetParent(player.transform, false);

            // Примитив-капсула имеет высоту два метра при масштабе единица и
            // точку отсчёта в середине. Значит масштабируем по нужной высоте и
            // поднимаем на половину — тогда низ капсулы окажется ровно в точке
            // игрока, как и подошвы у модели.
            capsule.transform.localScale = new Vector3(Radius * 2f, Height / 2f, Radius * 2f);
            capsule.transform.localPosition = new Vector3(0f, Height / 2f, 0f);

            // Коллайдер примитива убираем: физику игрока ведёт агент
            // навигации, а лишняя капсула-коллайдер начнёт цеплять всё вокруг.
            var collider = capsule.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader != null)
            {
                const string path = "Assets/_Game/Art/Materials/M_PlayerCapsule.mat";

                AssetDatabase.DeleteAsset(path);

                var material = new Material(shader);
                material.color = new Color(0.85f, 0.30f, 0.25f);
                material.SetFloat("_Smoothness", 0.1f);

                AssetDatabase.CreateAsset(material, path);
                capsule.GetComponent<Renderer>().sharedMaterial = material;
            }

            Debug.Log("[IsoRPG] Игрок заменён на капсулу " + Height + " м. " +
                      "Погашено мешей " + hidden + ", снято аниматоров " + animators + ".");

            MarkDirty();
        }

        [MenuItem("Tools/IsoRPG/Игрок: убрать капсулу", priority = 34)]
        public static void RemoveMenu() => Remove(silent: false);

        private static void Remove(bool silent)
        {
            var found = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                              .Where(g => g.name == Name)
                              .ToList();

            foreach (var g in found) Object.DestroyImmediate(g);

            if (!silent)
            {
                Debug.Log("[IsoRPG] Капсул убрано: " + found.Count +
                          ". Меши и аниматор придётся вернуть отдельно — " +
                          "капсула их не хранит.");
                MarkDirty();
            }
        }

        private static void MarkDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
