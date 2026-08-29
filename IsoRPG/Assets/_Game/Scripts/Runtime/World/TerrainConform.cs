using UnityEngine;

namespace IsoRPG.World
{
    /// <summary>
    /// Отдать рельеф шейдеру растительности, чтобы трава облегала холмы.
    ///
    /// Зачем. Куст травы у нас плоский и широкий: высота метр, поперечник
    /// четыре с половиной. На склоне под ним перепад земли доходит до двух
    /// метров, и жёсткая плита не может лечь на такую поверхность никак —
    /// утопишь по центру, утонет целиком; посадишь по краю, задерётся. Это
    /// не ошибка посадки, а несовместимость размеров, и решается она только
    /// изгибом самой геометрии.
    ///
    /// Шейдер получает карту высот текстурой и опускает каждую вершину туда,
    /// где под ней земля. Куст перестаёт быть плитой и ложится ковром.
    ///
    /// Карту строим сами, а не берём <c>terrainData.heightmapTexture</c>:
    /// у неё формат и диапазон меняются между версиями Unity (историческое
    /// «высоты лежат в 0..0.5»), и подгонка множителя вслепую — это ровно
    /// тот подбор, которого мы избегаем. Своя текстура хранит долю высоты
    /// в понятном виде: 0 — низ террейна, 1 — его потолок.
    /// </summary>
    [ExecuteAlways]
    public sealed class TerrainConform : MonoBehaviour
    {
        private static readonly int MapId = Shader.PropertyToID("_PNBHeightMap");
        private static readonly int PosId = Shader.PropertyToID("_PNBTerrainPos");
        private static readonly int SizeId = Shader.PropertyToID("_PNBTerrainSize");

        private Texture2D map;

        private void OnEnable()
        {
            Build();
        }

        private void OnDisable()
        {
            // Оставлять чужому шейдеру ссылку на уничтоженную текстуру нельзя:
            // растительность станет чёрной или пропадёт, и виноватым будет
            // выглядеть шейдер.
            Shader.SetGlobalTexture(MapId, Texture2D.blackTexture);
            Shader.SetGlobalVector(SizeId, Vector4.zero);
        }

        private void Build()
        {
            var terrain = GetComponent<Terrain>();

            if (terrain == null)
            {
                terrain = Object.FindObjectsByType<Terrain>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None) is { Length: > 0 } all
                    ? all[0] : null;
            }

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — траве не по чему изгибаться.");
                return;
            }

            var data = terrain.terrainData;
            int res = data.heightmapResolution;

            float[,] h = data.GetHeights(0, 0, res, res);

            // RFloat: одна величина на точку, без потерь на упаковку.
            // 513x513 — это чуть больше мегабайта, для карты 400 метров шаг
            // выходит около 0.8 м, и билинейная фильтрация сглаживает его.
            map = new Texture2D(res, res, TextureFormat.RFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Карта высот для травы"
            };

            var pixels = new Color[res * res];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    // GetHeights отдаёт [строка, столбец] — то есть [z, x].
                    // Перепутать оси здесь означает получить траву, изогнутую
                    // по зеркальному рельефу: на глаз похоже на правду, а на
                    // склонах разъезжается.
                    pixels[y * res + x] = new Color(h[y, x], 0f, 0f, 1f);
                }
            }

            map.SetPixels(pixels);
            map.Apply(false, false);

            var pos = terrain.transform.position;
            var size = data.size;

            Shader.SetGlobalTexture(MapId, map);
            Shader.SetGlobalVector(PosId, new Vector4(pos.x, pos.y, pos.z, 0f));
            Shader.SetGlobalVector(SizeId, new Vector4(size.x, size.y, size.z, 0f));

            Debug.Log("[IsoRPG] Рельеф отдан шейдеру травы: карта " + res + "x" + res +
                      ", участок " + size.x.ToString("0") + " x " + size.z.ToString("0") +
                      " м, высота " + size.y.ToString("0") +
                      " м, начало (" + pos.x.ToString("0") + ", " + pos.z.ToString("0") + ").");
        }
    }
}
