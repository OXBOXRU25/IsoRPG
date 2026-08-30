using System.Linq;
using UnityEngine;

namespace IsoRPG.Cameras
{
    /// <summary>
    /// Подводный вид: когда камера опускается ниже глади.
    ///
    /// Без этого вода просто исчезает. Гладь — плоскость, у которой грани
    /// смотрят вверх; из-под воды видна её изнанка, а изнанка не рисуется.
    /// Заказчик увидел ровно это: опустил камеру к воде — пруд пропал, и
    /// осталось сухое дно, посреди которого стоит герой по пояс.
    ///
    /// Двусторонняя гладь (снятое отсечение граней у материала) возвращает
    /// поверхность в кадр, но одной её мало: под водой должен меняться сам
    /// воздух. В WoW это плотная зеленоватая мгла с падением видимости —
    /// по ней и видно, что ты под водой, а не в мутной комнате.
    ///
    /// Туман здесь общий, сценический: он и есть самый дешёвый способ дать
    /// объём. Прежние настройки запоминаются при входе и возвращаются при
    /// выходе, иначе после первого же нырка мир останется в тумане.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class Underwater : MonoBehaviour
    {
        [Tooltip("Цвет воды изнутри.")]
        [SerializeField] private Color fogColor = new Color(0.10f, 0.42f, 0.44f);

        [Tooltip("Плотность подводного тумана: чем больше, тем ближе предел видимости.")]
        [SerializeField] private float fogDensity = 0.055f;

        [Tooltip("Насколько глубже глади должна опуститься камера, чтобы счёт пошёл, метры.")]
        [SerializeField] private float threshold = 0.05f;

        [Tooltip("По какому куску имени материала узнаём воду.")]
        [SerializeField] private string waterMaterial = "Water_Lake";

        private Renderer[] water;
        private Camera eye;
        private bool submerged;

        // Что было до погружения.
        private bool hadFog;
        private Color hadColor;
        private FogMode hadMode;
        private float hadDensity, hadStart, hadEnd;

        private void Awake()
        {
            eye = GetComponent<Camera>();
            Collect();

            Debug.Log("[IsoRPG] Подводный вид включён на камере «" + name +
                      "», водоёмов при старте " + (water != null ? water.Length : 0));
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
            water = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,
                                                FindObjectsSortMode.None)
                    .Where(r => r.sharedMaterial != null &&
                                r.sharedMaterial.name.Contains(waterMaterial))
                    .ToArray();
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

                // Внутри водоёма по горизонтали и ниже его поверхности.
                if (at.x < box.min.x || at.x > box.max.x) continue;
                if (at.z < box.min.z || at.z > box.max.z) continue;
                if (at.y > box.max.y - threshold) continue;

                now = true;
                break;
            }

            if (now == submerged) return;

            submerged = now;

            Debug.Log(submerged
                ? "[IsoRPG] Камера ушла под воду на " + (eye.transform.position.y).ToString("0.00")
                : "[IsoRPG] Камера вышла из воды");

            if (submerged) Dive();
            else Surface();
        }

        private void Dive()
        {
            hadFog = RenderSettings.fog;
            hadColor = RenderSettings.fogColor;
            hadMode = RenderSettings.fogMode;
            hadDensity = RenderSettings.fogDensity;
            hadStart = RenderSettings.fogStartDistance;
            hadEnd = RenderSettings.fogEndDistance;

            RenderSettings.fog = true;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;

            // Небо под водой не нужно: сквозь толщу его не видно, а голубая
            // полоса на горизонте выдаёт, что мы всё ещё «на воздухе».
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = fogColor;
        }

        private void Surface()
        {
            RenderSettings.fog = hadFog;
            RenderSettings.fogColor = hadColor;
            RenderSettings.fogMode = hadMode;
            RenderSettings.fogDensity = hadDensity;
            RenderSettings.fogStartDistance = hadStart;
            RenderSettings.fogEndDistance = hadEnd;

            eye.clearFlags = CameraClearFlags.Skybox;
        }

        /// <summary>
        /// Задать вид воды снаружи.
        ///
        /// Нужен отдельным вызовом: поля сериализованы, и у компонента,
        /// который уже лежит в сцене, стоит СТАРОЕ число. Правка умолчания
        /// в коде на него не действует — снаружи это выглядит как «поменял,
        /// а в игре то же самое». На этой ловушке мы горели дважды.
        /// </summary>
        public void SetLook(Color color, float density)
        {
            fogColor = color;
            fogDensity = density;
        }

        /// <summary>Пересобрать список водоёмов: после пересборки мира.</summary>
        public void Refresh() => Collect();
    }
}
