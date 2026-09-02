using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace IsoRPG.Cameras
{
    /// <summary>
    /// Подводный вид: когда камера опускается ниже глади.
    ///
    /// Гладь — плоскость, у которой грани смотрят вверх; из-под воды видна
    /// её изнанка, а изнанка не рисуется. Двусторонний материал возвращает
    /// поверхность в кадр, но у плоскости всё равно нет толщины: камера у
    /// самой кромки смотрит на неё РЕБРОМ и видит тонкую полоску там, где
    /// должна быть толща воды, а весь остальной кадр — суша и небо —
    /// остаётся нетронутым. Тумана сцены (`RenderSettings.fog`) для показа
    /// глубины тоже мало: у воды на пруду он либо неразличим на близких
    /// дистанциях, либо успевает мигнуть и погаснуть за то же мгновение,
    /// что и порог погружения.
    ///
    /// Поэтому вместо тумана — плоская заливка экрана цветом воды поверх
    /// всего, что нарисовано (кроме интерфейса): персонажа видно, но через
    /// синеву, ровно как в WoW. Полоска от плоскости при этом просто
    /// тонет в той же заливке и не бросается в глаза.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class Underwater : MonoBehaviour
    {
        [Tooltip("Цвет воды — он же цвет заливки экрана под водой.")]
        [SerializeField] private Color tint = new Color(0.10f, 0.42f, 0.44f);

        [Tooltip("Непрозрачность заливки под водой: 0 — не видно, 1 — сплошной цвет.")]
        [SerializeField] private float alpha = 0.5f;

        [Tooltip("Насколько глубже глади должна опуститься камера, чтобы погружение НАЧАЛОСЬ, метры.")]
        [SerializeField] private float diveThreshold = 0.05f;

        [Tooltip("Насколько выше глади должна подняться камера, чтобы всплытие СЧИТАЛОСЬ окончательным, метры. Больше порога погружения нарочно — гасит дребезг у самой кромки: без разницы между порогами камера у границы ныряла и всплывала по многу раз в секунду.")]
        [SerializeField] private float surfaceThreshold = 0.35f;

        [Tooltip("По какому куску имени материала узнаём воду.")]
        [SerializeField] private string waterMaterial = "Water_Lake";

        private Renderer[] water;
        private Camera eye;
        private bool submerged;
        private Image overlay;

        private void Awake()
        {
            eye = GetComponent<Camera>();
            Collect();
            BuildOverlay();

            Debug.Log("[IsoRPG] Подводный вид включён на камере «" + name +
                      "», водоёмов при старте " + (water != null ? water.Length : 0));
        }

        /// <summary>
        /// Заливка экрана — своим Canvas, а не через существующий HUD:
        /// компонент должен работать даже если HUD ещё не собран или собран
        /// иначе. Порядок сортировки нарочно отрицательный: заливка ложится
        /// НИЖЕ любого интерфейса (полоски здоровья, миникарты, окон) — они
        /// начинаются от 5 и выше, — иначе синева перекрасила бы и HUD.
        /// </summary>
        private void BuildOverlay()
        {
            var canvasGo = new GameObject("Подводная заливка", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;

            var imgGo = new GameObject("Заливка", typeof(Image));
            imgGo.transform.SetParent(canvasGo.transform, false);

            overlay = imgGo.GetComponent<Image>();
            overlay.raycastTarget = false;
            overlay.color = new Color(tint.r, tint.g, tint.b, 0f);

            var rt = overlay.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Собрать водоёмы сцены.
        ///
        /// По материалу, а не по имени объекта: у автора набора гладь
        /// называется то Lake, то Pond, то вовсе никак, и отбор по слову в
        /// названии уже подводил — красил мельничное колесо и пропускал
        /// водоём. Материал же у всей воды один.
        /// </summary>
        private void Collect()
        {
            water = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,
                                                    FindObjectsSortMode.None)
                    .Where(r => IsWater(r.sharedMaterial))
                    .ToArray();
        }

        /// <summary>
        /// Вода это или нет.
        ///
        /// Главный признак — ШЕЙДЕР, а не имя. У автора вода трёх видов:
        /// озеро, река и базовая, — и по имени «Water_Lake» ловилось только
        /// озеро. Пока река была залита нашим материалом, это сходило с рук;
        /// стоило вернуть автору его воду, и река перестала бы считаться
        /// водой вовсе — то есть в ручье можно было бы утонуть без единого
        /// признака погружения. Шейдер же у всей авторской воды один.
        ///
        /// Имя остаётся запасным путём: наш собственный материал воды сидит
        /// на штатном URP-шейдере, по которому его не отличить от камня.
        /// </summary>
        private bool IsWater(Material material)
        {
            if (material == null) return false;

            if (material.shader != null &&
                material.shader.name.IndexOf("WaterShader", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return material.name.Contains(waterMaterial);
        }

        /// <summary>
        /// Высота глади прямо над точкой — по плоскости самого объекта.
        ///
        /// Раньше здесь стоял верх габаритной коробки, и для пруда это было
        /// верно: горизонтальная плоскость, верх коробки и есть уровень воды.
        /// Ручей же уложен под наклон, и его коробка — 24.9 x 3.6 x 24.5 м:
        /// «верх» у неё на 8.67 м, то есть на верхнем конце русла, тремя с
        /// половиной метрами выше того места, где стоит герой. Камера на
        /// берегу оказывалась ниже этой цифры, и экран заливало синим посуху.
        ///
        /// Гладь — плоскость, значит её высота в точке считается ровно: берём
        /// нормаль (это «вверх» объекта) и решаем уравнение плоскости.
        /// Почти горизонтальную проверку пропускаем: у вертикальной плоскости
        /// «высота над точкой» смысла не имеет и делить было бы не на что.
        /// </summary>
        private static bool SurfaceHeight(Transform plane, Vector3 at, out float height)
        {
            Vector3 n = plane.up;
            height = 0f;

            if (Mathf.Abs(n.y) < 0.2f) return false;

            Vector3 p = plane.position;
            height = p.y - (n.x * (at.x - p.x) + n.z * (at.z - p.z)) / n.y;

            return true;
        }

        private float nextLook;

        private void LateUpdate()
        {
            // Список пересобираем, пока он пуст: камера в нашей игре
            // создаётся раньше мира, и на первом кадре воды ещё нет.
            if ((water == null || water.Length == 0) && Time.time > nextLook)
            {
                nextLook = Time.time + 1f;
                Collect();

                if (water.Length > 0)
                    Debug.Log("[IsoRPG] Подводный вид: найдено водоёмов " + water.Length);
            }

            if (water == null || water.Length == 0) return;

            bool now = false;
            var at = eye.transform.position;

            foreach (var w in water)
            {
                if (w == null) continue;

                var box = w.bounds;

                // Внутри водоёма по горизонтали.
                if (at.x < box.min.x || at.x > box.max.x) continue;
                if (at.z < box.min.z || at.z > box.max.z) continue;

                // Уровень воды — в этой точке, а не «верх коробки»: русло
                // идёт под уклон, и разница доходит до трёх с половиной
                // метров.
                if (!SurfaceHeight(w.transform, at, out float surface)) continue;

                // По высоте — с разными порогами на вход и выход (гистерезис):
                // если уже под водой, остаёмся ею, пока не поднимемся
                // заметно выше глади; если ещё на суше, ныряем только когда
                // опустимся заметно ниже. Одна и та же линия для обоих
                // направлений дребезжит на кромке пруда.
                bool underThisOne = submerged
                    ? at.y < surface + surfaceThreshold
                    : at.y < surface - diveThreshold;

                if (!underThisOne) continue;

                now = true;
                break;
            }

            if (now == submerged) return;

            submerged = now;

            Debug.Log(submerged
                ? "[IsoRPG] Камера ушла под воду на " + (eye.transform.position.y).ToString("0.00")
                : "[IsoRPG] Камера вышла из воды");

            if (overlay != null)
                overlay.color = new Color(tint.r, tint.g, tint.b, submerged ? alpha : 0f);
        }

        /// <summary>
        /// Задать вид воды снаружи.
        ///
        /// Нужен отдельным вызовом: поля сериализованы, и у компонента,
        /// который уже лежит в сцене, стоит СТАРОЕ число. Правка умолчания
        /// в коде на него не действует — снаружи это выглядит как «поменял,
        /// а в игре то же самое». На этой ловушке мы горели дважды.
        /// </summary>
        public void SetLook(Color color, float overlayAlpha)
        {
            tint = color;
            alpha = overlayAlpha;

            if (overlay != null)
                overlay.color = new Color(tint.r, tint.g, tint.b, submerged ? alpha : 0f);
        }

        /// <summary>Пересобрать список водоёмов: после пересборки мира.</summary>
        public void Refresh() => Collect();
    }
}
