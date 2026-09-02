using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Считает доворот кинжала в руке по замерам из Blender — и не через углы.
    ///
    /// Числа примерки пришли из соседнего чата (02.09.2026): матрица кинжала
    /// в системе кости `hand_r`. Прошлый перенос делался покомпонентной
    /// перестановкой углов Эйлера, и именно он дал «повёрнут не в ту сторону»:
    /// у Blender вертикаль Z и порядок осей XYZ, у Unity вертикаль Y и ZXY —
    /// перестановка компонент НЕ равна повороту.
    ///
    /// Здесь берём из матрицы не углы, а два направления: куда смотрит клинок
    /// и куда смотрит гарда — оба в системе кости. Направление переносится
    /// перестановкой честно, это просто вектор. А оси самой модели меряем уже
    /// в Unity, по её мешу: тогда разница между импортёрами Blender и Unity в
    /// расчёт вообще не входит.
    ///
    /// Остаётся один допуск: что кость `hand_r` в Unity и в Blender развёрнута
    /// одинаково. Если нет — доворот уедет на прямой угол, и это видно на
    /// первом же кадре щупа `grip-probe`, который ставит рядом соседние
    /// варианты.
    /// </summary>
    public static class GripFit
    {
        /// <summary>Кинжал, который примеряли и утвердили. НЕ _01 — тот был в первом ряду сравнения.</summary>
        public const string DaggerPath =
            "Assets/Synty/PolygonFantasyKingdom/Prefabs/Weapons/SM_Wep_Dagger_02.prefab";

        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        /// <summary>
        /// Смещение кинжала в кости, метры.
        ///
        /// Blender даёт (-0.090375, 0.025936, 0.006005) в своей системе; у него
        /// вертикаль Z, у нас Y, поэтому вторая и третья координаты меняются
        /// местами. Для точки перестановка законна — это просто три числа.
        /// </summary>
        public static readonly Vector3 Grip = new Vector3(-0.090375f, 0.006005f, 0.025936f);

        /// <summary>
        /// Куда смотрит клинок, в системе кости. Взято из матрицы Blender:
        /// это её второй столбец — образ локальной оси Y модели, а клинок у
        /// кинжалов Synty идёт как раз вдоль Y (габарит 0.712 м против 0.186
        /// у гарды и 0.049 у толщины).
        /// </summary>
        private static readonly Vector3 BladeInBone = ToUnity(new Vector3(-0.1109f, 0.0349f, -0.9932f));

        /// <summary>Куда смотрит гарда — третий столбец той же матрицы, образ оси Z модели.</summary>
        private static readonly Vector3 GuardInBone = ToUnity(new Vector3(0.9923f, -0.0520f, -0.1127f));

        /// <summary>Посчитанный доворот. Читает щуп, чтобы поставить его серединой ряда.</summary>
        public static Vector3 Fitted { get; private set; } = new Vector3(-96.5f, -93.2f, -1.7f);

        /// <summary>То же для левой руки — герой дерётся парой клинков.</summary>
        public static Vector3 GripLeft { get; private set; }

        public static Vector3 FittedLeft { get; private set; }

        [MenuItem("Tools/IsoRPG/Оружие: пересчитать хват из Blender", priority = 46)]
        public static void Apply()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DaggerPath);

            if (prefab == null)
            {
                Debug.LogError("[IsoRPG] Не найден кинжал " + DaggerPath);
                return;
            }

            // --- оси модели, померенные в Unity -----------------------------
            if (!ModelAxes(prefab, out var bladeLocal, out var guardLocal, out var size))
            {
                Debug.LogError("[IsoRPG] У кинжала не нашлось меша — оси не померить.");
                return;
            }

            // --- поворот, переводящий оси модели в направления из Blender ----
            //
            // Через две пары «вперёд + вверх», а не через углы: так порядок
            // осей вообще не участвует, и ошибиться в нём негде.
            var from = Quaternion.LookRotation(bladeLocal, guardLocal);
            var to = Quaternion.LookRotation(BladeInBone, GuardInBone);

            var rotation = to * Quaternion.Inverse(from);

            Fitted = rotation.eulerAngles;

            // --- левая рука --------------------------------------------------
            //
            // Герой дерётся парой клинков — второй виден ровно столько же.
            // Простым копированием локального трансформа зеркалить нельзя:
            // в Blender так кинжал уезжал на 23 см вверх. Нужно ОТРАЖЕНИЕ:
            // MIRROR @ local @ MIRROR, где MIRROR = diag(-1, 1, 1).
            //
            // Для точки это смена знака у X. Для поворота — отражение оси
            // вращения: у кватерниона (x, y, z, w) меняются знаки y и z.
            // Отражение дважды даёт снова честный поворот, а не зеркало, —
            // поэтому модель клинка остаётся той же, не зеркальной.
            GripLeft = new Vector3(-Grip.x, Grip.y, Grip.z);

            var mirrored = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
            FittedLeft = mirrored.eulerAngles;

            Debug.Log($"[IsoRPG] Хват пересчитан по матрице Blender.\n" +
                      $"  модель: {System.IO.Path.GetFileNameWithoutExtension(DaggerPath)}, " +
                      $"габарит {size.x:0.000} x {size.y:0.000} x {size.z:0.000} м\n" +
                      $"  клинок в модели: {bladeLocal}, гарда: {guardLocal}\n" +
                      $"  клинок в кости (Unity): {BladeInBone}, гарда: {GuardInBone}\n" +
                      $"  ДОВОРОТ правой: {Fitted.x:0.0} / {Fitted.y:0.0} / {Fitted.z:0.0}\n" +
                      $"  СМЕЩЕНИЕ правой: {Grip.x:0.0000} / {Grip.y:0.0000} / {Grip.z:0.0000}\n" +
                      $"  ДОВОРОТ левой: {FittedLeft.x:0.0} / {FittedLeft.y:0.0} / {FittedLeft.z:0.0}\n" +
                      $"  СМЕЩЕНИЕ левой: {GripLeft.x:0.0000} / {GripLeft.y:0.0000} / {GripLeft.z:0.0000}\n" +
                      $"  было: -96.5 / -93.2 / -1.7 на обе руки (перенос перестановкой углов — он и врал)");

            ApplyToScene();
            ApplyToItems(prefab);
        }

        /// <summary>
        /// Поставить числа герою в сцене.
        ///
        /// Обязательно: значение, заданное и в коде, и в сцене, работает ИЗ
        /// СЦЕНЫ — правка кода не догоняет уже расставленный компонент.
        /// </summary>
        private static void ApplyToScene()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            int done = 0;

            foreach (var visual in Object.FindObjectsByType<IsoRPG.Items.WeaponVisual>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                visual.SetGrip(Grip, Fitted, GripLeft, FittedLeft);
                EditorUtility.SetDirty(visual);
                done++;
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[IsoRPG] Хват проставлен в сцене: компонентов {done}.");
        }

        /// <summary>
        /// Перевести кинжалы в каталоге на утверждённую модель.
        ///
        /// В игре стоял `SM_Wep_Dagger_01` — он был в первом сравнительном
        /// ряду и остался значением по умолчанию. Примеряли и утверждали
        /// `_02`, и числа хвата сняты с него: у моделей разная опора, и под
        /// чужую они не сядут никогда, сколько ни крути углы.
        /// </summary>
        private static void ApplyToItems(GameObject dagger)
        {
            int changed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(IsoRPG.Items.ItemDefinition)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<IsoRPG.Items.ItemDefinition>(path);

                if (item == null || item.worldModel == null) continue;

                // По слову «Dagger» в имени модели, а не по конкретному
                // файлу: в каталоге лежало ТРЕТЬЕ имя — `SM_Prop_Dagger_01`
                // из набора персонажей, не из набора оружия. Отбор по одному
                // known-имени его бы и дальше не замечал.
                if (!item.worldModel.name.Contains("Dagger")) continue;
                if (item.worldModel == dagger) continue;

                Debug.Log($"[IsoRPG] {item.name}: модель {item.worldModel.name} → {dagger.name}");

                item.worldModel = dagger;
                EditorUtility.SetDirty(item);
                changed++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[IsoRPG] Кинжалов переведено на утверждённую модель: {changed}.");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Оси модели по её же мешу: самый длинный габарит — клинок, второй —
        /// гарда.
        ///
        /// Меряем в Unity, а не берём из Blender, ровно затем, чтобы разница
        /// между импортёрами не попала в расчёт. Знак берём по тому, в какую
        /// сторону от опоры меш уходит дальше: опора у кинжалов Synty стоит в
        /// точке хвата, то есть внутри длины, и клинок торчит в одну сторону.
        /// </summary>
        private static bool ModelAxes(GameObject prefab, out Vector3 blade, out Vector3 guard,
                                      out Vector3 size)
        {
            blade = Vector3.forward;
            guard = Vector3.up;
            size = Vector3.zero;

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true)
                                .Where(f => f.sharedMesh != null)
                                .ToArray();

            if (filters.Length == 0) return false;

            bool started = false;
            var box = new Bounds();

            foreach (var filter in filters)
            {
                var local = filter.sharedMesh.bounds;

                // В систему корня префаба: у Synty меш обычно лежит прямо на
                // корне, но проверять это на глаз не надо.
                var toRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var centre = toRoot.MultiplyPoint3x4(local.center);
                var extent = toRoot.MultiplyVector(local.extents);

                var part = new Bounds(centre, new Vector3(
                    Mathf.Abs(extent.x) * 2f, Mathf.Abs(extent.y) * 2f, Mathf.Abs(extent.z) * 2f));

                if (!started) { box = part; started = true; }
                else box.Encapsulate(part);
            }

            size = box.size;

            int longest = Longest(size, -1);
            int second = Longest(size, longest);

            blade = Axis(longest, Sign(box, longest));
            guard = Axis(second, Sign(box, second));

            return true;
        }

        private static int Longest(Vector3 size, int skip)
        {
            int best = -1;
            float value = -1f;

            for (int i = 0; i < 3; i++)
            {
                if (i == skip) continue;
                if (size[i] <= value) continue;

                value = size[i];
                best = i;
            }

            return best;
        }

        /// <summary>В какую сторону от опоры меш уходит дальше.</summary>
        private static float Sign(Bounds box, int axis)
        {
            return Mathf.Abs(box.max[axis]) >= Mathf.Abs(box.min[axis]) ? 1f : -1f;
        }

        private static Vector3 Axis(int index, float sign)
        {
            var v = Vector3.zero;
            v[index] = sign;
            return v;
        }

        /// <summary>
        /// Направление из системы Blender в нашу: у него вертикаль Z, у нас Y.
        /// Для направления это законная перестановка — в отличие от углов.
        /// </summary>
        private static Vector3 ToUnity(Vector3 blender) =>
            new Vector3(blender.x, blender.z, blender.y).normalized;
    }
}
