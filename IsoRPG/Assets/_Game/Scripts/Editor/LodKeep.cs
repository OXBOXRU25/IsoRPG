using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Запрещает растительности исчезать вдали.
    ///
    /// Павлон 02.09.2026: «часть цветов в процессе исчезает и появляется
    /// снова», и позже решающее уточнение — «когда камера приближена, всё
    /// ок, только когда далеко». Это уровни детализации: у растения три
    /// уровня, а после последнего стоит порог, на котором объект вырезается
    /// совсем. Отодвинул камеру — цветок стал мельче порога и пропал.
    ///
    /// Лечили сперва общим множителем дальности (`lodBias`), и это работало,
    /// но неправильно: множитель оттягивает ВСЕ переходы, то есть заставляет
    /// рисовать вдали подробную модель. Павлон предложил лучше — «не
    /// множитель, а снизить детализацию». Так и делаем: последний уровень
    /// (самый дешёвый) остаётся видимым всегда, вырезание отменяется.
    ///
    /// Цена: вдали рисуются простые меши вместо пустоты. Это дешевле, чем
    /// держать там подробные, и не трогает остальную сцену.
    ///
    /// **Прогонять после каждого переимпорта наборов**: папки наборов лежат
    /// в `.gitignore`, и правка порогов уезжает вместе с ними — та же беда,
    /// что у задания `mipcover`.
    /// </summary>
    public static class LodKeep
    {
        /// <summary>
        /// Во сколько раз дольше держится подробная модель.
        ///
        /// Четверть означает вчетверо: порог переключения — это высота на
        /// экране, ниже которой берётся упрощённая модель, так что чем он
        /// меньше, тем дольше живёт подробная.
        /// </summary>
        private const float DetailHold = 0.25f;

        /// <summary>Где живёт растительность. Другие наборы не трогаем.</summary>
        private static readonly string[] Folders =
        {
            "Assets/PolygonNatureBiomes",
        };

        [MenuItem("Tools/IsoRPG/Мир: растениям не исчезать вдали", priority = 33)]
        public static void Apply()
        {
            int prefabs = 0, fixedGroups = 0, skipped = 0;

            foreach (var folder in Folders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (var path in Directory.GetFiles(folder, "*.prefab", SearchOption.AllDirectories))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                    if (asset == null) continue;

                    var groups = asset.GetComponentsInChildren<LODGroup>(true);
                    if (groups.Length == 0) continue;

                    prefabs++;
                    bool touched = false;

                    foreach (var group in groups)
                    {
                        var lods = group.GetLODs();
                        if (lods.Length == 0) continue;

                        // Плавный переход — прочь.
                        //
                        // Он включён как CrossFade без анимации, а это значит
                        // дизеринг: в переходной зоне часть пикселей объекта
                        // выбрасывается по шумовой маске. Вблизи не видно, а
                        // на дальнем плане, где цветок занимает несколько
                        // пикселей, выброшенные пиксели читаются как мигание —
                        // и сразу полосой, потому что в переходной зоне
                        // оказываются все растения на одном удалении.
                        //
                        // Павлон 02.09.2026 описал это как «цветы исчезают и
                        // появляются, только когда камера далеко».
                        if (group.fadeMode != LODFadeMode.None)
                        {
                            group.fadeMode = LODFadeMode.None;
                            EditorUtility.SetDirty(group);
                        }

                        // Отодвигаем ПЕРЕКЛЮЧЕНИЕ на упрощённые модели.
                        //
                        // Павлон 02.09.2026, решающее наблюдение: «как бы
                        // модель упрощается при зуме». Так и есть — у куста
                        // на дальних уровнях просто меньше цветков, художник
                        // так его упростил. Отодвинул камеру, куст перешёл на
                        // упрощённый вид, часть цветов исчезла; тронулся —
                        // граница поехала, и это читается как мигание.
                        //
                        // Порог означает «высота на экране, ниже которой
                        // переключаемся». Уменьшаем его вчетверо: подробная
                        // модель держится вчетверо дальше. То же самое давал
                        // общий множитель дальности, но он оттягивал переходы
                        // во всей сцене; здесь платим только за растения.
                        for (int i = 0; i < lods.Length - 1; i++)
                        {
                            float was = lods[i].screenRelativeTransitionHeight;
                            if (was <= 0.0001f) continue;

                            float now = was * DetailHold;
                            if (Mathf.Abs(now - was) < 0.0001f) continue;

                            lods[i].screenRelativeTransitionHeight = now;
                            group.SetLODs(lods);
                            EditorUtility.SetDirty(group);
                            touched = true;
                        }

                        // Последний уровень — самый дешёвый. Его порог и есть
                        // граница вырезания: ниже неё объект не рисуется вовсе.
                        int last = lods.Length - 1;

                        if (lods[last].screenRelativeTransitionHeight <= 0.0001f)
                        {
                            skipped++;
                            continue;
                        }

                        lods[last].screenRelativeTransitionHeight = 0f;
                        group.SetLODs(lods);

                        EditorUtility.SetDirty(group);
                        touched = true;
                        fixedGroups++;
                    }

                    if (touched) EditorUtility.SetDirty(asset);
                }
            }

            AssetDatabase.SaveAssets();

            // Щуп: перечитываем с диска и считаем, у скольких порог остался.
            int left = 0;

            foreach (var folder in Folders)
            {
                if (!Directory.Exists(folder)) continue;

                left += Directory.GetFiles(folder, "*.prefab", SearchOption.AllDirectories)
                                 .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/')))
                                 .Where(a => a != null)
                                 .SelectMany(a => a.GetComponentsInChildren<LODGroup>(true))
                                 .Select(g => g.GetLODs())
                                 .Where(l => l.Length > 0)
                                 .Count(l => l[l.Length - 1].screenRelativeTransitionHeight > 0.0001f);
            }

            // Щуп по плавным переходам: их не должно остаться ни одного.
            int fades = 0;

            foreach (var folder in Folders)
            {
                if (!Directory.Exists(folder)) continue;

                fades += Directory.GetFiles(folder, "*.prefab", SearchOption.AllDirectories)
                                  .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/')))
                                  .Where(a => a != null)
                                  .SelectMany(a => a.GetComponentsInChildren<LODGroup>(true))
                                  .Count(g => g.fadeMode != LODFadeMode.None);
            }

            Debug.Log($"[IsoRPG] Растения не исчезают: префабов с уровнями {prefabs}, " +
                      $"поправлено групп {fixedGroups}, уже были {skipped}, " +
                      $"осталось с вырезанием {left}, с плавным переходом {fades}.");
        }
    }
}
