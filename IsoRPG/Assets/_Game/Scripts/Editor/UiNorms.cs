using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Снимает норматив автора с префабов набора интерфейса.
    ///
    /// Павлон 01.09.2026: «поизучай в наборе, наверняка есть какие-то
    /// стандарты, опять что-то сам выдумываешь — размерный ряд там,
    /// наложение». Он прав: толщину рамки и утопление полоски я подбирал
    /// сам, хотя у Synty те же вещи уже расставлены руками в его префабах.
    ///
    /// Что печатаем:
    ///   - какие размеры окон и панелей автор использует (размерный ряд);
    ///   - с какими множителями он рисует рамки 9-slice — это и есть ответ
    ///     на «какой толщины стенка»;
    ///   - насколько рамка выступает за содержимое (наложение);
    ///   - размеры слотов и полосок.
    ///
    /// Ничего не меняет — только читает.
    /// </summary>
    public static class UiNorms
    {
        private const string Prefabs = "Assets/Synty/InterfaceFantasyWarriorHUD/Prefabs";

        [MenuItem("Tools/IsoRPG/Щуп: норматив интерфейса Synty", priority = 43)]
        public static void Run()
        {
            var sizes = new List<(string name, float w, float h)>();
            var sliced = new List<(string sprite, float mult, float w, float h, Vector4 border)>();

            int prefabs = 0;

            foreach (var path in Directory.GetFiles(Prefabs, "*.prefab", SearchOption.AllDirectories))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                if (root == null) continue;

                prefabs++;

                var rect = root.GetComponent<RectTransform>();
                if (rect != null && rect.sizeDelta.x > 40f && rect.sizeDelta.y > 40f)
                    sizes.Add((root.name, rect.sizeDelta.x, rect.sizeDelta.y));

                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image == null || image.type != Image.Type.Sliced || image.sprite == null) continue;

                    var own = image.rectTransform;

                    sliced.Add((image.sprite.name, image.pixelsPerUnitMultiplier,
                                own.sizeDelta.x, own.sizeDelta.y, image.sprite.border));
                }
            }

            var text = new StringBuilder();

            text.Append("[IsoRPG] Норматив Synty: префабов ").Append(prefabs).Append('\n');

            // --- Размерный ряд ---
            text.Append("  РАЗМЕРЫ КОРНЕВЫХ ЭЛЕМЕНТОВ (ширина×высота, сколько раз встречается):\n");

            foreach (var group in sizes.GroupBy(s => $"{s.w:0}×{s.h:0}")
                                       .OrderByDescending(g => g.Count())
                                       .Take(18))
            {
                text.Append("    ").Append(group.Key.PadRight(14))
                    .Append(group.Count().ToString().PadLeft(3)).Append(" шт   ")
                    .Append(string.Join(", ", group.Take(3).Select(s => s.name)))
                    .Append('\n');
            }

            // --- Множители 9-slice ---
            //
            // Это ответ на «какой толщины стенка»: множитель говорит, во
            // сколько раз автор ужал картинку относительно её пикселей.
            text.Append("  МНОЖИТЕЛИ 9-SLICE (спрайт: множитель — сколько раз):\n");

            foreach (var group in sliced.GroupBy(s => $"{s.sprite} ×{s.mult:0.00}")
                                        .OrderByDescending(g => g.Count())
                                        .Take(20))
            {
                var first = group.First();

                text.Append("    ").Append(group.Key.PadRight(52))
                    .Append(group.Count().ToString().PadLeft(3)).Append(" шт, ")
                    .Append($"поле {first.w:0}×{first.h:0}, границы {first.border.x:0}/{first.border.y:0}")
                    .Append('\n');
            }

            text.Append("  Всего растянутых картинок ").Append(sliced.Count)
                .Append(", из них с множителем 1: ")
                .Append(sliced.Count(s => Mathf.Approximately(s.mult, 1f)));

            Debug.Log(text.ToString());
        }
    }
}
