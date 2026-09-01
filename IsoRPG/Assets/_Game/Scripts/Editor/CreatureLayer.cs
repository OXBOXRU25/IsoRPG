using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Переводит ВСЕ живые тела сцены на слой «Characters».
    ///
    /// Зачем. Камера отбирает препятствия слоем: живое тело не должно
    /// закрывать обзор. Павлон 01.09.2026: «прохожу через вторую лошадь —
    /// камера резко делает полный зум». Камера не ошибалась: между героем и
    /// ею оказался коллайдер лошади, и она придвинулась, как придвинулась бы
    /// к стволу дерева.
    ///
    /// Почему слоем, а не перебором в игре. Это ММО: проверка препятствий
    /// идёт каждый кадр у каждого игрока, и перебирать в ней коллайдеры
    /// нельзя — 01.09.2026 такой перебор уже уронил игру в «приложение не
    /// отвечает». Слой физика читает даром.
    ///
    /// Признак живого выбран так, чтобы накрыть ВЕСЬ класс, а не тех, кто
    /// попался под руку (на этом уже обожглись: отбор по навигационному
    /// агенту пропустил лошадь-декорацию, которая агента не носит):
    ///
    ///   живое = навигационный агент ИЛИ скелетная сетка в ветке.
    ///
    /// Скелет есть у каждого существа и не бывает у камня, забора и дерева.
    /// Тот же признак использует <c>PlayerMotor</c>, когда решает, сквозь что
    /// пропускать героя, — один словарь на весь проект.
    ///
    /// Трогаем только объекты со слоем Default: полоски здоровья над головой,
    /// небо и вода уже сидят на своих слоях, и переносить их незачем.
    /// </summary>
    public static class CreatureLayer
    {
        private const string LayerName = "Characters";

        /// <summary>Боевая сцена. Задание открывает её САМО — см. комментарий в Apply.</summary>
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        [MenuItem("Tools/IsoRPG/Существа: перевести на слой Characters", priority = 37)]
        public static void Apply()
        {
            // Открываем боевую сцену сами, а не работаем по той, что оказалась
            // открытой.
            //
            // 01.09.2026 первый прогон покрасил старую `Arena.unity` — она
            // осталась открытой с прошлого раза, — и отчитался «тел 4, без
            // слоя никого». Отчёт был честный: в ТОЙ сцене живых и правда
            // четверо. В боевую сцену не попало ничего, а сборка её открыла
            // сама и собрала непокрашенной. Проверка была исправна, пустым
            // было множество.
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            int layer = LayerMask.NameToLayer(LayerName);

            if (layer < 0)
            {
                Debug.LogError("[IsoRPG] В проекте нет слоя «" + LayerName + "» — задание не выполнено.");
                return;
            }

            var bodies = FindBodies();

            int objects = 0;

            foreach (var body in bodies)
            {
                objects += Paint(body.transform, layer);
                EditorUtility.SetDirty(body);
            }

            EditorSceneManager.SaveOpenScenes();

            // Щуп: читаем, что получилось, а не верим отчёту. Сюда же ловятся
            // существа, до которых задание не добралось.
            var missed = FindBodies()
                         .Where(b => b.layer != layer)
                         .Select(b => b.name)
                         .ToArray();

            Debug.Log(
                $"[IsoRPG] Слой существ: тел {bodies.Count}, покрашено объектов {objects}.\n" +
                $"  тела: {string.Join(", ", bodies.Take(30).Select(b => b.name))}\n" +
                $"  без слоя осталось: {(missed.Length == 0 ? "никого" : string.Join(", ", missed.Take(10)))}");
        }

        /// <summary>
        /// Все живые тела сцены. Корень тела — самый верхний предок, который
        /// ещё принадлежит существу: у собранного персонажа скелет лежит на
        /// пару уровней ниже агента и наклонного узла.
        /// </summary>
        private static List<GameObject> FindBodies()
        {
            var found = new HashSet<GameObject>();

            foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (agent != null) found.Add(agent.gameObject);
            }

            foreach (var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (skin == null) continue;

                var owner = Owner(skin.transform);
                if (owner != null) found.Add(owner);
            }

            // Вложенные тела убираем: если корень уже в списке, ветка ниже
            // покрасится вместе с ним, и второй проход только путал бы счёт.
            var roots = found.Where(go => go != null && !HasAncestorIn(go, found)).ToList();

            Debug.Log($"[IsoRPG] Живых найдено {found.Count}, из них корней {roots.Count}.");

            return roots;
        }

        /// <summary>
        /// Кому принадлежит скелетная сетка: поднимаемся до верхнего предка,
        /// который несёт признак существа. Без подъёма красился бы только
        /// меш, а коллайдер существа обычно висит выше.
        /// </summary>
        private static GameObject Owner(Transform skin)
        {
            GameObject owner = skin.gameObject;

            for (Transform t = skin; t != null; t = t.parent)
            {
                bool creature = t.GetComponent<NavMeshAgent>() != null
                                || t.GetComponent<Animator>() != null
                                || t.GetComponent<CharacterController>() != null
                                || t.GetComponent<IsoRPG.Combat.Health>() != null
                                || t.GetComponent<IsoRPG.Combat.Targetable>() != null;

                if (creature) owner = t.gameObject;
            }

            return owner;
        }

        private static bool HasAncestorIn(GameObject go, HashSet<GameObject> set)
        {
            for (Transform t = go.transform.parent; t != null; t = t.parent)
                if (set.Contains(t.gameObject)) return true;

            return false;
        }

        /// <summary>
        /// Красит ветку целиком.
        ///
        /// Сначала здесь стояло «трогаем только слой Default» — и щуп
        /// `cam-block` показал, чем это кончилось: у каждого существа висит
        /// капсула `Head` из набора Malbers на своём, безымянном слое, и
        /// двадцать таких голов остались препятствием для камеры. Мимо
        /// прошло ровно потому, что условие было про исходный слой, а не про
        /// принадлежность существу.
        ///
        /// Теперь красим всё, кроме слоёв, у которых слой — часть смысла:
        /// интерфейс, небо и вода. Всё прочее внутри ветки существа — это
        /// тело существа, где бы его ни разместил автор набора.
        /// </summary>
        private static int Paint(Transform root, int layer)
        {
            int painted = 0;

            if (!Keep(root.gameObject.layer))
            {
                root.gameObject.layer = layer;
                painted++;
            }

            foreach (Transform child in root)
                painted += Paint(child, layer);

            return painted;
        }

        /// <summary>Слои, которые несут смысл сами по себе и переносу не подлежат.</summary>
        private static bool Keep(int layer)
        {
            return layer == LayerMask.NameToLayer("UI")
                   || layer == LayerMask.NameToLayer("Sky")
                   || layer == LayerMask.NameToLayer("Water")
                   || layer == LayerMask.NameToLayer("TransparentFX");
        }
    }
}
