using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Чинит деревья: непроходимым остаётся только ствол, а корни уходят в
    /// землю.
    ///
    /// Две беды, и обе от того, что дерево — большой объект неправильной
    /// формы:
    ///
    /// **Невидимые стены.** Навигация печётся по нарисованной геометрии, и
    /// в неё попадает вся крона целиком. Под гигантским деревом получается
    /// круг радиусом в десять метров, куда не ступить, — герой упирается в
    /// пустоту и не понимает, во что.
    ///
    /// **Висящие корни.** Сажать по нижней точке правильно для стены и
    /// бочки, но у этих деревьев корни расходятся вширь и вниз; посаженное
    /// «нижней точкой на землю» дерево стоит на цыпочках.
    ///
    /// Лечение: дерево исключается из выпечки целиком, а вместо него
    /// ставится препятствие по стволу — цилиндр в метр шириной. Тогда под
    /// кроной ходят, а в ствол упираются, как и должно быть. И вся модель
    /// притапливается, чтобы корни ушли в грунт.
    /// </summary>
    public static class TreeNavFix
    {
        /// <summary>На сколько притопить, метров.</summary>
        private const float Sink = 0.6f;

        [MenuItem("Tools/IsoRPG/Деревья: починить проход и посадку", priority = 65)]
        public static void Fix()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            var roots = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude,
                                                             FindObjectsSortMode.None)
                              .Where(g => g.name == "BigTrees" || g.name == "WorldBorder")
                              .ToArray();

            if (roots.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Не нашёл ни деревьев, ни края мира.");
                return;
            }

            int fixedTrees = 0;
            float thinnest = float.MaxValue;
            float thickest = 0f;

            foreach (var root in roots)
            {
                foreach (Transform child in root.transform)
                {
                    var renderers = child.GetComponentsInChildren<Renderer>()
                                         .Where(r => !(r is ParticleSystemRenderer))
                                         .ToArray();

                    if (renderers.Length == 0) continue;

                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                    // Притапливаем по ЗАМЕРУ, а не на постоянные шестьдесят
                    // сантиметров.
                    //
                    // У этих деревьев корни расходятся лапами на метры вширь
                    // и вверх, и постоянное число не годится: одному дереву
                    // шестидесяти сантиметров хватает, а у соседнего лапы
                    // так и торчат над травой. Ищем высоту, на которой ствол
                    // становится стволом, и опускаем дерево ровно на неё.
                    child.position += new Vector3(0f, -RootDepth(child, bounds), 0f);

                    // Из выпечки НЕ исключаем — навигация теперь печётся по
                    // коллайдерам, а коллайдер у дерева и есть ствол.
                    //
                    // Раньше здесь стояло ignoreFromBuild, потому что пекли
                    // по нарисованным мешам и в выпечку шла вся крона.
                    // Исключение тянуло за собой карвящее препятствие — а
                    // вместе с ним и невидимую стену, и пересчёт сотен
                    // препятствий каждый кадр. Снимаем оба.
                    var modifier = child.GetComponent<NavMeshModifier>();
                    if (modifier != null) Object.DestroyImmediate(modifier);

                    var stale = child.GetComponent<NavMeshObstacle>();
                    if (stale != null) Object.DestroyImmediate(stale);

                    // Меш-коллайдер долой, вместо него цилиндр по стволу.
                    //
                    // В префабах деревьев стоит MeshCollider по ВСЕЙ
                    // геометрии, включая каждую ветку. Герой упирался не в
                    // навигацию, а физически в крону — и обойти её нельзя,
                    // потому что ветки торчат на много метров во все
                    // стороны. У самих Synty для этого лежит папка
                    // Models/Collision с упрощёнными формами: авторы
                    // предполагали, что меш целиком в физику не пойдёт.
                    foreach (var mesh in child.GetComponentsInChildren<MeshCollider>())
                        Object.DestroyImmediate(mesh);

                    var trunk = child.GetComponent<CapsuleCollider>();
                    if (trunk == null) trunk = child.gameObject.AddComponent<CapsuleCollider>();

                    // Толщину ствола МЕРЯЕМ по геометрии, а не берём долей
                    // от габарита.
                    //
                    // Доля от кроны — это и была та самая невидимая стена:
                    // 8% от двадцатиметровой кроны упирались в мой же предел
                    // 2.5, и вокруг метрового ствола вставал цилиндр в пять
                    // метров радиусом. А коэффициент подбирать бессмысленно:
                    // у тонкой сосны и у раскидистого дуба отношение ствола к
                    // кроне разное, одно число не подходит обоим.
                    float trunkWorld = MeasureTrunk(child, bounds);

                    // Коллайдер живёт в ЛОКАЛЬНЫХ единицах, а деревья
                    // масштабированы вдвое с лишним. Это вторая половина той
                    // же ошибки: 2.5 локальных при масштабе 2 давали пять
                    // метров в мире, то есть десять поперёк.
                    float wide = Mathf.Max(child.lossyScale.x, child.lossyScale.z);
                    float tall = Mathf.Max(child.lossyScale.y, 0.01f);

                    trunk.radius = trunkWorld / Mathf.Max(wide, 0.01f);
                    trunk.height = Mathf.Clamp(bounds.size.y, 2f, 14f) / tall;
                    trunk.center = new Vector3(0f, trunk.height * 0.5f, 0f);
                    trunk.direction = 1;

                    EditorUtility.SetDirty(child.gameObject);
                    fixedTrees++;

                    thinnest = Mathf.Min(thinnest, trunkWorld);
                    thickest = Mathf.Max(thickest, trunkWorld);
                }
            }

            NavBake.Rebake();

            Debug.Log("[IsoRPG] Деревья починены: " + fixedTrees +
                      ". Стволы от " + thinnest.ToString("0.00") +
                      " до " + thickest.ToString("0.00") + " м в радиусе — " +
                      "замер по геометрии, а не доля от кроны.");
        }

        /// <summary>
        /// На сколько притопить дерево, чтобы корни ушли в грунт.
        ///
        /// Считаем не «сколько красиво», а конкретную высоту: ту, на которой
        /// дерево перестаёт быть корневой лапой и становится стволом. Идём
        /// снизу вверх тонкими поясами и меряем, насколько широко расходится
        /// геометрия. Внизу это несколько метров — корни; выше сужается до
        /// ствола. Первая высота, где ширина упала до полутора толщин ствола,
        /// и есть глубина посадки.
        ///
        /// Ограничиваем сверху: если модель такая, что «ствол» не находится
        /// вовсе, лучше притопить на метр, чем утопить дерево по крону.
        /// </summary>
        private static float RootDepth(Transform tree, Bounds bounds)
        {
            float trunk = MeasureTrunk(tree, bounds);
            float ceiling = Mathf.Min(bounds.size.y * 0.18f, 3f);

            const int Bands = 12;

            for (int i = 0; i < Bands; i++)
            {
                float low = bounds.min.y + ceiling * i / Bands;
                float high = bounds.min.y + ceiling * (i + 1) / Bands;

                float wide = Spread(tree, low, high);

                // Пояс пустой — считаем, что дошли до чистого ствола.
                if (wide <= 0f) continue;

                if (wide <= trunk * 1.5f) return Mathf.Max(low - bounds.min.y, 0.3f);
            }

            return ceiling;
        }

        /// <summary>Насколько широко расходится геометрия в поясе высот.</summary>
        private static float Spread(Transform tree, float low, float high)
        {
            var axis = new Vector2(tree.position.x, tree.position.z);
            float widest = 0f;

            foreach (var filter in tree.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                var points = mesh.vertices;

                for (int i = 0; i < points.Length; i += 3)
                {
                    Vector3 at = filter.transform.TransformPoint(points[i]);

                    if (at.y < low || at.y > high) continue;

                    float apart = Vector2.Distance(new Vector2(at.x, at.z), axis);

                    if (apart > widest) widest = apart;
                }
            }

            return widest;
        }

        /// <summary>
        /// Толщина ствола по геометрии, в метрах мира.
        ///
        /// Меряем ПОЯС на четверти высоты: ниже расходятся корни, выше
        /// начинается крона, и оба испортят замер. Берём не самую дальнюю
        /// точку, а восемьдесят пятую долю — одна выпирающая ветка не должна
        /// решать за весь ствол.
        ///
        /// Поворот вокруг вертикали на радиус не влияет, поэтому мерить можно
        /// как есть, не разворачивая дерево.
        /// </summary>
        private static float MeasureTrunk(Transform tree, Bounds bounds)
        {
            float low = bounds.min.y + bounds.size.y * 0.20f;
            float high = bounds.min.y + bounds.size.y * 0.45f;

            var axis = new Vector2(tree.position.x, tree.position.z);
            var reach = new System.Collections.Generic.List<float>();

            foreach (var filter in tree.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                var points = mesh.vertices;

                // Через одну: для оценки радиуса густота не нужна, а деревьев
                // в сцене больше пятисот.
                for (int i = 0; i < points.Length; i += 2)
                {
                    Vector3 at = filter.transform.TransformPoint(points[i]);

                    if (at.y < low || at.y > high) continue;

                    reach.Add(Vector2.Distance(new Vector2(at.x, at.z), axis));
                }
            }

            if (reach.Count == 0)
            {
                // Пояс пустой — дерево странной формы. Берём осторожную
                // оценку, но не молчим: молчаливая подстановка числа и есть
                // то, с чего началась невидимая стена.
                Debug.LogWarning("[IsoRPG] У " + tree.name +
                                 " не нашлось ствола в поясе — радиус взят 0.6 м.");
                return 0.6f;
            }

            reach.Sort();

            float measured = reach[Mathf.Clamp(
                Mathf.RoundToInt((reach.Count - 1) * 0.85f), 0, reach.Count - 1)];

            // Нижний предел — чтобы в тонкую осину можно было упереться,
            // верхний — чтобы ошибка замера не выросла в стену.
            return Mathf.Clamp(measured, 0.25f, 1.6f);
        }
    }
}
